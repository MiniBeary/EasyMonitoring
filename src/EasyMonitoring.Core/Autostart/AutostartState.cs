namespace EasyMonitoring.Core.Autostart;

public sealed record AutostartState(
    bool? Enabled,
    DateTimeOffset LastCheckedAt,
    string? LastError = null,
    AutostartTaskHealth? Health = null)
{
    public bool IsKnown => Enabled is not null;

    public bool IsEnabled => Enabled == true;
}
