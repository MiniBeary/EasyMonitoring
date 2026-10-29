using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;
using EasyMonitoring.App.Autostart;
using EasyMonitoring.App.Native;
using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.App.Tests;

[Collection(nameof(LifecycleTestCollection))]
public class AutostartWindowsTests
{
    [AdministratorFact]
    public async Task Protected_task_registers_runs_and_is_removed_without_touching_user_task()
    {
        using var directory = new ProtectedTestDirectory();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TaskFixture")))
            File.Copy(file, Path.Combine(directory.Path, System.IO.Path.GetFileName(file)));
        var executable = System.IO.Path.Combine(directory.Path, "EasyMonitoring.TaskFixture.exe");
        var name = "EasyMonitoring.Tests." + Guid.NewGuid().ToString("N");
        var runner = new ScheduledTaskCommandRunner();
        var manager = new ScheduledTaskAutostartManager(executable, WindowsIdentity.GetCurrent().User!.Value,
            runner, new ProtectedExecutablePath(), name);
        var module = new AutostartStateModule(manager);
        try
        {
            using (var control = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true })!)
            {
                Assert.True(control.WaitForExit(10000));
                Assert.Equal(0, control.ExitCode);
            }
            var marker = System.IO.Path.Combine(directory.Path, "task-ran.txt");
            Assert.True(File.Exists(marker), "The ordinary-path control did not execute.");
            File.Delete(marker);
            File.Delete(System.IO.Path.Combine(directory.Path, "task-pid.txt"));
            var enabled = module.SetEnabled(true, DateTimeOffset.UtcNow);
            Assert.True(enabled.Success, enabled.Error);
            Assert.Equal(ExecutableProtection.Safe, module.State.Protection);
            var originalCodePage = GetConsoleOutputCP();
            var legacyQuery = Utf8TaskQuery.Run(["/Query", "/TN", name, "/XML", "/HRESULT"], codePage: 437);
            Assert.True(legacyQuery.Success, legacyQuery.Output);
            Assert.DoesNotContain(executable, legacyQuery.Output);
            Assert.Equal(originalCodePage, GetConsoleOutputCP());
            var xml = runner.Run(["/Query", "/TN", name, "/XML", "/HRESULT"]);
            Assert.True(xml.Success, xml.Output);
            Assert.Equal(originalCodePage, GetConsoleOutputCP());
            Assert.Equal(executable, XDocument.Parse(xml.Output).Descendants(
                XName.Get("Command", "http://schemas.microsoft.com/windows/2004/02/mit/task")).Single().Value);
            var run = runner.Run(["/Run", "/TN", name]);
            Assert.True(run.Success, run.Output);
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(marker) && deadline.Elapsed < TimeSpan.FromSeconds(10))
                await Task.Delay(50);
            Assert.True(File.Exists(marker), "The protected fixture did not execute. " +
                runner.Run(["/Query", "/TN", name, "/V", "/FO", "LIST"]).Output);
            Assert.Equal("ok", File.ReadAllText(marker));
            await WaitForFixtureExit(directory.Path);
            Assert.True(module.SetEnabled(false, DateTimeOffset.UtcNow).Success);
            Assert.Equal(TaskPresence.Missing, manager.Probe().Presence);
        }
        finally
        {
            try { await WaitForFixtureExit(directory.Path); }
            finally
            {
                runner.Run(["/Delete", "/TN", name, "/F"]);
                Assert.Equal(TaskPresence.Missing, manager.Probe().Presence);
            }
        }
    }

    private static async Task WaitForFixtureExit(string directory)
    {
        var pidFile = System.IO.Path.Combine(directory, "task-pid.txt");
        if (!File.Exists(pidFile)) return;
        var pid = int.Parse(File.ReadAllText(pidFile), System.Globalization.CultureInfo.InvariantCulture);
        Process process;
        try { process = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; } // The fixture has already exited.
        using (process)
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    [AdministratorFact]
    public void Reparse_directory_is_refused_even_when_it_points_to_a_safe_executable()
    {
        using var directory = new ProtectedTestDirectory();
        var link = System.IO.Path.Combine(directory.Path, "redirect");
        Directory.CreateSymbolicLink(link, Environment.SystemDirectory);
        try
        {
            using var lease = new ProtectedExecutablePath().Inspect(System.IO.Path.Combine(link, "cmd.exe"));
            Assert.Equal(ExecutableProtection.Unknown, lease.Protection);
        }
        finally { Directory.Delete(link); }
    }

    [AdministratorFact]
    public void Substituted_drive_is_refused()
    {
        var assigned = DriveInfo.GetDrives().Select(d => d.Name[..2]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var drive = Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => ((char)c) + ":")
            .FirstOrDefault(candidate => !assigned.Contains(candidate));
        Assert.NotNull(drive);
        RunSubst(drive!, Environment.SystemDirectory);
        try
        {
            using var lease = new ProtectedExecutablePath().Inspect(drive + "\\cmd.exe");
            Assert.Equal(ExecutableProtection.Unknown, lease.Protection);
        }
        finally { RunSubst(drive!, "/D"); }
    }

    [AdministratorFact]
    public void User_writable_executable_cannot_create_a_real_elevated_task()
    {
        var name = "EasyMonitoring.Tests.Refusal." + Guid.NewGuid().ToString("N");
        var runner = new ScheduledTaskCommandRunner();
        var manager = new ScheduledTaskAutostartManager(
            System.IO.Path.Combine(AppContext.BaseDirectory, "TaskFixture", "EasyMonitoring.TaskFixture.exe"),
            WindowsIdentity.GetCurrent().User!.Value, runner, new ProtectedExecutablePath(), name);
        var result = new AutostartStateModule(manager).SetEnabled(true, DateTimeOffset.UtcNow);
        try
        {
            Assert.False(result.Success);
            Assert.Contains("protected", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TaskPresence.Missing, manager.Probe().Presence);
        }
        finally { runner.Run(["/Delete", "/TN", name, "/F"]); }
    }

    [Fact]
    public void Xml_query_does_not_accept_shell_metacharacters()
    {
        var result = new ScheduledTaskCommandRunner().Run(["/Query", "/TN", "EasyMonitoring&whoami", "/XML", "/HRESULT"]);
        Assert.Null(result.ExitCode);
        Assert.Equal("Unsupported task query arguments.", result.Output);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();

    private static void RunSubst(string drive, string target)
    {
        var start = new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "subst.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(drive);
        start.ArgumentList.Add(target);
        using var process = Process.Start(start)!;
        Assert.True(process.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
    }

    private sealed class ProtectedTestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EasyMonitoring.Tests." + Guid.NewGuid().ToString("N") + ".тест&copy");
        public ProtectedTestDirectory()
        {
            var security = new DirectorySecurity();
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(admins);
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(Path).Create(security);
        }
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), System.IO.Path.GetDirectoryName(resolved));
            Assert.StartsWith("EasyMonitoring.Tests.", System.IO.Path.GetFileName(resolved));
            Directory.Delete(resolved, recursive: true);
        }
    }
}

public sealed class AdministratorFactAttribute : FactAttribute
{
    public AdministratorFactAttribute()
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            Skip = "Requires an elevated Windows test process; creates only isolated fixtures and unique test tasks.";
    }
}

public sealed class AdministratorTheoryAttribute : TheoryAttribute
{
    public AdministratorTheoryAttribute()
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            Skip = "Requires an elevated Windows test process for protected XML staging.";
    }
}
