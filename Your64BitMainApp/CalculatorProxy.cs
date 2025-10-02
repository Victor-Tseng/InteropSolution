#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Interop.Contracts;
using StreamJsonRpc;

/// <summary>
/// CalculatorProxy: connects to an x86 InteropProxy via a named pipe using StreamJsonRpc for automatic JSON marshaling.
/// Starts the 32-bit proxy on demand, keeps the connection alive, and retries when the channel drops.
/// </summary>
public sealed class CalculatorProxy : ICalculator, IDisposable, IAsyncDisposable
{
    private const string PipeName = "InteropPipe";
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly TimeSpan _initialProbeTimeout = TimeSpan.FromMilliseconds(200);
    private readonly TimeSpan _connectRetryWindow = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(150);

    private NamedPipeClientStream? _client;
    private JsonRpc? _jsonRpc;
    private Process? _serviceProcess;
    private bool _startedService;
    private string? _cachedProxyPath;
    private Exception? _lastConnectException;
    private bool _disposed;

    public CalculatorProxy(bool startIfMissing = true)
    {
        EnsureConnectedAsync(startIfMissing, CancellationToken.None).GetAwaiter().GetResult();
    }

    public int Add(int a, int b)
    {
#pragma warning disable VSTHRD002
        return InvokeRpcAsync<int>(nameof(Add), a, b).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    public string GetPlatformInfo()
    {
#pragma warning disable VSTHRD002
        return InvokeRpcAsync<string>(nameof(GetPlatformInfo)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    public Task<int> AddAsync(int a, int b) => InvokeRpcAsync<int>(nameof(Add), a, b);

    public Task<string> GetPlatformInfoAsync() => InvokeRpcAsync<string>(nameof(GetPlatformInfo));

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose(true);
        return ValueTask.CompletedTask;
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            TryShutdownRemoteAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        }
        catch
        {
            // best effort shutdown
        }

        DisposeConnectionObjects();

        if (_startedService && _serviceProcess != null)
        {
            try
            {
                if (!_serviceProcess.HasExited)
                {
                    if (!_serviceProcess.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds))
                    {
                        _serviceProcess.Kill(entireProcessTree: true);
                    }
                }
            }
            catch
            {
                // swallow shutdown errors
            }
            finally
            {
                _serviceProcess.Dispose();
            }
        }

        _connectionLock.Dispose();
    }

    private async Task<T> InvokeRpcAsync<T>(string methodName, params object?[] args)
    {
        EnsureNotDisposed();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await EnsureConnectedAsync(startIfMissing: true, CancellationToken.None).ConfigureAwait(false);

            try
            {
                return await _jsonRpc!
                    .InvokeAsync<T>(methodName, args)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt == 0)
            {
                await ResetConnectionAsync().ConfigureAwait(false);
            }
        }

        await EnsureConnectedAsync(startIfMissing: true, CancellationToken.None).ConfigureAwait(false);
        return await _jsonRpc!.InvokeAsync<T>(methodName, args).ConfigureAwait(false);
    }

    private async Task EnsureConnectedAsync(bool startIfMissing, CancellationToken cancellationToken)
    {
        if (IsConnected())
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected())
            {
                return;
            }

            if (await TryConnectAsync(_initialProbeTimeout, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!startIfMissing)
            {
                throw new InvalidOperationException("Named pipe not available and startIfMissing is false.", _lastConnectException);
            }

            await StartServiceIfNeededAsync(cancellationToken).ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < _connectRetryWindow)
            {
                if (await TryConnectAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Failed to connect to InteropProxy within timeout.", _lastConnectException);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<bool> TryConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        NamedPipeClientStream? client = null;
        try
        {
            client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await client.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
            client.ReadMode = PipeTransmissionMode.Message;

            _client = client;
            _jsonRpc = JsonRpc.Attach(client);
            _lastConnectException = null;
            return true;
        }
        catch (OperationCanceledException)
        {
            client?.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            client?.Dispose();
            _lastConnectException = ex;
            return false;
        }
    }

    private Task StartServiceIfNeededAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_serviceProcess != null && !_serviceProcess.HasExited)
        {
            return Task.CompletedTask;
        }

        var executablePath = GetOrDiscoverProxyPath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new FileNotFoundException("Could not locate InteropProxy executable or dll.");
        }

        var startInfo = BuildStartInfo(executablePath);
        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start InteropProxy process.");
        }

        _serviceProcess = process;
        _startedService = true;

        return Task.CompletedTask;
    }

    private ProcessStartInfo BuildStartInfo(string path)
    {
        var workingDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        if (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = ResolveDotnetHost(),
            Arguments = $"\"{path}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
    private static string ResolveDotnetHost()
    {
        var x86Root = Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
        if (!string.IsNullOrWhiteSpace(x86Root))
        {
            var candidate = Path.Combine(x86Root, "dotnet.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet";
    }

    private string? GetOrDiscoverProxyPath()
    {
        if (!string.IsNullOrWhiteSpace(_cachedProxyPath) && File.Exists(_cachedProxyPath))
        {
            return _cachedProxyPath;
        }

        var configured = Environment.GetEnvironmentVariable("INTEROP_PROXY_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            _cachedProxyPath = configured;
            return _cachedProxyPath;
        }

        var baseDir = AppContext.BaseDirectory;
        var probeRoots = new[]
        {
            baseDir,
            Path.GetFullPath(Path.Combine(baseDir, "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "InteropProxy")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."))
        };

        foreach (var root in probeRoots)
        {
            var candidate = FindProxyIn(root);
            if (candidate != null)
            {
                _cachedProxyPath = candidate;
                return _cachedProxyPath;
            }
        }

        return null;
    }

    private static string? FindProxyIn(string root)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        try
        {
            foreach (var candidate in Directory.EnumerateFiles(root, "InteropProxy.exe", SearchOption.AllDirectories))
            {
                return candidate;
            }

            foreach (var candidate in Directory.EnumerateFiles(root, "InteropProxy.dll", SearchOption.AllDirectories))
            {
                return candidate;
            }
        }
        catch
        {
            // ignore inaccessible directories
        }

        return null;
    }

    private async Task TryShutdownRemoteAsync(TimeSpan timeout)
    {
        if (_jsonRpc == null)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await _jsonRpc.NotifyAsync("ShutdownAsync").WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // best effort shutdown
        }
    }

    private async Task ResetConnectionAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            DisposeConnectionObjects();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void DisposeConnectionObjects()
    {
        try
        {
            _jsonRpc?.Dispose();
        }
        catch
        {
            // ignore cleanup errors
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
            // ignore cleanup errors
        }

        _jsonRpc = null;
        _client = null;
    }

    private bool IsConnected() => _jsonRpc != null && _client is { IsConnected: true };

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CalculatorProxy));
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is IOException or ObjectDisposedException or EndOfStreamException or StreamJsonRpc.ConnectionLostException;
}
