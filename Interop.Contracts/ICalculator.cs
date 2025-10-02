using System;
using System.Threading.Tasks;

namespace Interop.Contracts
{
    public interface ICalculator
    {
        // Async API for StreamJsonRpc
        Task<int> AddAsync(int a, int b);
        Task<string> GetPlatformInfoAsync();
    }
}

