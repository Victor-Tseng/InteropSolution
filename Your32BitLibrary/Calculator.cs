using System;
using System.Threading.Tasks;
using Interop.Contracts;

namespace Your32BitLibrary
{
    public class Calculator : ICalculator
    {
        public Task<int> AddAsync(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        public Task<string> GetPlatformInfoAsync()
        {
            string bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            return Task.FromResult($"Running on {bitness} process");
        }
    }
}
