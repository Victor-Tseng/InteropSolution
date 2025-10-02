using System;
using System.IO;
using System.Threading.Tasks;

// Simple end-to-end smoke test. Exits 0 on success, 1 on failure.
try
{
	var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

	// Candidate locations to find a locally built/published InteropProxy exe.
	var candidates = new[]
	{
		Environment.GetEnvironmentVariable("INTEROP_PROXY_PATH"),
		Path.Combine(repoRoot, "publish", "InteropProxy-win-x86-self", "InteropProxy.exe"),
		Path.Combine(repoRoot, "publish", "InteropProxy-win-x86", "InteropProxy.exe"),
		Path.Combine(repoRoot, "InteropProxy", "bin", "Release", "net8.0", "win-x86", "publish", "InteropProxy.exe"),
		Path.Combine(repoRoot, "InteropProxy", "bin", "Release", "net8.0", "publish", "InteropProxy.exe"),
		Path.Combine(repoRoot, "InteropProxy", "bin", "Release", "net8.0", "InteropProxy.exe")
	};

	string? proxyPath = null;
	foreach (var candidate in candidates)
	{
		if (string.IsNullOrWhiteSpace(candidate)) continue;
		try
		{
			var full = Path.GetFullPath(candidate);
			if (File.Exists(full))
			{
				proxyPath = full;
				break;
			}
		}
		catch { }
	}

	if (proxyPath == null)
	{
		Console.Error.WriteLine("InteropProxy executable not found. Build the solution and publish the InteropProxy (see docs/installation-guide.md).\nYou can also set INTEROP_PROXY_PATH to point to a published InteropProxy.exe.");
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
