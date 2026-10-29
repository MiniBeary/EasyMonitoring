using System.Security.AccessControl;
using System.Security.Principal;
using EasyMonitoring.App.Native;
using EasyMonitoring.Core.Autostart;
using Microsoft.Win32.SafeHandles;

namespace EasyMonitoring.App.Autostart;

internal enum ExecutablePathPart { Executable, ApplicationDirectory, AncestorDirectory }
internal sealed record ExecutableAccessDecision(ExecutableProtection Protection, string? Error = null);

internal static class ExecutableAccessPolicy
{
    internal const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    private static bool Trusted(SecurityIdentifier? sid) => sid?.Value is
        "S-1-5-18" or "S-1-5-32-544" or TrustedInstallerSid;

    public static ExecutableAccessDecision Evaluate(RawSecurityDescriptor descriptor, ExecutablePathPart part)
    {
        if (descriptor.DiscretionaryAcl is null)
            return new(ExecutableProtection.Unknown, "A null or missing DACL cannot protect elevated autostart.");

        var applicable = new List<CommonAce>();
        foreach (GenericAce ace in descriptor.DiscretionaryAcl)
        {
            if ((ace.AceFlags & AceFlags.InheritOnly) != 0) continue;
            if (ace is not CommonAce common || common.IsCallback ||
                common.AceQualifier is not (AceQualifier.AccessAllowed or AceQualifier.AccessDenied))
                return new(ExecutableProtection.Unknown, "Unsupported access-control entry.");
            applicable.Add(common);
        }
        if (!Trusted(descriptor.Owner))
            return applicable.Any(ace => ace.SecurityIdentifier.Value == "S-1-3-4")
                ? new(ExecutableProtection.Unknown, "Owner-rights entries require effective-access analysis.")
                : new(ExecutableProtection.Unsafe, "Owner can change permissions and is not SYSTEM, Administrators or TrustedInstaller.");
        var hasDeny = applicable.Any(ace => ace.AceQualifier == AceQualifier.AccessDenied);

        var dangerous = (uint)(FileSystemRights.Delete | FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes);
        if (part != ExecutablePathPart.Executable)
            dangerous |= (uint)FileSystemRights.DeleteSubdirectoriesAndFiles;
        if (part != ExecutablePathPart.AncestorDirectory)
            dangerous |= (uint)FileSystemRights.Write;

        foreach (var common in applicable)
        {
            if (common.AceQualifier == AceQualifier.AccessDenied) continue;

            var mask = unchecked((uint)common.AccessMask);
            if ((mask & 0x10000000) != 0) mask |= 0x001f01ff; // GENERIC_ALL
            if ((mask & 0x40000000) != 0) mask |= 0x00120116; // GENERIC_WRITE
            if ((mask & 0x80000000) != 0) mask |= 0x00120089; // GENERIC_READ
            if ((mask & 0x20000000) != 0) mask |= 0x001200a0; // GENERIC_EXECUTE
            if ((mask & ~0xf01f01ffu) != 0)
                return new(ExecutableProtection.Unknown, "Unsupported access mask.");
            if (!Trusted(common.SecurityIdentifier) && (mask & dangerous) != 0)
                return hasDeny || common.SecurityIdentifier.Value.StartsWith("S-1-3-", StringComparison.Ordinal)
                    ? new(ExecutableProtection.Unknown, "Conflicting or special-principal permissions cannot be certified safe.")
                    : new(ExecutableProtection.Unsafe, $"Untrusted SID {common.SecurityIdentifier.Value} can modify or replace this path.");
        }
        return new(ExecutableProtection.Safe);
    }
}

internal interface IExecutablePathGuard
{
    ExecutablePathLease Inspect(string path);
}

internal sealed class ExecutablePathLease : IDisposable
{
    private IReadOnlyList<SafeFileHandle>? _handles;
    public ExecutableProtection Protection { get; }
    public string? Error { get; }
    public string ExecutablePath { get; }
    public string DirectoryPath { get; }
    public string StagingDirectoryPath { get; }
    public string CanonicalVolumePath { get; }

    internal ExecutablePathLease(ExecutableProtection protection, string path, string? error = null,
        IReadOnlyList<SafeFileHandle>? handles = null, string? canonicalVolumePath = null)
    {
        Protection = protection;
        Error = error;
        ExecutablePath = path;
        DirectoryPath = Path.GetDirectoryName(path) ?? string.Empty;
        CanonicalVolumePath = canonicalVolumePath ?? path;
        StagingDirectoryPath = Path.GetDirectoryName(CanonicalVolumePath) ?? string.Empty;
        _handles = handles;
    }

    public void Dispose()
    {
        var handles = Interlocked.Exchange(ref _handles, null);
        if (handles is null) return;
        for (var i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
    }
}

internal sealed class ProtectedExecutablePath : IExecutablePathGuard
{
    public ExecutablePathLease Inspect(string path)
    {
        var handles = new List<SafeFileHandle>();
        try
        {
            var (root, names) = SplitPath(path);
            if (root.Length == 3) ExecutableFileSecurity.RequirePhysicalDrive(root[..2]);
            var rootHandle = ExecutableFileSecurity.Open(root, directory: true);
            handles.Add(rootHandle);
            var canonicalRoot = ExecutableFileSecurity.FinalPath(rootHandle);
            ExecutableFileSecurity.RequireLocalAclVolume(canonicalRoot);

            var current = canonicalRoot;
            for (var i = 0; i <= names.Length; i++)
            {
                var isFile = i == names.Length;
                var handle = rootHandle;
                if (i > 0)
                {
                    current = Path.Combine(current, names[i - 1]);
                    handle = ExecutableFileSecurity.Open(current, directory: !isFile);
                    handles.Add(handle);
                }

                var attributes = ExecutableFileSecurity.Attributes(handle);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new UnverifiedExecutablePathException($"Reparse points are not supported: {current}");
                if (((attributes & FileAttributes.Directory) != 0) == isFile)
                    throw new UnverifiedExecutablePathException($"Unexpected filesystem object: {current}");

                var part = isFile ? ExecutablePathPart.Executable :
                    i == names.Length - 1 ? ExecutablePathPart.ApplicationDirectory : ExecutablePathPart.AncestorDirectory;
                var decision = ExecutableAccessPolicy.Evaluate(ExecutableFileSecurity.Descriptor(handle), part);
                if (decision.Protection != ExecutableProtection.Safe)
                {
                    DisposeHandles(handles);
                    return new(decision.Protection, path, $"{current}: {decision.Error}");
                }
            }

            return new(ExecutableProtection.Safe, ExecutableFileSecurity.SchedulingPath(handles[^1], rootHandle),
                handles: handles, canonicalVolumePath: ExecutableFileSecurity.FinalPath(handles[^1]));
        }
        catch (UnverifiedExecutablePathException exception)
        {
            DisposeHandles(handles);
            return new(ExecutableProtection.Unknown, path, exception.Message);
        }
        catch (Exception exception)
        {
            DisposeHandles(handles);
            return new(ExecutableProtection.Unknown, path, $"Cannot verify executable protection: {exception.Message}");
        }
    }

    private static (string Root, string[] Names) SplitPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.Contains('%') || path.Contains('/'))
            throw new UnverifiedExecutablePathException("An unambiguous absolute Windows path is required.");
        string root;
        if (path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            var end = path.IndexOf('}');
            if (end < 0 || end + 1 >= path.Length || path[end + 1] != '\\' ||
                !Guid.TryParseExact(path[11..end], "D", out _))
                throw new UnverifiedExecutablePathException("Invalid volume path.");
            root = path[..(end + 2)];
        }
        else if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\')
            root = path[..3];
        else
            throw new UnverifiedExecutablePathException("Network, relative and device paths are not supported.");

        var names = path[root.Length..].Split('\\');
        if (names.Any(name => name.Length == 0 || name is "." or ".." || name.EndsWith('.') ||
            name.EndsWith(' ') || name.IndexOfAny([':', '"', '<', '>', '|', '?', '*', '\0']) >= 0))
            throw new UnverifiedExecutablePathException("Ambiguous filename or alternate stream.");
        return (root, names);
    }

    private static void DisposeHandles(List<SafeFileHandle> handles)
    {
        for (var i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
    }
}

internal sealed class UnverifiedExecutablePathException(string message) : Exception(message);
