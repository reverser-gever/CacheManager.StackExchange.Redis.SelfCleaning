using System;
using System.Linq;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    internal static class TestUtilities
    {
        #region Testable SelfCleaningRedisCacheHandle class Creation
        internal static TestableSelfCleaningRedisCacheHandle<DummyModel>CreateSelfCleaningRedisCacheHandleForTests(
            IConnectionMultiplexer connectionMock, ITimer timerMock, TimeSpan timeToLive)
        {
            var cache = CacheFactory.Build<DummyModel>(part => part
                .WithSerializer(typeof(DummySerializer))
                .WithSelfCleaningRedisConfiguration(connectionMock, timerMock, timeToLive,
                    out string configurationKey)
                .WithTestableSelfCleaningRedisCacheHandle(configurationKey));

            return (TestableSelfCleaningRedisCacheHandle<DummyModel>)cache.CacheHandles.Single();
        }

        private static ConfigurationBuilderCacheHandlePart WithTestableSelfCleaningRedisCacheHandle(
            this ConfigurationBuilderCachePart part, string configurationKey)
        {
            // This method tells the CacheFactory to create an instance of SelfCleaningRedisCacheHandle when it builds
            // the cache (CacheManager does this using reflection). This instance will use the configuration added by
            // the WithSelfCleaningRedisConfiguration method
            return part.WithHandle(typeof(TestableSelfCleaningRedisCacheHandle<>), configurationKey);
        }

        #endregion
    }
}