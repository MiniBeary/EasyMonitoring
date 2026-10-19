using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EasyMonitoring.App.Native;

internal readonly record struct MemoryStatus(
    double UsedGb,
    double TotalGb,
    double UsedPercent);

internal static class GlobalMemoryStatusEx
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusExNative(ref MemoryStatusEx buffer);

    public static MemoryStatus Read()
    {
        var buffer = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>(),
        };

        if (!GlobalMemoryStatusExNative(ref buffer))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var totalGb = buffer.TotalPhysical / BytesPerGigabyte;
        var availableGb = buffer.AvailablePhysical / BytesPerGigabyte;

        return new MemoryStatus(
            UsedGb: totalGb - availableGb,
            TotalGb: totalGb,
            UsedPercent: buffer.MemoryLoad);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
