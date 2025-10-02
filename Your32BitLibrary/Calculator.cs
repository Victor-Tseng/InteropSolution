using System;
using System.Threading.Tasks;
using Interop.Contracts;

namespace Your32BitLibrary
{
    public class Calculator : ICalculator
    {
        // Async-only implementation to match the RPC contract (ICalculator)
        public Task<int> AddAsync(int a, int b)
        {
            // Simple synchronous computation wrapped as a completed Task.
            return Task.FromResult(a + b);
        }

        public Task<string> GetPlatformInfoAsync()
        {
            string bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            return Task.FromResult($"Running on {bitness} process");
        }
    }
}
