using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using EasyMonitoring.App.Autostart;
using Microsoft.Win32.SafeHandles;

namespace EasyMonitoring.App.Native;

// schtasks converts XML to its console code page even when stdout is redirected.
// A private hidden console preserves Unicode without changing the caller's console.
internal static class Utf8TaskQuery
{
    private static readonly object Gate = new();

    public static ScheduledTaskCommandResult Run(IReadOnlyList<string> arguments, uint codePage = 65001)
    {
        // This shell boundary accepts only the fixed query switches and ASCII task identifiers.
        if (arguments.Count == 0 || arguments[0] != "/Query" || !arguments.Contains("/XML") ||
            arguments.Any(a => a.Length == 0 || a.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('/' or '\\' or '.' or '-' or '_'))))
            return new(null, "Unsupported task query arguments.");
        lock (Gate) return RunLocked(arguments, codePage);
    }

    private static ScheduledTaskCommandResult RunLocked(IReadOnlyList<string> arguments, uint codePage)
    {
        var system = Environment.SystemDirectory;
        if (system.IndexOfAny(['"', '%', '\r', '\n']) >= 0)
            return new(null, "Unsupported system directory for task query.");
        var command = new StringBuilder($"\"{Path.Combine(system, "cmd.exe")}\" /d /q /v:off /s /c \"\"{Path.Combine(system, "chcp.com")}\" {codePage} >nul && \"{Path.Combine(system, "schtasks.exe")}\" {string.Join(' ', arguments.Select(a => $"\"{a}\""))}\"");
        try
        {
            using var outputPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            using var errorPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            using var inputPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            var startup = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfo>(), Flags = 0x101, // USESTDHANDLES | USESHOWWINDOW, SW_HIDE
                Input = inputPipe.ClientSafePipeHandle.DangerousGetHandle(),
                Output = outputPipe.ClientSafePipeHandle.DangerousGetHandle(),
                Error = errorPipe.ClientSafePipeHandle.DangerousGetHandle(),
            };
            if (!CreateProcessW(Path.Combine(system, "cmd.exe"), command, IntPtr.Zero, IntPtr.Zero,
                    true, 0x10, IntPtr.Zero, system, ref startup, out var information)) // CREATE_NEW_CONSOLE
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using var process = new SafeProcessHandle(information.Process, ownsHandle: true);
            using var thread = new SafeWaitHandle(information.Thread, ownsHandle: true);
            outputPipe.DisposeLocalCopyOfClientHandle();
            errorPipe.DisposeLocalCopyOfClientHandle();
            inputPipe.DisposeLocalCopyOfClientHandle();
            inputPipe.Dispose();
            // An explicit legacy page is used only by the regression test control.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding((int)codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            using var outputReader = new StreamReader(outputPipe, encoding);
            using var errorReader = new StreamReader(errorPipe, encoding);
            var output = outputReader.ReadToEndAsync();
            var error = errorReader.ReadToEndAsync();
            var wait = WaitForSingleObject(process, 15000);
            if (wait == 258)
            {
                using var running = Process.GetProcessById((int)information.ProcessId);
                running.Kill(entireProcessTree: true);
                return new(null, "schtasks.exe timed out.", TimedOut: true);
            }
            if (wait != 0 || !GetExitCodeProcess(process, out var exitCode))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return new(unchecked((int)exitCode), string.Join(Environment.NewLine,
                new[] { output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult() }
                    .Where(line => !string.IsNullOrWhiteSpace(line))).Trim());
        }
        catch (Exception exception) { return new(null, exception.Message); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public ushort ShowWindow, ReservedLength;
        public IntPtr ReservedBytes, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder command,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle handle, out uint exitCode);
}
