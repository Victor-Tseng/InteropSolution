# Interop Integration Sample

This sample demonstrates a clean approach to integrate a 32-bit .NET assembly into a 64-bit .NET application using Inter-Process Communication (IPC) via named pipes with a proxy pattern to make calls look like local class library invocations.

## Approach Explanation

The chosen approach uses IPC to avoid platform restrictions. The 32-bit component runs in its own process, and the 64-bit host communicates with it via named pipes. A proxy class implements the same interface as the 32-bit library, making remote calls appear as local method invocations.

Original configuration likely: Entire application was 32-bit, but the main app needs to be 64-bit for better performance/memory usage, while some components must remain 32-bit due to dependencies.

## Installation & Run Guide

This section describes how to build, publish and run the InteropSolution on a clean Windows machine using PowerShell 7 (pwsh) and .NET 8 SDK.

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

## Maintenance Notes

- The proxy class (`CalculatorProxy`) implements the same interface (`ICalculator`) as the real library.
- IPC is handled transparently within the proxy methods, with automatic discovery of the proxy executable.
- The proxy automatically starts the 32-bit service if not already running, using named pipe "InteropPipe" for communication.
- Extend by adding more methods to the interface and implementing them in both the library and proxy.
- For production, consider using WCF or gRPC for more robust IPC with better error handling and type safety.

## Async-first API design

- This project favors an async-first API: `AddAsync` and `GetPlatformInfoAsync` are the primary methods callers should use.
- The previous synchronous wrappers (e.g. `Add`, `GetPlatformInfo`) have been intentionally disabled in the proxy to avoid unsafe sync-over-async patterns that can cause deadlocks in certain environments (UI threads, ASP.NET).
- If your code must call these APIs synchronously, update your caller to run the async call on a background thread and block there, for example:

```csharp
// Not recommended in UI/ASP.NET contexts
var result = Task.Run(() => calculator.AddAsync(1, 2)).GetAwaiter().GetResult();
```

- Recommended: prefer `await calculator.AddAsync(...)` and propagate async through your call chain. This approach is safer and scales better under concurrency.

- If you rely on synchronous APIs for compatibility reasons, contact the project maintainers to discuss a compatibility layer or separate sync-compat package.

## Using StreamJsonRpc

This solution uses StreamJsonRpc to implement the IPC channel between the 64-bit host and the 32-bit proxy process. StreamJsonRpc is a lightweight JSON-RPC implementation that works well over stream transports such as named pipes.

How it is used in this project:
- The 32-bit `InteropProxy` process hosts a JSON-RPC endpoint bound to a `NamedPipeServerStream` and exposes an implementation of `ICalculator` as RPC methods.
- The 64-bit `CalculatorProxy` client connects to the named pipe using `NamedPipeClientStream` and attaches a `JsonRpc` instance to the stream. The client invokes RPC methods which are marshalled as JSON and handled by the proxy.

Minimal plumbing notes:
- Server side (32-bit proxy): create a `NamedPipeServerStream`, instantiate your service object that implements the contract, then call `JsonRpc.Attach(stream, service)` to host the methods.
- Client side (64-bit host): create a `NamedPipeClientStream` and call `JsonRpc.Attach(stream)` to obtain a `JsonRpc` instance. Use `InvokeAsync<T>(methodName, args)` for remote calls.

Example (conceptual):

Server side (inside `InteropProxy`):

```csharp
using (var server = new NamedPipeServerStream("InteropPipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
{
	await server.WaitForConnectionAsync();
	var service = new CalculatorService(...);
	using var jsonRpc = JsonRpc.Attach(server, service);
	await jsonRpc.Completion;
}
```

Client side (inside `CalculatorProxy`):

```csharp
var client = new NamedPipeClientStream(".", "InteropPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
await client.ConnectAsync();
var jsonRpc = JsonRpc.Attach(client);
var result = await jsonRpc.InvokeAsync<int>("AddAsync", 1, 2);
```

Security and operational notes
- Named pipes are local to the machine; ensure pipe names and permissions are controlled to prevent unauthorized access on multi-user machines.
- Validate and sanitize inputs where appropriate — JSON-RPC will deserialize arguments into your method parameters.
- Consider adding authentication/authorization on top of the RPC channel for production systems (for example, a simple shared secret handshake before accepting commands, or using OS-level access controls).

Troubleshooting
- If the client cannot connect, verify the proxy process is running and listening on the expected pipe name (`InteropPipe`).
- Use logs on both sides to capture connection attempts, exceptions, and method-level errors to aid debugging.