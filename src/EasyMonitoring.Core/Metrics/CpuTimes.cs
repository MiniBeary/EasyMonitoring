namespace EasyMonitoring.Core.Metrics;

public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
