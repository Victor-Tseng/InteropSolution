using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            await Console.Error.WriteLineAsync("This sample host requires Windows to run (uses Named Pipes and x86 proxy). Exiting.");
            return;
        }
    // If the 32-bit library were referenced directly you would call:
    // var calculator = new Calculator();
    var calculator = await CalculatorProxy.CreateAsync(startIfMissing: true);

    var result = await calculator.AddAsync(5, 3);
        Console.WriteLine($"5 + 3 = {result}");
    var info = await calculator.GetPlatformInfoAsync();
    Console.WriteLine($"Service Info: {info}");

    Console.WriteLine($"Main app is 64-bit: {Environment.Is64BitProcess}");

    await calculator.DisposeAsync();
    }
}
