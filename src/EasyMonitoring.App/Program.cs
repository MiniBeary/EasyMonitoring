using EasyMonitoring.App.Logging;
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
            if (context.AppliedSnapshotCount == 0 && DateTime.UtcNow < deadline)
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

            File.WriteAllLines(Path.Combine(outputDirectory, "ui-diagnostics.txt"), lines);
            context.ExitForDiagnostics();
        };

        timer.Start();
        Application.Run(context);

        return snapshotReceived ? 0 : 1;
    }
}
