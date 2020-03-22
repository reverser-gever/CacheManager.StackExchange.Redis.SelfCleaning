using System;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Core
{
    public interface IStartable : IDisposable
    {
        void Start();
    }
}