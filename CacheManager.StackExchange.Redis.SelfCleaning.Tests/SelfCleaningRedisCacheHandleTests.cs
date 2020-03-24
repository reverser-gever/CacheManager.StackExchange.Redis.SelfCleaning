using System;
using System.Linq;
using System.Net;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    [TestFixture]
    public class SelfCleaningRedisCacheHandleTests
    {
        private Mock<IServer> _serverMock;
        
        private Mock<IConnectionMultiplexer> _connectionMock;
        private Mock<ITimer> _timerMock;
        private TimeSpan _timeToLive;

        private ICacheManager<DummyModel> _cache;
        private SelfCleaningRedisCacheHandle<DummyModel> _cacheHandle;

        [SetUp]
        public void Setup()
        {
            _serverMock = new Mock<IServer>();
            
            _connectionMock = new Mock<IConnectionMultiplexer>();
            _timerMock = new Mock<ITimer>();
            _timeToLive = TimeSpan.FromSeconds(1);

            SetupConnectionMock();

            _cache = CacheFactory.Build<DummyModel>(part => part
                .WithSerializer(typeof(DummySerializer))
                .WithSelfCleaningRedisConfiguration(_connectionMock.Object, _timerMock.Object, _timeToLive,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));

            _cacheHandle = (SelfCleaningRedisCacheHandle<DummyModel>) _cache.CacheHandles.Single();
        }

        private void SetupConnectionMock()
        {
            _serverMock.SetupGet(server => server.IsConnected).Returns(true);
            _serverMock.SetupGet(server => server.Features).Returns(new RedisFeatures(Version.Parse("3.0.504")));

            _connectionMock.SetupGet(connection => connection.Configuration).Returns("connectionString");
            _connectionMock.Setup(connection => connection.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[1]);
            _connectionMock.Setup(connection => connection.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
                .Returns(_serverMock.Object);
        }

        [Test]
        public void Start_HappyFlow_SubscribesToElapsedAndStartTimer()
        {
            // Arrange
            _timerMock.SetupAdd(timer => timer.Elapsed += It.IsAny<Action>());
            
            // Act
            _cacheHandle.Start();
            
            // Assert
            _timerMock.VerifyAdd(timer => timer.Elapsed += It.IsAny<Action>(), Times.Once);
            _timerMock.Verify(timer => timer.Start(), Times.Once);
        }

        [Test]
        public void Dispose_HappyFlow_UnsubscribesFromElapsedAndDisposesTimer()
        {
            // Arrange
            _timerMock.SetupRemove(timer => timer.Elapsed -= It.IsAny<Action>());
            
            // Act
            _cache.Dispose();
            
            // Assert
            _timerMock.VerifyRemove(timer => timer.Elapsed -= It.IsAny<Action>(), Times.Once);
            _timerMock.Verify(timer => timer.Dispose(), Times.Once);
        }
    }
}