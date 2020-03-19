using System;
using CacheManager.Core;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    /// <summary>
    /// To properly use these methods, first call <see cref="WithSelfCleaningRedisConfiguration"/>,
    /// then call <see cref="WithSelfCleaningRedisCacheHandle"/>
    /// </summary>
    public static class SelfCleaningRedisConfigurationBuilderExtensions
    {
        // CacheManager saves its RedisConfigurations in a dictionary-like way. These keys exist to identify and
        // separate between the basic RedisConfiguration and our SelfCleaningRedisConfiguration
        private const string REDIS_CONFIGURATION_KEY = "redis";
        private const string SELF_CLEANING_REDIS_CONFIGURATION_KEY = "redis-self_cleaning";

        public static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, string connectionString, TimeSpan cleanupInterval,
            TimeSpan timeToLive, out string configurationKey, int databaseId = 0,
            bool enableKeyspaceNotifications = false)
        {
            IConnectionMultiplexer redisClient = ConnectionMultiplexer.Connect(connectionString);
            ITimer cleanupTimer = new DefaultTimer(cleanupInterval.TotalMilliseconds);

            return part.WithSelfCleaningRedisConfiguration(redisClient, cleanupTimer, timeToLive,
                out configurationKey, databaseId, enableKeyspaceNotifications);
        }

        public static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, IConnectionMultiplexer redisClient, ITimer cleanupTimer,
            TimeSpan timeToLive, out string configurationKey, int databaseId = 0,
            bool enableKeyspaceNotifications = false)
        {
            return part
                .WithRedisConfiguration(REDIS_CONFIGURATION_KEY, redisClient, databaseId, enableKeyspaceNotifications)
                .WithSelfCleaningRedisConfiguration(REDIS_CONFIGURATION_KEY, redisClient.GetDatabase(databaseId),
                    cleanupTimer, timeToLive, out configurationKey);
        }
        
        private static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, string configurationKey, IDatabase redisDatabase,
            ITimer cleanupTimer, TimeSpan timeToLive, out string newConfigurationKey)
        {
            RedisConfiguration configuration = RedisConfigurations.GetConfiguration(configurationKey);

            newConfigurationKey = SELF_CLEANING_REDIS_CONFIGURATION_KEY;

            var newConfiguration = new SelfCleaningRedisConfiguration(redisDatabase, cleanupTimer, timeToLive)
            {
                Key = newConfigurationKey,
                ConnectionString = configuration.ConnectionString,
                Database = configuration.Database,
                KeyspaceNotificationsEnabled = configuration.KeyspaceNotificationsEnabled
            };
            
            RedisConfigurations.AddConfiguration(newConfiguration);

            return part;
        }

        public static ConfigurationBuilderCacheHandlePart WithSelfCleaningRedisCacheHandle(
            this ConfigurationBuilderCachePart part, string configurationKey)
        {
            // This method tells the CacheFactory to create an instance of SelfCleaningRedisCacheHandle when it builds
            // the cache (CacheManager does this using reflection). This instance will use the configuration added by
            // the WithSelfCleaningRedisConfiguration method
            return part.WithHandle(typeof(SelfCleaningRedisCacheHandle<>), configurationKey);
        }
    }
}