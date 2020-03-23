using System;
using System.Collections.Generic;
using System.Text;
using CacheManager.Core;
using CacheManager.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    class SelfCleaningRedisConfigurationProvider : ISelfCleaningRedisConfigurationProvider
    {
        public SelfCleaningRedisConfiguration GetConfiguration(CacheHandleConfiguration configuration)
        {
            RedisConfiguration redisConfiguration = RedisConfigurations.GetConfiguration(configuration.Key);

            if (!(redisConfiguration is SelfCleaningRedisConfiguration selfCleaningRedisConfiguration))
            {
                throw new ArgumentException(
                    $"The given configuration is not of type {typeof(SelfCleaningRedisConfiguration).Name}. " +
                    $"Actual type: {redisConfiguration.GetType().Name}",
                    nameof(configuration));
            }

            return selfCleaningRedisConfiguration;
        }
    }
}
