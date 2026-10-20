# Third-party notices

EasyMonitoring itself is licensed under the [MIT License](LICENSE). Published self-contained builds include third-party components listed below; those components remain under their own licenses.

## LibreHardwareMonitorLib and related packages

| Package | Version | License | Source |
| --- | --- | --- | --- |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | [BlackSharp](https://github.com/Blacktempel/BlackSharp) |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | [DiskInfoToolkit](https://github.com/Blacktempel/DiskInfoToolkit) |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | [RAMSPDToolkit](https://github.com/Blacktempel/RAMSPDToolkit) |
| HidSharp | 2.6.4 | See package license | [HidSharp](https://software.seekye.com/hidsharp/) |

The full text of the Mozilla Public License 2.0 is included in [docs/licenses/MPL-2.0.txt](docs/licenses/MPL-2.0.txt). The LibreHardwareMonitorLib source corresponding to version 0.9.6 is available from the upstream repository and the NuGet package metadata.

## .NET 9 runtime and Microsoft packages

Self-contained builds include Microsoft .NET 9 runtime components and packages such as `System.Management`, `System.IO.Ports`, `System.CodeDom`, `System.Threading.AccessControl`, and runtime native assets. These components are distributed under the MIT license and their respective third-party notices.

- [.NET license](docs/licenses/dotnet-LICENSE.txt)
- [.NET third-party notices](docs/licenses/dotnet-ThirdPartyNotices.txt)

## PawnIO

[PawnIO](https://github.com/namazso/PawnIO) is a separate prerequisite installed by the user. It is **not** bundled with EasyMonitoring. PawnIO is distributed under GPL-2.0 by its authors; see its repository for the complete license and source.

## Other NuGet dependencies

The published application also includes `Mono.Posix.NETStandard` and other transitive NuGet packages. Their license files are available in the corresponding NuGet packages and upstream repositories. Use `dotnet list package --include-transitive` to inspect the resolved dependency graph for a given build.
