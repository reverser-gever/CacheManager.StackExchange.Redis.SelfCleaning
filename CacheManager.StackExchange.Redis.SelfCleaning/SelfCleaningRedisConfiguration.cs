using System;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    public class SelfCleaningRedisConfiguration : RedisConfiguration
    {
        public IDatabase RedisDatabase { get; }

        public ITimer CleanupTimer { get; }
        
        public TimeSpan TimeToLive { get; }

        public SelfCleaningRedisConfiguration(IDatabase redisDatabase, ITimer cleanupTimer, TimeSpan timeToLive)
        {
            RedisDatabase = redisDatabase;
            CleanupTimer = cleanupTimer;
            TimeToLive = timeToLive;
        }
    }
}