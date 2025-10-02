using System;
using System.Threading.Tasks;

namespace Interop.Contracts
{
    public interface ICalculator
    {
        int Add(int a, int b);
        string GetPlatformInfo();

        // Async versions for StreamJsonRpc
        Task<int> AddAsync(int a, int b);
        Task<string> GetPlatformInfoAsync();
    }
}

