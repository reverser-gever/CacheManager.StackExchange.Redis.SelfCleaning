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
using NUnit.Framework.Constraints;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    [TestFixture]
    public class SelfCleaningRedisCacheHandleTests
    {
        private Mock<IServer> _serverMock;
        private Mock<IDatabase> _redisDatabaseMock;
        private int _defulatDatabaseId;
        private Mock<ITimer> _timerMock;
        private TimeSpan _defaultTimeToLive;

        private TestableSelfCleaningRedisCacheHandle _cacheHandle;

        [SetUp]
        public void Setup()
        {
            // Because the 'SelfCleaningRedisCacheHandle' depends on base class, we have 2 kinds of setups here:
            // 1. Setupping behaviors of base class, in order that our code will even run.
            // 2. Setupping behaviors of our code, in order to control different flows and test them (like our usual setups).
            // First kind setups will be in 'TestUtilities.CreateSelfCleaningRedisCacheHandleForTests', and the second kind will
            // be here. That in order to conceal irrelevant setups from the developer that test the class here :)

            _serverMock = new Mock<IServer>();

            _redisDatabaseMock = new Mock<IDatabase>();
            _defulatDatabaseId = 12;
            _redisDatabaseMock.SetupGet(database => database.Database)
                .Returns(_defulatDatabaseId);

            _timerMock = new Mock<ITimer>();
            _defaultTimeToLive = TimeSpan.FromSeconds(1);

            _cacheHandle = TestUtilities.CreateSelfCleaningRedisCacheHandleForTests(
                _serverMock, _redisDatabaseMock, _timerMock, _defaultTimeToLive);
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
            SetupKeysMethodOfServer(It.IsAny<int>(), new List<RedisKey>());

            // Act
            _cacheHandle.Start();
            _timerMock.Raise(timer => timer.Elapsed += null);

            // Assert
            VerifyKeysMethodOfServer(_defulatDatabaseId);

            _redisDatabaseMock.VerifyGet(database => database.Database, Times.Once);
            _redisDatabaseMock.Verify(database => database.KeyIdleTime(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);

            // No calls to Get method
            Assert.IsEmpty(_cacheHandle.KeysArgumentsOfGetMethodCalls);

            // No calls to Remove method
            Assert.IsEmpty(_cacheHandle.KeysArgumentsOfRemoveMethodCalls);
        }

        #region "cleaner" setup and verifiction for 'Keys' method of 'Server'
        // 'Keys' of 'Server' has a lot of optional arguments, and the setup (and the verify) look bad, so I prettify it

        private void SetupKeysMethodOfServer(int databaseId, IEnumerable<RedisKey> result)
        {
            _serverMock.Setup(server => server.Keys(databaseId,
                    It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(result);
        }

        private void VerifyKeysMethodOfServer(int databaseId)
        {
            _serverMock.Verify(server => server.Keys(_defulatDatabaseId,
                It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                It.IsAny<int>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion
    }

    #region Testable SelfCleaningRedisCacheHandle internal class

    /// <summary>
    /// Exposes the protected overrides methods of <see cref="SelfCleaningRedisCacheHandle"/> (Dispose),
    /// and the <see cref="RedisCacheHandle"/> methods which being called from
    /// <see cref="SelfCleaningRedisCacheHandle"/> (Get, Remove)
    /// </summary>
    internal class TestableSelfCleaningRedisCacheHandle : SelfCleaningRedisCacheHandle<DummyModel>
    {
        public TestableSelfCleaningRedisCacheHandle(ICacheManagerConfiguration managerConfiguration,
            CacheHandleConfiguration configuration, ILoggerFactory loggerFactory, ICacheSerializer serializer)
            : base(managerConfiguration, configuration, loggerFactory, serializer) { }

        public void RunDispose(bool disposeManaged)
        {
            base.Dispose(disposeManaged);
        }

        public List<string> KeysArgumentsOfGetMethodCalls = new List<string>();

        public override DummyModel Get(string key)
        {
            KeysArgumentsOfGetMethodCalls.Add(key);

            return base.Get(key);
        }

        public List<string> KeysArgumentsOfRemoveMethodCalls = new List<string>();

        public override bool Remove(string key)
        {
            KeysArgumentsOfRemoveMethodCalls.Add(key);

            return base.Remove(key);
        }
    }

    #endregion
}