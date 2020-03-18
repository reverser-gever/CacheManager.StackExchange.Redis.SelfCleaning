using System;
using CacheManager.Core;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    public static class SelfCleaningRedisConfigurationBuilderExtensions
    {
        private const string DEFAULT_CONFIGURATION_KEY = "redis";
        private const string CONFIGURATION_KEY_SUFFIX = "-self_cleaning";
        
        public static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, string configurationKey, IDatabase redisDatabase,
            ITimer cleanupTimer, TimeSpan slidingExpiration, out string newConfigurationKey)
        {
            RedisConfiguration configuration = RedisConfigurations.GetConfiguration(configurationKey);

            newConfigurationKey = configurationKey + CONFIGURATION_KEY_SUFFIX;

            var newConfiguration = new SelfCleaningRedisConfiguration(redisDatabase, cleanupTimer, slidingExpiration)
            {
                Key = newConfigurationKey,
                ConnectionString = configuration.ConnectionString,
                Database = configuration.Database,
                KeyspaceNotificationsEnabled = configuration.KeyspaceNotificationsEnabled
            };
            
            RedisConfigurations.AddConfiguration(newConfiguration);

            return part;
        }

        public static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, IConnectionMultiplexer redisClient, ITimer cleanupTimer,
            TimeSpan slidingExpiration, out string configurationKey, int databaseId = 0,
            bool enableKeyspaceNotifications = false)
        {
            return part
                .WithRedisConfiguration(DEFAULT_CONFIGURATION_KEY, redisClient, databaseId, enableKeyspaceNotifications)
                .WithSelfCleaningRedisConfiguration(DEFAULT_CONFIGURATION_KEY, redisClient.GetDatabase(databaseId),
                    cleanupTimer, slidingExpiration, out configurationKey);
        }

        public static ConfigurationBuilderCachePart WithSelfCleaningRedisConfiguration(
            this ConfigurationBuilderCachePart part, string connectionString, TimeSpan cleanupInterval,
            TimeSpan slidingExpiration, out string configurationKey, int databaseId = 0,
            bool enableKeyspaceNotifications = false)
        {
            IConnectionMultiplexer redisClient = ConnectionMultiplexer.Connect(connectionString);
            ITimer cleanupTimer = new DefaultTimer(cleanupInterval.TotalMilliseconds);

            return part.WithSelfCleaningRedisConfiguration(redisClient, cleanupTimer, slidingExpiration,
                out configurationKey, databaseId, enableKeyspaceNotifications);
        }

        public static ConfigurationBuilderCacheHandlePart WithSelfCleaningRedisCacheHandle(
            this ConfigurationBuilderCachePart part, string configurationKey)
        {
            return part.WithHandle(typeof(SelfCleaningRedisCacheHandle<>), configurationKey);
        }
    }
}