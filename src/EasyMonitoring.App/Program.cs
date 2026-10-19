using EasyMonitoring.App.Autostart;
using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Formatting;

namespace EasyMonitoring.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\EasyMonitoring.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunDiagnosticsAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--ui-check", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunUiCheck();
            return;
        }

        if (args.Contains("--autostart-check", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunAutostartCheck();
            return;
        }

        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var createdNew);

        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
            AppLog.Error("Unhandled UI exception.", eventArgs.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Error("Unhandled exception.", eventArgs.ExceptionObject as Exception);

        using var context = new TrayApplicationContext();
        Application.Run(context);
    }

    private static async Task<int> RunDiagnosticsAsync()
    {
        using var sampler = new MetricsSampler();
        var firstSnapshot = new TaskCompletionSource<EasyMonitoring.Core.Metrics.MetricsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        sampler.SnapshotReady += snapshot => firstSnapshot.TrySetResult(snapshot);
        sampler.Start();

        EasyMonitoring.Core.Metrics.MetricsSnapshot? result;

        try
        {
            result = await firstSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            AppLog.Error("Diagnostics timed out waiting for the first metrics snapshot.");
            return 1;
        }

        var lines = new[]
        {
            "EasyMonitoring diagnostics",
            StatusFormatter.FormatCpu(result.CpuLoadPercent, result.CpuTemperatureC),
            StatusFormatter.FormatGpu(result.GpuLoadPercent, result.GpuTemperatureC),
            StatusFormatter.FormatRam(result.RamUsedGb, result.RamTotalGb, result.RamUsedPercent),
        };

        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyMonitoring");

        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "diagnostics.txt"), lines);

        return 0;
    }

    private static int RunUiCheck()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        using var context = new TrayApplicationContext(showTrayIcon: false);
        using var timer = new System.Windows.Forms.Timer
        {
            Interval = 250,
        };

        var deadline = DateTime.UtcNow.AddSeconds(15);
        var snapshotReceived = false;

        timer.Tick += (_, _) =>
        {
            if ((context.AppliedSnapshotCount == 0 || !context.AutostartStateLoaded) &&
                DateTime.UtcNow < deadline)
            {
                return;
            }

            snapshotReceived = context.AppliedSnapshotCount > 0;
            timer.Stop();

            var outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EasyMonitoring");
            Directory.CreateDirectory(outputDirectory);

            var lines = new List<string>
            {
                "EasyMonitoring UI diagnostics",
                $"Snapshots: {context.AppliedSnapshotCount}",
                $"TrayText: {context.TrayText}",
            };
            lines.AddRange(context.MenuTexts.Select((text, index) => $"Menu[{index}]: {text}"));
            lines.Add($"AnimationIntervalMs: {context.AnimationIntervalMs}");
            lines.Add($"AnimationFrameCount: {context.AnimationFrameCount}");
            lines.Add($"CurrentFrameIndex: {context.CurrentFrameIndex}");
            lines.Add($"AutostartChecked: {context.AutostartChecked}");
            lines.Add($"AutostartStateLoaded: {context.AutostartStateLoaded}");
            lines.Add($"MenuBackColor: {context.MenuBackColor.ToArgb():X8}");
            lines.Add($"MenuForeColor: {context.MenuForeColor.ToArgb():X8}");
            lines.Add($"ShowCheckMargin: {context.ShowCheckMargin}");

            File.WriteAllLines(Path.Combine(outputDirectory, "ui-diagnostics.txt"), lines);
            context.ExitForDiagnostics();
        };

        timer.Start();
        Application.Run(context);

        return snapshotReceived ? 0 : 1;
    }

    private static int RunAutostartCheck()
    {
        var manager = new ScheduledTaskAutostartManager();
        var controller = new AutostartController(manager);

        var lines = new List<string>
        {
            "EasyMonitoring autostart diagnostics",
        };

        var initial = controller.IsEnabled;
        lines.Add($"Initial: {initial}");

        var success = false;
        var restored = true;

        try
        {
            var enable = controller.SetEnabled(true);
            var enabled = controller.IsEnabled;
            lines.Add($"Enable: success={enable.Success} error={enable.Error ?? "none"}");
            lines.Add($"EnabledState: {enabled}");

            var taskXml = manager.TryGetTaskXml();
            var hasRunLevel = taskXml?.Contains("HighestAvailable", StringComparison.OrdinalIgnoreCase) == true;
            var hasInteractiveToken = taskXml?.Contains("InteractiveToken", StringComparison.OrdinalIgnoreCase) == true;
            var hasLogonTrigger = taskXml?.Contains("LogonTrigger", StringComparison.OrdinalIgnoreCase) == true;
            lines.Add($"TaskXmlRunLevel: {hasRunLevel}");
            lines.Add($"TaskXmlInteractiveToken: {hasInteractiveToken}");
            lines.Add($"TaskXmlLogonTrigger: {hasLogonTrigger}");

            var disable = controller.SetEnabled(false);
            var disabled = !controller.IsEnabled;
            lines.Add($"Disable: success={disable.Success} error={disable.Error ?? "none"}");
            lines.Add($"DisabledState: {disabled}");

            success = enable.Success && enabled && hasRunLevel && hasInteractiveToken &&
                      hasLogonTrigger && disable.Success && disabled;
        }
        catch (Exception exception)
        {
            AppLog.Error("Autostart diagnostics failed.", exception);
            lines.Add($"Error: {exception.Message}");
        }
        finally
        {
            try
            {
                if (initial && !controller.IsEnabled)
                {
                    var restore = controller.SetEnabled(true);
                    restored = restore.Success && controller.IsEnabled;
                    lines.Add($"Restore: success={restore.Success} error={restore.Error ?? "none"}");
                }
            }
            catch (Exception exception)
            {
                restored = false;
                AppLog.Error("Failed to restore the initial autostart state.", exception);
                lines.Add($"Restore: error={exception.Message}");
            }
        }

        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyMonitoring");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllLines(Path.Combine(outputDirectory, "autostart-diagnostics.txt"), lines);

        return success && restored ? 0 : 1;
    }
}
