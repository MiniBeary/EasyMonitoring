using System.Runtime.ExceptionServices;
using EasyMonitoring.App.Tray;
using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App.Tests;

public class TrayPresentationTests
{
    [Fact]
    public void Presentation_applies_snapshot_and_preserves_diagnostics_keys()
    {
        RunOnSta(() =>
        {
            using var presentation = new TrayPresentation(showTrayIcon: false);

            presentation.ApplySnapshot(new MetricsSnapshot(23, 58, 5, 47, 8.2, 31.9, 26));

            var diagnostics = presentation.GetDiagnostics();

            Assert.Equal(1, diagnostics.Snapshots);
            Assert.Equal("CPU: 23% | 58 °C", diagnostics.TrayText);
            Assert.Equal(
                new[]
                {
                    "CPU: 23% | 58 °C",
                    "GPU: 5% | 47 °C",
                    "RAM: 8.2 / 31.9 GB (26%)",
                    "Start with Windows",
                    "Exit",
                },
                diagnostics.MenuTexts);
            Assert.Equal(397, diagnostics.AnimationIntervalMs);
            Assert.Equal(5, diagnostics.AnimationFrameCount);
            Assert.Equal("FF3C3C3C", diagnostics.MenuBackColor.ToArgb().ToString("X8"));
            Assert.Equal("FFFFFFFF", diagnostics.MenuForeColor.ToArgb().ToString("X8"));
            Assert.True(diagnostics.ShowCheckMargin);
        });
    }

    [Fact]
    public void Presentation_applies_autostart_unknown_with_fallback()
    {
        RunOnSta(() =>
        {
            using var presentation = new TrayPresentation(showTrayIcon: false);
            var state = new AutostartState(null, DateTimeOffset.UtcNow, "probe failed");

            presentation.ApplyAutostart(state, fallbackChecked: true);

            var diagnostics = presentation.GetDiagnostics();

            Assert.True(diagnostics.AutostartChecked);
            Assert.True(diagnostics.AutostartStateLoaded);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
