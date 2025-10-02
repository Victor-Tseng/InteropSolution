# Installation & Run Guide

This document describes how to build, publish and run the InteropSolution on a clean Windows machine using PowerShell 7 (pwsh) and .NET 8 SDK.

Prerequisites
- Windows with PowerShell 7 (pwsh.exe)
- .NET 8 SDK installed and available in PATH

Build the solution

```powershell
cd 'D:\codex\bitness\InteropSolution'
dotnet restore InteropSolution.sln
dotnet build InteropSolution.sln -c Release
```

Publish the x86 proxy (recommended for distribution)

Publish as framework-dependent (requires .NET runtime on target):

```powershell
dotnet publish .\InteropProxy\InteropProxy.csproj -c Release -r win-x86 --self-contained false -o .\publish\InteropProxy-win-x86
```

Publish as self-contained single-file (no runtime dependency):

```powershell
dotnet publish .\InteropProxy\InteropProxy.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o .\publish\InteropProxy-win-x86-self
```

Run the 64-bit host

If you published the proxy and want the host to use the published exe, set the environment variable first:

```powershell
$env:INTEROP_PROXY_PATH = 'D:\codex\bitness\InteropSolution\publish\InteropProxy-win-x86\InteropProxy.exe'
dotnet run --project .\Your64BitMainApp\Your64BitMainApp.csproj -c Release
```

Or run the host directly (host will attempt to discover and start the proxy if available):

```powershell
dotnet run --project .\Your64BitMainApp\Your64BitMainApp.csproj -c Release
```

Notes
- For deployment artifacts, we recommend packaging the published `InteropProxy` directory together with the `Your64BitMainApp` build for distribution.
- If you run into permission issues when using Named Pipes, ensure the service has appropriate access rights.
