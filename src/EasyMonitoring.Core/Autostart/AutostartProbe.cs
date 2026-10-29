namespace EasyMonitoring.Core.Autostart;

public enum TaskPresence { Unknown, Missing, Present }
public enum TaskOwnership { Unknown, CurrentUser, OtherUser }
public enum ExecutableProtection { Unknown, Safe, Unsafe }

public sealed record AutostartTaskHealth(
    bool HasRunLevel,
    bool HasInteractiveToken,
    bool HasLogonTrigger);

public sealed record AutostartProbe(
    bool IsKnown,
    bool Enabled,
    AutostartTaskHealth? Health = null,
    string? Error = null,
    TaskPresence Presence = TaskPresence.Unknown,
    TaskOwnership Ownership = TaskOwnership.Unknown,
    ExecutableProtection Protection = ExecutableProtection.Unknown);
