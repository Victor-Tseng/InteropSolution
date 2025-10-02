using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using StreamJsonRpc;
using Interop.Contracts;
using Your32BitLibrary;

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

        using var host = new NamedPipeCalculatorHost(PipeName, lifetimeCts);
        await host.RunAsync().ConfigureAwait(false);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class NamedPipeCalculatorHost : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly CalculatorService _service;

    public NamedPipeCalculatorHost(string pipeName, CancellationTokenSource lifetimeCts)
    {
        _pipeName = pipeName;
        _lifetimeCts = lifetimeCts;
        _service = new CalculatorService(_lifetimeCts);
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

            using var jsonRpc = JsonRpc.Attach(server, _service);
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
internal sealed class CalculatorService : ICalculator
{
    private readonly CancellationTokenSource _shutdownSignal;
    private readonly Calculator _calculator = new();

    public CalculatorService(CancellationTokenSource shutdownSignal)
    {
        _shutdownSignal = shutdownSignal;
    }

    public int Add(int a, int b) => _calculator.Add(a, b);

    public string GetPlatformInfo() => _calculator.GetPlatformInfo();

    public Task<int> AddAsync(int a, int b) => _calculator.AddAsync(a, b);

    public Task<string> GetPlatformInfoAsync() => _calculator.GetPlatformInfoAsync();

    public async Task ShutdownAsync()
    {
        if (!_shutdownSignal.IsCancellationRequested)
        {
            await _shutdownSignal.CancelAsync().ConfigureAwait(false);
        }
    }
}
