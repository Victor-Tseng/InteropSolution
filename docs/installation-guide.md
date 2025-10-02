# Installation & Run Guide

This document describes how to build, publish and run the InteropSolution on a clean Windows machine using PowerShell 7 (pwsh) and .NET 8 SDK.

Prerequisites
- Windows with PowerShell 7 (pwsh.exe)
- .NET 8 SDK installed and available in PATH

Build the solution

```powershell
# From repo root (run these commands in your local checkout root)
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
# If you published the proxy into ./publish, set INTEROP_PROXY_PATH relative to repo root
$env:INTEROP_PROXY_PATH = (Resolve-Path .\publish\InteropProxy-win-x86\InteropProxy.exe).Path
dotnet run --project .\Your64BitMainApp\Your64BitMainApp.csproj -c Release
```

Or run the host directly (host will attempt to discover and start the proxy if available):

```powershell
dotnet run --project .\Your64BitMainApp\Your64BitMainApp.csproj -c Release
```

End-to-end smoke test (build-from-source)

If evaluators should build from source and run the smoke-checker instead of downloading an artifact, instruct them to:

```powershell
# From repo root
dotnet build InteropSolution.sln -c Release

# (Optional) publish InteropProxy locally if you want an exe for the host to start
dotnet publish .\InteropProxy\InteropProxy.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o .\publish\InteropProxy-win-x86-self

# Run the E2E checker which will attempt to discover a locally built/published InteropProxy
dotnet run --project .\tests\Interop.E2E\Interop.E2E.csproj -c Release
```

If the E2E checker cannot find a published `InteropProxy.exe`, it will print guidance and exit with a failure code so the evaluator can follow the build steps above.

Notes
- For deployment artifacts, we recommend packaging the published `InteropProxy` directory together with the `Your64BitMainApp` build for distribution.
- If you run into permission issues when using Named Pipes, ensure the service has appropriate access rights.
