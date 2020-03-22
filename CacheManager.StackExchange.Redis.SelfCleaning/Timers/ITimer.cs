using System;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Timers
{
    public interface ITimer : IStartable
    {
        event Action Elapsed;
    }
}