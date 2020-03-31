using System;
using System.Linq;
using System.Net;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using Moq;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    internal static class TestUtilities
    {
        #region Testable SelfCleaningRedisCacheHandle class Creation
        internal static TestableSelfCleaningRedisCacheHandle CreateSelfCleaningRedisCacheHandleForTests(
            Mock<IServer> serverMock,
            Mock<IDatabase> redisDatabaseMock, Mock<ITimer> timerMock, TimeSpan timeToLive)
        {
            var connectionMock = new Mock<IConnectionMultiplexer>();

            SetupConnectionMock(connectionMock, serverMock, redisDatabaseMock);

            var cache = CacheFactory.Build<DummyModel>(part => part
                .WithSerializer(typeof(DummySerializer))
                .WithSelfCleaningRedisConfiguration(connectionMock.Object, timerMock.Object, timeToLive,
                    out string configurationKey)
                .WithTestableSelfCleaningRedisCacheHandle(configurationKey));

            return (TestableSelfCleaningRedisCacheHandle)cache.CacheHandles.Single();
        }

        private static void SetupConnectionMock(Mock<IConnectionMultiplexer> connectionMock, Mock<IServer> serverMock, Mock<IDatabase> redisDatabaseMock)
        {
            serverMock.SetupGet(server => server.IsConnected).Returns(true);
            serverMock.SetupGet(server => server.Features).Returns(new RedisFeatures(Version.Parse("3.0.504")));

            connectionMock.SetupGet(connection => connection.Configuration)
                .Returns("connectionString");
            connectionMock.Setup(connection => connection.GetEndPoints(It.IsAny<bool>()))
                .Returns(new EndPoint[1]);
            connectionMock.Setup(connection => connection.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
               .Returns(serverMock.Object);
            connectionMock.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(redisDatabaseMock.Object);
        }

        private static ConfigurationBuilderCacheHandlePart WithTestableSelfCleaningRedisCacheHandle(
            this ConfigurationBuilderCachePart part, string configurationKey)
        {
            // This method tells the CacheFactory to create an instance of SelfCleaningRedisCacheHandle when it builds
            // the cache (CacheManager does this using reflection). This instance will use the configuration added by
            // the WithSelfCleaningRedisConfiguration method
            return part.WithHandle(typeof(TestableSelfCleaningRedisCacheHandle), configurationKey);
        }

        #endregion
    }
}