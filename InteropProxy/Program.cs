#nullable enable
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using StreamJsonRpc;
using Interop.Contracts;
using Your32BitLibrary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[SupportedOSPlatform("windows")]

internal static class Program
{
    private const string PipeName = "InteropPipe";

    static async Task Main(string[] args)
    {
        using var lifetimeCts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            lifetimeCts.Cancel();
        };

        // Build a DI container so the host can optionally resolve dependencies for Calculator.
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "hh:mm:ss ";
        }));

        // Note: We intentionally do NOT register ICalculator by default so the host will
        // fall back to the parameterless Calculator if the consumer hasn't provided a registration.
        var serviceProvider = services.BuildServiceProvider();

        using var host = new NamedPipeCalculatorHost(PipeName, lifetimeCts, serviceProvider);
        await host.RunAsync().ConfigureAwait(false);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class NamedPipeCalculatorHost : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly IServiceProvider _serviceProvider;

    public NamedPipeCalculatorHost(string pipeName, CancellationTokenSource lifetimeCts, IServiceProvider serviceProvider)
    {
        _pipeName = pipeName;
        _lifetimeCts = lifetimeCts;
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync()
    {
        while (!_lifetimeCts.IsCancellationRequested)
        {
            using var server = CreateServerStream();
            try
            {
                await Console.Out.WriteLineAsync("Waiting for client...").ConfigureAwait(false);
                await server.WaitForConnectionAsync(_lifetimeCts.Token).ConfigureAwait(false);
                server.ReadMode = PipeTransmissionMode.Message;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await Console.Out.WriteLineAsync("Client connected. Hosting JSON-RPC endpoint.").ConfigureAwait(false);

            // Create a DI scope per-connection. Resolve ICalculator if registered; otherwise fall back.
            var scope = _serviceProvider.CreateScope();
            ICalculator calculator;
            try
            {
                calculator = scope.ServiceProvider.GetService<ICalculator>() ?? new Calculator();
            }
            catch
            {
                // If resolution throws for any reason, ensure we have a fallback.
                calculator = new Calculator();
            }

            var service = new CalculatorService(_lifetimeCts, calculator, scope);

            using var jsonRpc = JsonRpc.Attach(server, service);
            try
            {
                await jsonRpc.Completion.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await Console.Out.WriteLineAsync("Cancellation requested. Shutting down current session.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"JsonRpc session ended with error: {ex.Message}").ConfigureAwait(false);
            }

            // Dispose the scope (CalculatorService also disposes if needed).
            try { scope.Dispose(); } catch { }

            if (_lifetimeCts.IsCancellationRequested)
            {
                break;
            }

            await Console.Out.WriteLineAsync("Client disconnected. Awaiting next connection.").ConfigureAwait(false);
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);
    }

    public void Dispose()
    {
    }
}

[SupportedOSPlatform("windows")]
internal sealed class CalculatorService : ICalculator, IDisposable
{
    private readonly CancellationTokenSource _shutdownSignal;
    private readonly ICalculator _calculator;
    private readonly IServiceScope? _scope;

    public CalculatorService(CancellationTokenSource shutdownSignal, ICalculator calculator, IServiceScope? scope = null)
    {
        _shutdownSignal = shutdownSignal;
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _scope = scope;
    }

    // Synchronous methods removed from the contract. Forward async calls to the inner calculator.
    public Task<int> AddAsync(int a, int b) => _calculator.AddAsync(a, b);

    public Task<string> GetPlatformInfoAsync() => _calculator.GetPlatformInfoAsync();

    public async Task ShutdownAsync()
    {
        if (!_shutdownSignal.IsCancellationRequested)
        {
            await _shutdownSignal.CancelAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        try { _scope?.Dispose(); } catch { }
    }
}
