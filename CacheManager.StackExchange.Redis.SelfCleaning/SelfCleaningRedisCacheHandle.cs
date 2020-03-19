using System;
using System.Collections.Generic;
using System.Linq;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    public class SelfCleaningRedisCacheHandle<TCacheValue> : RedisCacheHandle<TCacheValue>, IStartable
    {
        private readonly IDatabase _redisDatabase;
        private readonly ITimer _cleanupTimer;
        private readonly TimeSpan _timeToLive;
        
        public SelfCleaningRedisCacheHandle(ICacheManagerConfiguration managerConfiguration,
            CacheHandleConfiguration configuration, ILoggerFactory loggerFactory, ICacheSerializer serializer) : base(
            managerConfiguration, configuration, loggerFactory, serializer)
        {
            RedisConfiguration redisConfiguration = RedisConfigurations.GetConfiguration(configuration.Key);

            if (!(redisConfiguration is SelfCleaningRedisConfiguration selfCleaningRedisConfiguration))
            {
                throw new ArgumentException(
                    $"The given configuration is not of type {typeof(SelfCleaningRedisConfiguration).Name}. " +
                    $"Actual type: {redisConfiguration.GetType().Name}", 
                    nameof(configuration));
            }

            _redisDatabase = selfCleaningRedisConfiguration.RedisDatabase;
            _cleanupTimer = selfCleaningRedisConfiguration.CleanupTimer;
            _timeToLive = selfCleaningRedisConfiguration.TimeToLive;
        }

        public void Start()
        {
            _cleanupTimer.Elapsed += RunCleanup;
            _cleanupTimer.Start();
        }

        protected override void Dispose(bool disposeManaged)
        {
            _cleanupTimer.Elapsed -= RunCleanup;
            _cleanupTimer.Dispose();
            
            base.Dispose(disposeManaged);
        }

        private void RunCleanup()
        {
            IEnumerable<RedisKey> keysToRemove = Servers
                // We cannot get a list of all keys directly from the database, so we get them from each server instead
                .SelectMany(server => server.Keys(_redisDatabase.Database))
                // After we collect all keys, we filter them and keep only those which exceeded the TTL   
                .Where(key => (_redisDatabase.KeyIdleTime(key) ?? TimeSpan.Zero) >= _timeToLive);

            foreach (RedisKey key in keysToRemove)
            {
                TCacheValue value = Get(key);
                Remove(key);

                TriggerCacheSpecificRemove(key, null, CacheItemRemovedReason.Expired, value);
            }
        }
    }
}