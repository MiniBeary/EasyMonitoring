using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.Core.Tests;

public class AutostartControllerTests
{
    [Fact]
    public void IsEnabled_reflects_the_manager()
    {
        var manager = new FakeAutostartManager { Enabled = true };
        var controller = new AutostartController(manager);

        Assert.True(controller.IsEnabled);
    }

    [Fact]
    public void SetEnabled_true_enables_when_disabled()
    {
        var manager = new FakeAutostartManager { Enabled = false };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(true);

        Assert.True(result.Success);
        Assert.Equal(1, manager.EnableCalls);
        Assert.Equal(0, manager.DisableCalls);
        Assert.True(manager.Enabled);
    }

    [Fact]
    public void SetEnabled_true_does_nothing_when_already_enabled()
    {
        var manager = new FakeAutostartManager { Enabled = true };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(true);

        Assert.True(result.Success);
        Assert.Equal(0, manager.EnableCalls);
        Assert.Equal(0, manager.DisableCalls);
    }

    [Fact]
    public void SetEnabled_false_disables_when_enabled()
    {
        var manager = new FakeAutostartManager { Enabled = true };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(false);

        Assert.True(result.Success);
        Assert.Equal(0, manager.EnableCalls);
        Assert.Equal(1, manager.DisableCalls);
        Assert.False(manager.Enabled);
    }

    [Fact]
    public void SetEnabled_false_does_nothing_when_already_disabled()
    {
        var manager = new FakeAutostartManager { Enabled = false };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(false);

        Assert.True(result.Success);
        Assert.Equal(0, manager.EnableCalls);
        Assert.Equal(0, manager.DisableCalls);
    }

    [Fact]
    public void SetEnabled_returns_enable_failure()
    {
        var manager = new FakeAutostartManager
        {
            Enabled = false,
            EnableResult = new AutostartResult(false, "access denied"),
        };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(true);

        Assert.False(result.Success);
        Assert.Equal("access denied", result.Error);
        Assert.False(manager.Enabled);
    }

    [Fact]
    public void SetEnabled_returns_disable_failure()
    {
        var manager = new FakeAutostartManager
        {
            Enabled = true,
            DisableResult = new AutostartResult(false, "task busy"),
        };
        var controller = new AutostartController(manager);

        var result = controller.SetEnabled(false);

        Assert.False(result.Success);
        Assert.Equal("task busy", result.Error);
        Assert.True(manager.Enabled);
    }

    private sealed class FakeAutostartManager : IAutostartManager
    {
        public bool Enabled { get; set; }

        public AutostartResult EnableResult { get; set; } = new(true);

        public AutostartResult DisableResult { get; set; } = new(true);

        public int EnableCalls { get; private set; }

        public int DisableCalls { get; private set; }

        public bool IsEnabled() => Enabled;

        public AutostartResult Enable()
        {
            EnableCalls++;

            if (EnableResult.Success)
            {
                Enabled = true;
            }

            return EnableResult;
        }

        public AutostartResult Disable()
        {
            DisableCalls++;

            if (DisableResult.Success)
            {
                Enabled = false;
            }

            return DisableResult;
        }
    }
}
