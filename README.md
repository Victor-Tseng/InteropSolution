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