namespace EasyMonitoring.Core.Autostart;

public sealed record AutostartTaskHealth(
    bool HasRunLevel,
    bool HasInteractiveToken,
    bool HasLogonTrigger);

public sealed record AutostartProbe(
    bool IsKnown,
    bool Enabled,
    AutostartTaskHealth? Health = null,
    string? Error = null);
