# Interop Integration Sample

This sample demonstrates a clean approach to integrate a 32-bit .NET assembly into a 64-bit .NET application using Inter-Process Communication (IPC) via named pipes with a proxy pattern to make calls look like local class library invocations.

## Approach Explanation

The chosen approach uses IPC to avoid platform restrictions. The 32-bit component runs in its own process, and the 64-bit host communicates with it via named pipes. A proxy class implements the same interface as the 32-bit library, making remote calls appear as local method invocations.

Original configuration likely: Entire application was 32-bit, but the main app needs to be 64-bit for better performance/memory usage, while some components must remain 32-bit due to dependencies.

## Build Instructions

1. Ensure .NET 8.0 SDK is installed.
2. Open the solution in Visual Studio or use CLI: `dotnet build InteropSolution.sln`

## Run Instructions

1. Build the solution.
2. Run the host: `dotnet run --project Your64BitMainApp`

The host will automatically discover and start the 32-bit proxy service, perform operations using the proxy (which looks like local calls), and display results.

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