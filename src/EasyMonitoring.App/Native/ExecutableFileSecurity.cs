using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using EasyMonitoring.App.Autostart;
using Microsoft.Win32.SafeHandles;

namespace EasyMonitoring.App.Native;

internal static class ExecutableFileSecurity
{
    public static SafeFileHandle Open(string path, bool directory)
    {
        var handle = CreateFileW(path, 0x00020080, directory ? 3u : 1u, IntPtr.Zero,
            3, 0x02200000, IntPtr.Zero); // READ_CONTROL | READ_ATTRIBUTES; OPEN_EXISTING, no-follow
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error, $"Cannot open {path}");
    }

    public static FileAttributes Attributes(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return (FileAttributes)information.Attributes;
    }

    public static string FinalPath(SafeFileHandle handle)
    {
        var path = HandlePath(handle, 1);
        if (!path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
            throw new UnverifiedExecutablePathException("A stable local volume path is required.");
        return path;
    }

    private static string HandlePath(SafeFileHandle handle, uint flags)
    {
        var buffer = new StringBuilder(512);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, flags);
        if (length >= buffer.Capacity)
        {
            buffer.Capacity = checked((int)length + 1);
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, flags);
        }
        if (length == 0 || length >= buffer.Capacity)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return buffer.ToString();
    }

    public static string SchedulingPath(SafeFileHandle file, SafeFileHandle volume)
    {
        var volumePath = FinalPath(volume);
        var buffer = new char[32768];
        if (!GetVolumePathNamesForVolumeNameW(volumePath, buffer, (uint)buffer.Length, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var device = HandlePath(volume, 2).TrimEnd('\\'); // VOLUME_NAME_NT
        foreach (var mount in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (mount.Length != 3 || mount[1] != ':' || mount[2] != '\\') continue;
            var mapping = new StringBuilder(32768);
            if (QueryDosDeviceW(mount[..2], mapping, mapping.Capacity) == 0 ||
                !string.Equals(mapping.ToString(), device, StringComparison.OrdinalIgnoreCase)) continue;
            var final = FinalPath(file);
            if (!final.StartsWith(volumePath, StringComparison.OrdinalIgnoreCase))
                throw new UnverifiedExecutablePathException("Executable volume identity changed.");
            // Task Scheduler rejects volume-GUID Exec paths; use a Mount Manager drive, not a user-created alias.
            return mount + final[volumePath.Length..];
        }
        throw new UnverifiedExecutablePathException("No verified persistent drive-letter path is available for Task Scheduler.");
    }

    public static RawSecurityDescriptor Descriptor(SafeFileHandle handle)
    {
        var error = GetSecurityInfo(handle, 1, 5, out _, out _, out _, out _, out var descriptor);
        if (error != 0) throw new Win32Exception((int)error);
        try
        {
            var bytes = new byte[GetSecurityDescriptorLength(descriptor)];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            return new RawSecurityDescriptor(bytes, 0);
        }
        finally { LocalFree(descriptor); }
    }

    public static void RequirePhysicalDrive(string drive)
    {
        var buffer = new StringBuilder(32768);
        if (QueryDosDeviceW(drive, buffer, buffer.Capacity) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var target = buffer.ToString();
        const string prefix = @"\Device\HarddiskVolume";
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            target.Length == prefix.Length || target[prefix.Length..].Any(c => !char.IsAsciiDigit(c)))
            throw new UnverifiedExecutablePathException("SUBST, mapped and unsupported drives are not supported.");
    }

    public static void RequireLocalAclVolume(string root)
    {
        if (GetDriveTypeW(root) != 3)
            throw new UnverifiedExecutablePathException("Autostart requires a local fixed volume.");
        if (!GetVolumeInformationW(root, null, 0, out _, out _, out var flags, null, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((flags & 8) == 0)
            throw new UnverifiedExecutablePathException("The filesystem must support persistent Windows ACLs.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, int type, uint information,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);
    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDeviceW(string name, StringBuilder target, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNamesForVolumeNameW(string volume, [Out] char[] paths, uint length, out uint required);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(string root, StringBuilder? name, int nameLength,
        out uint serial, out uint maximumComponent, out uint flags, StringBuilder? filesystem, int filesystemLength);
}
