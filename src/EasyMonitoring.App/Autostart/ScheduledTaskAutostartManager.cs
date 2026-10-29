using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.App.Autostart;

internal sealed record ScheduledTaskCommandResult(int? ExitCode, string Output, bool TimedOut = false)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}

internal interface IScheduledTaskCommandRunner
{
    ScheduledTaskCommandResult Run(IReadOnlyList<string> arguments);
}

internal sealed class ScheduledTaskAutostartManager : IAutostartManager
{
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private readonly string _executable;
    private readonly string _sid;
    private readonly string _taskName;
    private readonly IScheduledTaskCommandRunner _runner;
    private readonly IExecutablePathGuard _guard;

    public ScheduledTaskAutostartManager() : this("EasyMonitoring") { }

    internal ScheduledTaskAutostartManager(string taskName) : this(
        Environment.ProcessPath ?? string.Empty, WindowsIdentity.GetCurrent().User?.Value ?? string.Empty,
        new ScheduledTaskCommandRunner(), new ProtectedExecutablePath(), taskName) { }

    internal ScheduledTaskAutostartManager(string executable, string sid,
        IScheduledTaskCommandRunner runner, IExecutablePathGuard guard, string taskName = "EasyMonitoring")
    {
        _executable = executable;
        _sid = sid;
        _runner = runner;
        _guard = guard;
        _taskName = taskName;
    }

    public AutostartProbe Probe()
    {
        var query = _runner.Run(["/Query", "/TN", _taskName, "/XML", "/HRESULT"]);
        if (!query.Success)
        {
            if (!query.TimedOut && query.ExitCode == unchecked((int)0x80070002))
                return new(true, false, Presence: TaskPresence.Missing);
            return new(false, false, Error: query.Output);
        }

        TaskOwnership owner = TaskOwnership.Unknown;
        try
        {
            using var reader = XmlReader.Create(new StringReader(query.Output.TrimStart('\ufeff')),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root?.Name != TaskNamespace + "Task") throw new InvalidDataException("Unexpected task XML.");
            var principals = root.Elements(TaskNamespace + "Principals").Elements(TaskNamespace + "Principal").ToArray();
            if (principals.Length != 1) throw new InvalidDataException("Task principal is ambiguous.");
            var principal = principals[0];
            var principalSid = ResolveSid(principal.Element(TaskNamespace + "UserId")?.Value);
            owner = principalSid is null ? TaskOwnership.Unknown :
                principalSid == _sid ? TaskOwnership.CurrentUser : TaskOwnership.OtherUser;
            if (owner != TaskOwnership.CurrentUser)
                return new(false, false, Error: "The existing task does not have a verified current-user principal.",
                    Presence: TaskPresence.Present, Ownership: owner);

            var actions = root.Elements(TaskNamespace + "Actions").ToArray();
            var entries = actions.SelectMany(a => a.Elements()).ToArray();
            if (actions.Length != 1 || entries.Length != 1 || entries[0].Name != TaskNamespace + "Exec" ||
                (actions[0].Attribute("Context") is { } context && context.Value != principal.Attribute("id")?.Value) ||
                !string.IsNullOrWhiteSpace(entries[0].Element(TaskNamespace + "Arguments")?.Value))
                throw new InvalidDataException("Task actions or arguments are not an unambiguous EasyMonitoring action.");

            var command = entries[0].Element(TaskNamespace + "Command")?.Value ?? string.Empty;
            var directory = entries[0].Element(TaskNamespace + "WorkingDirectory")?.Value;
            using var target = _guard.Inspect(command);
            var protection = target.Protection;
            var error = target.Error;
            if (protection == ExecutableProtection.Safe &&
                !string.Equals(directory, target.DirectoryPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(directory, Path.GetDirectoryName(command), StringComparison.OrdinalIgnoreCase))
            {
                protection = ExecutableProtection.Unknown;
                error = "Task working directory does not match its protected executable.";
            }
            var triggers = root.Elements(TaskNamespace + "Triggers").SelectMany(t => t.Elements()).ToArray();
            var health = new AutostartTaskHealth(
                principal.Element(TaskNamespace + "RunLevel")?.Value == "HighestAvailable",
                principal.Element(TaskNamespace + "LogonType")?.Value == "InteractiveToken",
                triggers.Length == 1 && triggers[0].Name == TaskNamespace + "LogonTrigger" &&
                triggers[0].Element(TaskNamespace + "Enabled")?.Value != "false" &&
                ResolveSid(triggers[0].Element(TaskNamespace + "UserId")?.Value) == _sid);
            using var current = _guard.Inspect(_executable);
            var matches = current.Protection == ExecutableProtection.Safe &&
                string.Equals(current.ExecutablePath, target.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            var enabled = root.Element(TaskNamespace + "Settings")?.Element(TaskNamespace + "Enabled")?.Value != "false" &&
                matches && health.HasRunLevel && health.HasInteractiveToken && health.HasLogonTrigger;
            return new(true, enabled, health, error, TaskPresence.Present, owner, protection);
        }
        catch (Exception exception)
        {
            return new(false, false, Error: exception.Message, Presence: TaskPresence.Present, Ownership: owner);
        }
    }

    public AutostartResult Enable()
    {
        using var lease = _guard.Inspect(_executable);
        if (lease.Protection != ExecutableProtection.Safe || string.IsNullOrEmpty(_sid))
            return new(false, $"Autostart requires an administrator-protected executable and folder. {lease.Error} Copy the exe into a protected folder, then try again.");

        string? staging = null;
        try
        {
            staging = Path.Combine(lease.StagingDirectoryPath, $".EasyMonitoring-autostart-{Guid.NewGuid():N}.xml");
            var security = new FileSecurity();
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(administrators);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            using (var stream = new FileInfo(staging).Create(FileMode.CreateNew,
                FileSystemRights.ReadData | FileSystemRights.WriteData, FileShare.Read, 4096, FileOptions.WriteThrough, security))
            {
                using (var writer = new StreamWriter(stream, Encoding.Unicode, leaveOpen: true))
                    BuildTaskXml(lease).Save(writer, SaveOptions.DisableFormatting);
                stream.Flush(flushToDisk: true);
            }
            // The protected parent and DACL secure the handoff; a read handle also permits schtasks' read-only sharing mode.
            using var readLock = File.Open(staging, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = _runner.Run(["/Create", "/TN", _taskName, "/XML", staging, "/F"]);
            return new(result.Success, result.Success ? null : result.Output);
        }
        catch (Exception exception) { return new(false, exception.Message); }
        finally
        {
            if (staging is not null)
            {
                try { File.Delete(staging); }
                catch (Exception exception) { AppLog.Error("Could not remove autostart staging XML.", exception); }
            }
        }
    }

    public AutostartResult Disable()
    {
        var result = _runner.Run(["/Delete", "/TN", _taskName, "/F"]);
        return new(result.Success, result.Success ? null : result.Output);
    }

    private static string? ResolveSid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
                ? new SecurityIdentifier(value).Value
                : ((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value;
        }
        catch (Exception) { return null; }
    }

    private XDocument BuildTaskXml(ExecutablePathLease lease)
    {
        var user = _sid;
        var command = lease.ExecutablePath;
        var workingDirectory = lease.DirectoryPath;

        return new XDocument(
            new XDeclaration("1.0", "utf-16", null),
            new XElement(TaskNamespace + "Task",
                new XAttribute("version", "1.4"),
                new XElement(TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Description", "EasyMonitoring tray monitor"),
                    new XElement(TaskNamespace + "URI", @"\" + _taskName)),
                new XElement(TaskNamespace + "Triggers",
                    new XElement(TaskNamespace + "LogonTrigger",
                        new XElement(TaskNamespace + "Enabled", "true"),
                        new XElement(TaskNamespace + "UserId", user))),
                new XElement(TaskNamespace + "Principals",
                    new XElement(TaskNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskNamespace + "UserId", user),
                        new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
                        new XElement(TaskNamespace + "RunLevel", "HighestAvailable"))),
                new XElement(TaskNamespace + "Settings",
                    new XElement(TaskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(TaskNamespace + "DisallowStartIfOnBatteries", "false"),
                    new XElement(TaskNamespace + "StopIfGoingOnBatteries", "false"),
                    new XElement(TaskNamespace + "AllowHardTerminate", "true"),
                    new XElement(TaskNamespace + "StartWhenAvailable", "false"),
                    new XElement(TaskNamespace + "RunOnlyIfNetworkAvailable", "false"),
                    new XElement(TaskNamespace + "AllowStartOnDemand", "true"),
                    new XElement(TaskNamespace + "Enabled", "true"),
                    new XElement(TaskNamespace + "Hidden", "false"),
                    new XElement(TaskNamespace + "RunOnlyIfIdle", "false"),
                    new XElement(TaskNamespace + "WakeToRun", "false"),
                    new XElement(TaskNamespace + "ExecutionTimeLimit", "PT0S"),
                    new XElement(TaskNamespace + "Priority", "7")),
                new XElement(TaskNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(TaskNamespace + "Exec",
                        new XElement(TaskNamespace + "Command", command),
                        new XElement(TaskNamespace + "WorkingDirectory", workingDirectory)))));
    }

}

internal sealed class ScheduledTaskCommandRunner : IScheduledTaskCommandRunner
{
    public ScheduledTaskCommandResult Run(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return new(null, "Failed to start schtasks.exe.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                return new(null, "schtasks.exe timed out.", TimedOut: true);
            }
            return new(process.ExitCode, string.Join(Environment.NewLine,
                new[] { output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult() }
                    .Where(line => !string.IsNullOrWhiteSpace(line))).Trim());
        }
        catch (Exception exception) { return new(null, exception.Message); }
    }
}
