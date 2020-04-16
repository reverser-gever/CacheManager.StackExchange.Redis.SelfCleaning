using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests.Integrations
{
    [TestFixture]
    public abstract class SelfCleaningRedisIntegrationTestsBase
    {
        private const double TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD = 200;
        private const double CLEANUP_INTERVAL = 100;

        private Mock<IServer> _serverMock;
        protected Mock<IDatabase> DatabaseMock;
        private Mock<IConnectionMultiplexer> _connectionMock;

        protected IDictionary<RedisKey, CacheItemWithInsertionTime> FauxDatabase;

        private ITimer _cleanupTimer;
        private TimeSpan _timeToLive;
        private ICacheManager<DummyModel> _cache;

        private ICollection<OnRemoveByHandleInvocation> _onRemoveByHandleInvocations;

        protected abstract Version RedisVersion { get; }

        [SetUp]
        public void Setup()
        {
            _serverMock = new Mock<IServer>();
            DatabaseMock = new Mock<IDatabase>();
            _connectionMock = new Mock<IConnectionMultiplexer>();

            FauxDatabase = new ConcurrentDictionary<RedisKey, CacheItemWithInsertionTime>();

            SetupServerMock();
            SetupDatabaseMock();
            SetupDatabaseMockByVersion();
            SetupConnectionMock();
            SetupCache();
        }

        #region Setup Methods

        private void SetupServerMock()
        {
            _serverMock.SetupGet(server => server.IsConnected).Returns(true);
            _serverMock.SetupGet(server => server.Features).Returns(new RedisFeatures(RedisVersion));
            _serverMock
                .Setup(server => server.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(() => FauxDatabase.Keys);
        }

        private void SetupDatabaseMock()
        {
            DatabaseMock.SetupGet(database => database.Database).Returns(0);
            DatabaseMock.Setup(database => database.KeyIdleTime(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns<RedisKey, CommandFlags>((key, flags) => DateTime.Now - FauxDatabase[key].InsertionTime);

            // Remove
            DatabaseMock
                .Setup(database => database.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, CommandFlags>((key, flags) => FauxDatabase.Remove(key))
                .Returns(true);
        }

        private void SetupConnectionMock()
        {
            _connectionMock.SetupGet(connection => connection.Configuration).Returns("connectionString");
            _connectionMock.Setup(connection => connection.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[1]);
            _connectionMock.Setup(connection => connection.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
                .Returns(_serverMock.Object);
            _connectionMock.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(DatabaseMock.Object);
        }

        private void SetupCache()
        {
            _cleanupTimer = new DefaultTimer(CLEANUP_INTERVAL);
            _timeToLive = TimeSpan.FromSeconds(1);

            _cache = CacheFactory.Build<DummyModel>(part => part
                .WithJsonSerializer()
                .WithSelfCleaningRedisConfiguration(_connectionMock.Object, _cleanupTimer, _timeToLive,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));

            _onRemoveByHandleInvocations = new List<OnRemoveByHandleInvocation>();

            _cache.OnRemoveByHandle += (sender, args) => _onRemoveByHandleInvocations.Add(
                new OnRemoveByHandleInvocation
                {
                    Args = args,
                    RemovalTime = DateTime.Now
                });

            _cache.CacheHandles.OfType<SelfCleaningRedisCacheHandle<DummyModel>>().Single().Start();
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            FauxDatabase.Clear();
            _onRemoveByHandleInvocations.Clear();
            _cache.Dispose();

            // Clear the configurations dictionary to make sure a previously given configuration won't be used again  
            var redisConfigurations = typeof(RedisConfigurations)
                .GetProperty("Configurations", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IDictionary<string, RedisConfiguration>;

            redisConfigurations?.Clear();

            // Clear the connections dictionary to make sure a previously given connection won't be used again
            var redisConnections = Assembly.GetAssembly(typeof(RedisCacheHandle<>))
                .GetType("CacheManager.Redis.RedisConnectionManager")
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as IDictionary<string, IConnectionMultiplexer>;

            redisConnections?.Clear();
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        public void Put_AddSomeAmountOfKeys_OnRemoveByHandleCalledAfterTimeToLiveWithCorrectValueAndReason(
            int amountOfKeys, bool delayBetweenInsertions)
        {
            // Arrange
            IEnumerable<(string, DummyModel)> cacheItems = GenerateDifferentCacheItems(amountOfKeys);

            // Act
            var expectedCachedItems = new List<(string ExpectedKey, DummyModel ExpectedValue, DateTime InsertionTime)>();

            foreach ((string key, DummyModel value) in cacheItems)
            {
                _cache[key] = value;
                DateTime insertionTime = DateTime.Now;
                expectedCachedItems.Add((key, value, insertionTime));

                if (delayBetweenInsertions)
                {
                    Wait(_timeToLive / 2);
                }
            }

            Wait(_timeToLive.TotalMilliseconds + TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD);

            // Assert
            Assert.AreEqual(expectedCachedItems.Count, _onRemoveByHandleInvocations.Count);

            IDictionary<string, OnRemoveByHandleInvocation> invocationsDictionary =
                _onRemoveByHandleInvocations.ToDictionary(invocation => invocation.Args.Key, invocation => invocation);

            foreach ((string expectedKey, DummyModel expectedValue, DateTime insertionTime) in expectedCachedItems)
            {
                CollectionAssert.Contains(invocationsDictionary.Keys, expectedKey);
                OnRemoveByHandleAssertion(invocationsDictionary[expectedKey], expectedValue, insertionTime,
                    _timeToLive);
            }
        }

        [Test]
        public void Put_AddKeyAndUpdateBeforeExpiry_TimeToLiveMeasuredFromLastUpdateTime()
        {
            // Arrange
            var key = "key1";
            var value = new DummyModel { Property = "property1" };
            var updatedValue = new DummyModel { Property = "property2" };

            TimeSpan delayBeforeUpdate = _timeToLive / 2;

            // Act
            _cache[key] = value;
            DateTime insertionTime = DateTime.Now;

            Wait(delayBeforeUpdate);

            _cache[key] = updatedValue;

            Wait(_timeToLive.TotalMilliseconds + TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD);

            // Assert
            OnRemoveByHandleAssertion(_onRemoveByHandleInvocations.Single(), updatedValue, insertionTime,
                _timeToLive + delayBeforeUpdate);
        }

        [Test]
        public void PutAndRemove_AddKeyAndRemoveBeforeExpiry_OnRemoveByHandleNotCalled()
        {
            // Arrange
            var key = "key1";
            var value = new DummyModel { Property = "property1" };

            TimeSpan delayBeforeRemove = _timeToLive / 2;

            // Act
            _cache[key] = value;

            Wait(delayBeforeRemove);

            _cache.Remove(key);

            // In order to be sure that event was not invoked, we wait even longer in this test
            Wait(_timeToLive.TotalMilliseconds + TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD * 2);

            // Assert
            CollectionAssert.IsEmpty(_onRemoveByHandleInvocations);
        }

        [Test]
        public void RunCleanup_NoKeysInServer_OnRemoveByHandleNotCalled()
        {
            // Arrange
            _serverMock
                .Setup(server => server.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(Enumerable.Empty<RedisKey>);

            // Act

            // In order to be sure that event was not invoked, we wait even longer in this test
            Wait(CLEANUP_INTERVAL * 2);

            // Assert
            CollectionAssert.IsEmpty(_onRemoveByHandleInvocations);
        }

        [Test]
        public void Put_KeyIdleTimeReturnsNull_OnRemoveByHandleNotCalled()
        {
            // Arrange
            var key = "key1";
            var value = new DummyModel { Property = "property1" };

            DatabaseMock
                .Setup(database => database.KeyIdleTime(key, It.IsAny<CommandFlags>()))
                .Returns((TimeSpan?)null);

            // Act
            _cache[key] = value;

            // In order to be sure that event was not invoked, we wait even longer in this test
            Wait(CLEANUP_INTERVAL * 2);

            // Assert
            CollectionAssert.IsEmpty(_onRemoveByHandleInvocations);
        }

        [Test]
        public void PutAndRemoveByAnotherInstance_AddThreeKeysAndTheMiddltOneFailedToBeRemoved_OnRemoveByHandleCalledTwoTimes()
        {
            // Arrange
            IEnumerable<(string, DummyModel)> cacheItems = GenerateDifferentCacheItems(3).ToList();

            var middleCacheItemKey = cacheItems.ElementAt(1).Item1;

            // Configure Remove to return false for the middle cachedItem
            DatabaseMock
                .Setup(database => database.KeyDelete(
                    It.Is<RedisKey>(key => key.ToString() == middleCacheItemKey), It.IsAny<CommandFlags>()))
                .Returns(false);

            // Act
            var expectedCachedItems = new List<(string ExpectedKey, DummyModel ExpectedValue, DateTime InsertionTime)>();

            foreach ((string key, DummyModel value) in cacheItems)
            {
                _cache[key] = value;
                DateTime insertionTime = DateTime.Now;

                if (key != middleCacheItemKey)
                {
                    expectedCachedItems.Add((key, value, insertionTime));
                }
            }

            Wait(_timeToLive.TotalMilliseconds + TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD);

            // Assert
            Assert.AreEqual(expectedCachedItems.Count, _onRemoveByHandleInvocations.Count);

            IDictionary<string, OnRemoveByHandleInvocation> invocationsDictionary =
                _onRemoveByHandleInvocations.ToDictionary(invocation => invocation.Args.Key, invocation => invocation);

            foreach ((string expectedKey, DummyModel expectedValue, DateTime insertionTime) in expectedCachedItems)
            {
                CollectionAssert.Contains(invocationsDictionary.Keys, expectedKey);
                OnRemoveByHandleAssertion(invocationsDictionary[expectedKey], expectedValue, insertionTime,
                    _timeToLive);
            }
        }

        [Test]
        public void Dispose_HappyFlow_CleanupTimerIsDisposed()
        {
            // Act
            _cache.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => _cleanupTimer.Start());
        }

        protected void PutInFauxDatabase(RedisKey key, CacheItemWithInsertionTime item)
        {
            if (FauxDatabase.ContainsKey(key))
            {
                FauxDatabase[key] = item;
            }
            else
            {
                FauxDatabase.Add(key, item);
            }
        }

        protected abstract void SetupDatabaseMockByVersion();

        private static void Wait(TimeSpan delay) => Task.Delay(delay).Wait();

        private static void Wait(double delay) => Task.Delay((int)delay).Wait();

        private static IEnumerable<(string, DummyModel)> GenerateDifferentCacheItems(int count) =>
            Enumerable.Repeat("key", count).Select((key, i) => (key + i, new DummyModel { Property = "property" + i }));

        private static void OnRemoveByHandleAssertion(OnRemoveByHandleInvocation invocation,
            DummyModel expectedValue, DateTime insertionTime, TimeSpan expectedTimeAlive)
        {
            Assert.AreEqual(CacheItemRemovedReason.Expired, invocation.Args.Reason);
            Assert.AreEqual(expectedValue, invocation.Args.Value);

            TimeSpan timeAlive = invocation.RemovalTime - insertionTime;
            double differenceBetweenTimeAliveAndExpected =
                Math.Abs(timeAlive.TotalMilliseconds - expectedTimeAlive.TotalMilliseconds);

            Assert.LessOrEqual(differenceBetweenTimeAliveAndExpected, TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD);
        }
    }
}