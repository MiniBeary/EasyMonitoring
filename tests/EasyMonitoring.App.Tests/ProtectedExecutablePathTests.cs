using System.Security.AccessControl;
using EasyMonitoring.App.Autostart;
using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.App.Tests;

public class ProtectedExecutablePathTests
{
    [Theory]
    [InlineData("O:BAG:BAD:(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;BU)", "Executable", ExecutableProtection.Safe)]
    [InlineData("O:BAG:BAD:(A;;FA;;;BU)", "Executable", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;GW;;;BU)", "Executable", ExecutableProtection.Unsafe)]
    [InlineData("O:BUG:BAD:(A;;GRGX;;;BU)", "Executable", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;0x2;;;BU)", "ApplicationDirectory", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;0x40;;;BU)", "AncestorDirectory", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;0x40000;;;BU)", "AncestorDirectory", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;0x80000;;;BU)", "AncestorDirectory", ExecutableProtection.Unsafe)]
    [InlineData("O:BAG:BAD:(A;;0x4;;;AU)(A;;GRGX;;;BU)", "AncestorDirectory", ExecutableProtection.Safe)]
    [InlineData("O:BAG:BAD:(A;CIIO;GA;;;BU)(A;;GRGX;;;BU)", "ApplicationDirectory", ExecutableProtection.Safe)]
    public void Policy_rejects_replacement_rights_but_allows_readers_and_harmless_root_creation(
        string sddl, string part, ExecutableProtection expected)
    {
        var result = ExecutableAccessPolicy.Evaluate(new RawSecurityDescriptor(sddl), Enum.Parse<ExecutablePathPart>(part));
        Assert.Equal(expected, result.Protection);
    }

    [Fact]
    public void Null_dacl_cannot_be_certified_safe()
    {
        var descriptor = new RawSecurityDescriptor("O:BAG:BAD:NO_ACCESS_CONTROL");
        Assert.NotEqual(ExecutableProtection.Safe,
            ExecutableAccessPolicy.Evaluate(descriptor, ExecutablePathPart.Executable).Protection);
    }

    [Fact]
    public void Conflicting_deny_and_allow_is_unknown_rather_than_proven_unsafe_for_legacy_cleanup()
    {
        var descriptor = new RawSecurityDescriptor("O:BAG:BAD:(D;;GW;;;BU)(A;;GW;;;BU)");
        Assert.Equal(ExecutableProtection.Unknown,
            ExecutableAccessPolicy.Evaluate(descriptor, ExecutablePathPart.Executable).Protection);
    }

    [Fact]
    public void Restricted_owner_rights_are_not_misreported_as_proven_owner_write_access()
    {
        var descriptor = new RawSecurityDescriptor("O:BUG:BAD:(D;;0x40000;;;OW)(A;;GRGX;;;BU)");
        Assert.Equal(ExecutableProtection.Unknown,
            ExecutableAccessPolicy.Evaluate(descriptor, ExecutablePathPart.Executable).Protection);
    }

    [Theory]
    [InlineData(@"\\server\share\EasyMonitoring.exe")]
    [InlineData(@"C:\Tools\..\EasyMonitoring.exe")]
    [InlineData(@"C:\Tools\file.exe:stream")]
    [InlineData(@"C:\%TEMP%\EasyMonitoring.exe")]
    [InlineData(@"C:\Tools.\EasyMonitoring.exe")]
    [InlineData("relative.exe")]
    public void Ambiguous_or_redirectable_names_are_refused(string path)
    {
        using var lease = new ProtectedExecutablePath().Inspect(path);
        Assert.NotEqual(ExecutableProtection.Safe, lease.Protection);
        Assert.NotNull(lease.Error);
    }

    [Fact]
    public void Real_protected_windows_executable_has_a_stable_volume_path()
    {
        using var lease = new ProtectedExecutablePath().Inspect(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
        Assert.True(lease.Protection == ExecutableProtection.Safe, lease.Error);
        Assert.StartsWith(@"\\?\Volume{", lease.CanonicalVolumePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(lease.ExecutablePath.StartsWith(@"\\?\", StringComparison.Ordinal));
        using var again = new ProtectedExecutablePath().Inspect(lease.ExecutablePath);
        Assert.Equal(ExecutableProtection.Safe, again.Protection);
        Assert.Equal(lease.CanonicalVolumePath, again.CanonicalVolumePath);
    }

    [Fact]
    public void Current_user_owned_test_output_is_not_an_elevated_autostart_location()
    {
        using var lease = new ProtectedExecutablePath().Inspect(typeof(ProtectedExecutablePathTests).Assembly.Location);
        Assert.Equal(ExecutableProtection.Unsafe, lease.Protection);
    }
}
