using System;
using System.IO;
using System.Threading.Tasks;

// Simple end-to-end smoke test. Exits 0 on success, 1 on failure.
try
{
	var repoRoot = AppContext.BaseDirectory; // tests bin folder
	// Allow override via environment variable
	var proxyPath = Environment.GetEnvironmentVariable("INTEROP_PROXY_PATH")
					?? Path.GetFullPath(Path.Combine(repoRoot, "..", "..", "..", "publish", "InteropProxy-win-x86-self", "InteropProxy.exe"));

	if (!File.Exists(proxyPath))
	{
		Console.Error.WriteLine($"InteropProxy not found at {proxyPath}. Set INTEROP_PROXY_PATH to the published exe.");
		Environment.Exit(1);
	}

	Environment.SetEnvironmentVariable("INTEROP_PROXY_PATH", proxyPath);

	var calculator = await CalculatorProxy.CreateAsync(startIfMissing: true);
	var result = await calculator.AddAsync(2, 2);
	Console.WriteLine($"E2E: 2 + 2 = {result}");
	await calculator.DisposeAsync();

	if (result != 4)
	{
		Console.Error.WriteLine("Unexpected result");
		Environment.Exit(1);
	}

	Environment.Exit(0);
}
catch (Exception ex)
{
	Console.Error.WriteLine($"E2E failure: {ex}");
	Environment.Exit(1);
}
