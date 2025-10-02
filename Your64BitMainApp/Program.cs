using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Previously, if the 32-bit library was referenced directly you would call:
        // var calculator = new Calculator();
        // To keep the host 64-bit and still use the same API, replace that with the proxy:
        var calculator = new CalculatorProxy(startIfMissing: true);

        int result = await calculator.AddAsync(5, 3);
        Console.WriteLine($"5 + 3 = {result}");

        string info = await calculator.GetPlatformInfoAsync();
        Console.WriteLine($"Service Info: {info}");

        Console.WriteLine($"Host is 64-bit: {Environment.Is64BitProcess}");

        calculator.Dispose();
    }
}
