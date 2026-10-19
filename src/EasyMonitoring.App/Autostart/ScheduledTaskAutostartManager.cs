using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.App.Autostart;

internal sealed class ScheduledTaskAutostartManager : IAutostartManager
{
    private const string TaskName = "EasyMonitoring";
    private const int CommandTimeoutMs = 15000;
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public bool IsEnabled()
    {
        var query = RunSchtasks(["/Query", "/TN", TaskName, "/XML"]);
        if (!query.Success)
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(query.Output);

            var settingsEnabled = document
                .Descendants(TaskNamespace + "Settings")
                .Descendants(TaskNamespace + "Enabled")
                .FirstOrDefault()?.Value;

            if (string.Equals(settingsEnabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var command = document
                .Descendants(TaskNamespace + "Command")
                .FirstOrDefault()?.Value;

            var principal = document
                .Descendants(TaskNamespace + "Principal")
                .FirstOrDefault();

            var logonType = principal?.Element(TaskNamespace + "LogonType")?.Value;
            var runLevel = principal?.Element(TaskNamespace + "RunLevel")?.Value;

            var hasLogonTrigger = document
                .Descendants(TaskNamespace + "LogonTrigger")
                .Any(trigger => !string.Equals(
                    trigger.Element(TaskNamespace + "Enabled")?.Value,
                    "false",
                    StringComparison.OrdinalIgnoreCase));

            return !string.IsNullOrWhiteSpace(command) &&
                   string.Equals(command, CurrentExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                   hasLogonTrigger &&
                   string.Equals(logonType, "InteractiveToken", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal string? TryGetTaskXml()
    {
        var query = RunSchtasks(["/Query", "/TN", TaskName, "/XML"]);
        return query.Success ? query.Output : null;
    }

    public AutostartResult Enable()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"EasyMonitoring-autostart-{Guid.NewGuid():N}.xml");

        try
        {
            File.WriteAllText(tempPath, BuildTaskXml().ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));

            var create = RunSchtasks(["/Create", "/TN", TaskName, "/XML", tempPath, "/F"]);
            if (!create.Success)
            {
                return new AutostartResult(false, create.Output);
            }

            return IsEnabled()
                ? new AutostartResult(true)
                : new AutostartResult(false, "Task was created, but the state check failed.");
        }
        catch (Exception exception)
        {
            return new AutostartResult(false, exception.Message);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception)
            {
            }
        }
    }

    public AutostartResult Disable()
    {
        var delete = RunSchtasks(["/Delete", "/TN", TaskName, "/F"]);

        if (delete.Success || !IsEnabled())
        {
            return new AutostartResult(true);
        }

        return new AutostartResult(false, delete.Output);
    }

    private static string CurrentExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return Path.Combine(AppContext.BaseDirectory, "EasyMonitoring.exe");
        }
    }

    private static XDocument BuildTaskXml()
    {
        var user = WindowsIdentity.GetCurrent().Name;
        var command = CurrentExecutablePath;
        var workingDirectory = Path.GetDirectoryName(command) ?? AppContext.BaseDirectory;

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(TaskNamespace + "Task",
                new XAttribute("version", "1.4"),
                new XElement(TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Description", "EasyMonitoring tray monitor"),
                    new XElement(TaskNamespace + "URI", @"\EasyMonitoring")),
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

    private static (bool Success, string Output) RunSchtasks(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (false, "Failed to start schtasks.exe.");
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(CommandTimeoutMs))
            {
                process.Kill(entireProcessTree: true);
                return (false, "schtasks.exe timed out.");
            }

            var output = string.Join(
                Environment.NewLine,
                new[] { standardOutput, standardError }
                    .Where(line => !string.IsNullOrWhiteSpace(line)))
                .Trim();

            return (process.ExitCode == 0, output);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }
}
