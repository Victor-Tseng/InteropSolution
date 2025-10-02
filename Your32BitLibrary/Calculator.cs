using System;
using System.Threading.Tasks;
using Interop.Contracts;

namespace Your32BitLibrary
{
    public class Calculator : ICalculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public string GetPlatformInfo()
        {
            string bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            return $"Running on {bitness} process";
        }

        public Task<int> AddAsync(int a, int b)
        {
            return Task.FromResult(Add(a, b));
        }

        public Task<string> GetPlatformInfoAsync()
        {
            return Task.FromResult(GetPlatformInfo());
        }
    }
}
