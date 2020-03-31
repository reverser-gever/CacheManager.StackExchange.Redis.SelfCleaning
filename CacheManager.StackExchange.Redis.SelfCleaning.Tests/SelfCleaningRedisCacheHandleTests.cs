using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
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

        private TestableSelfCleaningRedisCacheHandle<DummyModel> _cacheHandle;

        [SetUp]
        public void Setup()
        {
            _serverMock = new Mock<IServer>();

            _connectionMock = new Mock<IConnectionMultiplexer>();
            _timerMock = new Mock<ITimer>();
            _timeToLive = TimeSpan.FromSeconds(1);

            SetupConnectionMock();

            _cacheHandle = TestUtilities.CreateSelfCleaningRedisCacheHandleForTests(
                _connectionMock.Object, _timerMock.Object, _timeToLive);
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

        [TestCase(true)]
        [TestCase(false)]
        public void Dispose_HappyFlow_UnsubscribesFromElapsedAndDisposesTimerRegardlessOfBlooeanArgument(bool disposeManaged)
        {
            // Arrange
            _timerMock.SetupRemove(timer => timer.Elapsed -= It.IsAny<Action>());

            // Act
            _cacheHandle.RunDispose(disposeManaged);

            // Assert
            _timerMock.VerifyRemove(timer => timer.Elapsed -= It.IsAny<Action>(), Times.Once);
            _timerMock.Verify(timer => timer.Dispose(), Times.Once);
        }

        [Test]
        public void RunCleanup_HappyFlowRedisDataBaseIsntFoundInServers_EventWasntRaised()
        {
            // Arrange
            _serverMock.Setup(server => server.Keys(It.IsAny<int>(),
                    It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(new List<RedisKey>());

            // Act
            _cacheHandle.Start();
            _timerMock.Raise(timer => timer.Elapsed += null);

            // Assert
            _serverMock.VerifyNoOtherCalls();
        }


    }

    #region Testable SelfCleaningRedisCacheHandle internal class

    /// <summary>
    /// Exposes the protected <see cref="SelfCleaningRedisCacheHandle.Dispose"/>
    /// </summary>
    internal class TestableSelfCleaningRedisCacheHandle<TCacheValue> : SelfCleaningRedisCacheHandle<TCacheValue>
    {
        public TestableSelfCleaningRedisCacheHandle(ICacheManagerConfiguration managerConfiguration,
            CacheHandleConfiguration configuration, ILoggerFactory loggerFactory, ICacheSerializer serializer)
            : base(managerConfiguration, configuration, loggerFactory, serializer) { }

        public void RunDispose(bool disposeManaged)
        {
            base.Dispose(disposeManaged);
        }
    }

    #endregion
}