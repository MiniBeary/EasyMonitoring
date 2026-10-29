using System.Security.Principal;
using System.Xml.Linq;
using EasyMonitoring.App.Autostart;
using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.App.Tests;

public class ScheduledTaskAutostartManagerTests
{
    private static readonly string Sid = WindowsIdentity.GetCurrent().User!.Value;
    private const string Exe = @"C:\Protected\EasyMonitoring.exe";
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Theory]
    [InlineData(unchecked((int)0x80070002), TaskPresence.Missing)]
    [InlineData(unchecked((int)0x80070005), TaskPresence.Unknown)]
    [InlineData(1, TaskPresence.Unknown)]
    public void Only_exact_file_not_found_hresult_means_missing(int exit, TaskPresence expected)
    {
        var runner = new FakeRunner(new(exit, "localized error"));
        var manager = Create(runner);
        var probe = manager.Probe();
        Assert.Equal(expected, probe.Presence);
        Assert.Single(runner.Calls);
        Assert.Contains("/HRESULT", runner.Calls[0]);
        Assert.Equal(expected == TaskPresence.Missing, probe.IsKnown);
    }

    [Fact]
    public void Timeout_is_unknown_even_if_an_exit_value_looks_like_absence()
    {
        var probe = Create(new FakeRunner(new(unchecked((int)0x80070002), "timeout", true))).Probe();
        Assert.Equal(TaskPresence.Unknown, probe.Presence);
    }

    [Theory]
    [InlineData("<broken>")]
    [InlineData("<!DOCTYPE Task [<!ENTITY x SYSTEM 'file:///not-read'>]><Task>&x;</Task>")]
    public void Invalid_or_external_entity_xml_is_unknown(string xml)
    {
        var probe = Create(new FakeRunner(new(0, xml))).Probe();
        Assert.False(probe.IsKnown);
        Assert.Equal(TaskPresence.Present, probe.Presence);
    }

    [Fact]
    public void Probe_preserves_owner_and_unsafe_target_for_core_cleanup()
    {
        var guard = new FakeGuard(ExecutableProtection.Unsafe);
        var probe = Create(new FakeRunner(new(0, Xml())), guard).Probe();
        Assert.Equal(TaskPresence.Present, probe.Presence);
        Assert.Equal(TaskOwnership.CurrentUser, probe.Ownership);
        Assert.Equal(ExecutableProtection.Unsafe, probe.Protection);
    }

    [Fact]
    public void Foreign_principal_never_becomes_an_owned_task()
    {
        var probe = Create(new FakeRunner(new(0, Xml("S-1-5-18")))).Probe();
        Assert.Equal(TaskOwnership.OtherUser, probe.Ownership);
        Assert.False(probe.IsKnown);
    }

    [Fact]
    public void Additional_actions_are_not_silently_ignored()
    {
        var document = XDocument.Parse(Xml());
        document.Descendants(Ns + "Actions").Single().Add(new XElement(Ns + "Exec", new XElement(Ns + "Command", Exe)));
        var probe = Create(new FakeRunner(new(0, document.ToString()))).Probe();
        Assert.Equal(ExecutableProtection.Unknown, probe.Protection);
        Assert.False(probe.IsKnown);
    }

    [Fact]
    public void Refused_enable_creates_neither_task_nor_staging_file()
    {
        var runner = new FakeRunner(new(0, ""));
        var result = Create(runner, new FakeGuard(ExecutableProtection.Unsafe)).Enable();
        Assert.False(result.Success);
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Easy%Monitor\EasyMonitoring.exe")]
    [InlineData(@"\\server\protected\EasyMonitoring.exe")]
    public void Unverified_path_through_real_guard_and_adapter_does_not_authorize_legacy_deletion(string path)
    {
        var runner = new FakeRunner(new(0, Xml().Replace(Exe, path, StringComparison.Ordinal)));
        var manager = Create(runner, new ProtectedExecutablePath());
        var state = new AutostartStateModule(manager).Refresh(DateTimeOffset.UtcNow, force: true);
        Assert.Equal(ExecutableProtection.Unknown, state.Protection);
        Assert.False(state.IsKnown);
        Assert.Single(runner.Calls);
        Assert.DoesNotContain(runner.Calls, args => args[0] is "/Delete" or "/Create");
    }

    [Fact]
    public void Disable_is_one_command_without_current_executable_guard_or_post_probe()
    {
        var runner = new FakeRunner(new(0, ""));
        Assert.True(Create(runner, new FakeGuard(ExecutableProtection.Unsafe)).Disable().Success);
        Assert.Single(runner.Calls);
        Assert.Equal("/Delete", runner.Calls[0][0]);
    }

    [AdministratorTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enable_keeps_xml_locked_until_command_finishes_then_cleans_it(int exit)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EasyMonitoring-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "EasyMonitoring.exe");
        var runner = new FakeRunner(new(exit, "command result"));
        string? staging = null;
        runner.OnRun = args =>
        {
            Assert.Equal("/Create", args[0]);
            staging = args[Array.IndexOf(args, "/XML") + 1];
            Assert.Equal(directory, Path.GetDirectoryName(staging));
            var document = XDocument.Load(staging);
            Assert.Equal(executable, document.Descendants(Ns + "Command").Single().Value);
            Assert.Equal("HighestAvailable", document.Descendants(Ns + "RunLevel").Single().Value);
            Assert.Equal(Sid, document.Descendants(Ns + "Principal").Single().Element(Ns + "UserId")!.Value);
            Assert.Throws<IOException>(() => File.Open(staging, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
        };
        try
        {
            var manager = new ScheduledTaskAutostartManager(executable, Sid, runner, new FakeGuard(ExecutableProtection.Safe));
            var result = manager.Enable();
            Assert.True(runner.Calls.Count == 1, result.Error);
            Assert.True(result.Success == (exit == 0), result.Error);
            Assert.NotNull(staging);
            Assert.False(File.Exists(staging));
        }
        finally
        {
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.StartsWith("EasyMonitoring-tests-", Path.GetFileName(directory));
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ScheduledTaskAutostartManager Create(FakeRunner runner, IExecutablePathGuard? guard = null) =>
        new(Exe, Sid, runner, guard ?? new FakeGuard(ExecutableProtection.Safe));

    private static string Xml(string? user = null) => $"""
        <Task xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task" version="1.4">
          <Principals><Principal id="Author"><UserId>{user ?? Sid}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
          <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user ?? Sid}</UserId></LogonTrigger></Triggers>
          <Settings><Enabled>true</Enabled></Settings>
          <Actions Context="Author"><Exec><Command>{Exe}</Command><WorkingDirectory>C:\Protected</WorkingDirectory></Exec></Actions>
        </Task>
        """;

    private sealed class FakeGuard(ExecutableProtection protection) : IExecutablePathGuard
    {
        public ExecutablePathLease Inspect(string path) => new(protection, path, protection == ExecutableProtection.Safe ? null : "unsafe path");
    }

    private sealed class FakeRunner(ScheduledTaskCommandResult result) : IScheduledTaskCommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public Action<string[]>? OnRun { get; set; }
        public ScheduledTaskCommandResult Run(IReadOnlyList<string> arguments)
        {
            var args = arguments.ToArray();
            Calls.Add(args);
            OnRun?.Invoke(args);
            return result;
        }
    }
}
