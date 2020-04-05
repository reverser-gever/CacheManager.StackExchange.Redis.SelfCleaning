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
    public class SelfCleaningRedisIntegrationTests
    {
        private const double TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD = 200;
        private const double CLEANUP_INTERVAL = 100;
        
        private Mock<IServer> _serverMock;
        private Mock<IDatabase> _databaseMock;
        private Mock<IConnectionMultiplexer> _connectionMock;

        private IDictionary<RedisKey, CacheItemWithInsertionTime> _fauxDatabase;

        private ITimer _cleanupTimer;
        private TimeSpan _timeToLive;
        private ICacheManager<DummyModel> _cache;

        private ICollection<OnRemoveByHandleInvocation> _onRemoveByHandleInvocations;

        [SetUp]
        public void Setup()
        {
            _serverMock = new Mock<IServer>();
            _databaseMock = new Mock<IDatabase>();
            _connectionMock = new Mock<IConnectionMultiplexer>();

            _fauxDatabase = new ConcurrentDictionary<RedisKey, CacheItemWithInsertionTime>();

            SetupServerMock();
            SetupDatabaseMock();
            SetupConnectionMock();
            SetupCache();
        }

        #region Setup Methods

        private void SetupServerMock()
        {
            _serverMock.SetupGet(server => server.IsConnected).Returns(true);
            _serverMock.SetupGet(server => server.Features).Returns(new RedisFeatures(Version.Parse("3.0.504")));
            _serverMock
                .Setup(server => server.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(() => _fauxDatabase.Keys);
        }

        private void SetupDatabaseMock()
        {
            _databaseMock.SetupGet(database => database.Database).Returns(0);
            _databaseMock.Setup(database => database.KeyIdleTime(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns<RedisKey, CommandFlags>((key, flags) => DateTime.Now - _fauxDatabase[key].InsertionTime);
            
            void PutInFauxDatabase(RedisKey key, CacheItemWithInsertionTime item)
            {
                if (_fauxDatabase.ContainsKey(key))
                {
                    _fauxDatabase[key] = item;
                }
                else
                {
                    _fauxDatabase.Add(key, item);
                }
            }
            
            // RedisCacheHandle uses ScriptEvaluate (in versions newer than 2.5.7) and HashSet/HashGet (otherwise) to
            // access the cache in the Put and Get methods. Below, we mock the behavior of Redis with our own faux
            // database, by setting up callbacks and return values in response to invocations of said methods.

            // Put
            _databaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values != null), It.IsAny<CommandFlags>()))
                .Callback<byte[], RedisKey[], RedisValue[], CommandFlags>((hash, keys, values, flags) =>
                    PutInFauxDatabase(keys[0], new CacheItemWithInsertionTime(values)));

            _databaseMock
                .Setup(database => database.HashSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, RedisValue, When, CommandFlags>((key, hash, value, when, flags) =>
                    PutInFauxDatabase(key, new CacheItemWithInsertionTime(value)))
                .Returns(true);

            _databaseMock
                .Setup(database =>
                    database.HashSet(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, HashEntry[], CommandFlags>((key, hashEntries, flags) =>
                    PutInFauxDatabase(key, new CacheItemWithInsertionTime(_fauxDatabase[key].Values, hashEntries)));

            // Get
            _databaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values == null), It.IsAny<CommandFlags>()))
                .Returns<byte[], RedisKey[], RedisValue[], CommandFlags>((hash, keys, values, flags) =>
                    RedisResult.Create(_fauxDatabase[keys[0]].Values));

            _databaseMock
                .Setup(database =>
                    database.HashGet(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
                .Returns<RedisKey, RedisValue[], CommandFlags>((key, values, flags) => _fauxDatabase[key].Values);

            // Remove
            _databaseMock
                .Setup(database => database.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, CommandFlags>((key, flags) => _fauxDatabase.Remove(key));
        }

        private void SetupConnectionMock()
        {
            _connectionMock.SetupGet(connection => connection.Configuration).Returns("connectionString");
            _connectionMock.Setup(connection => connection.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[1]);
            _connectionMock.Setup(connection => connection.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
                .Returns(_serverMock.Object);
            _connectionMock.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_databaseMock.Object);
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
            _fauxDatabase.Clear();
            _onRemoveByHandleInvocations.Clear();
            _cache.Dispose();

            // Clear the configurations dictionary to make sure a given configuration won't be used twice  
            var redisConfigurations = typeof(RedisConfigurations)
                .GetProperty("Configurations", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IDictionary<string, RedisConfiguration>;

            redisConfigurations?.Clear();
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
            var value = new DummyModel {Property = "property1"};
            var updatedValue = new DummyModel {Property = "property2"};

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
            var value = new DummyModel {Property = "property1"};

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
            var value = new DummyModel {Property = "property1"};

            _databaseMock
                .Setup(database => database.KeyIdleTime(key, It.IsAny<CommandFlags>()))
                .Returns((TimeSpan?) null);
            
            // Act
            _cache[key] = value;

            // In order to be sure that event was not invoked, we wait even longer in this test
            Wait(CLEANUP_INTERVAL * 2);

            // Assert
            CollectionAssert.IsEmpty(_onRemoveByHandleInvocations);
        }

        [Test]
        public void Dispose_HappyFlow_CleanupTimerIsDisposed()
        {
            // Act
            _cache.Dispose();
            
            // Assert
            Assert.Throws<ObjectDisposedException>(() => _cleanupTimer.Start());
        }

        private static void Wait(TimeSpan delay) => Task.Delay(delay).Wait();
        
        private static void Wait(double delay) => Task.Delay((int)delay).Wait();

        private static IEnumerable<(string, DummyModel)> GenerateDifferentCacheItems(int count) =>
            Enumerable.Repeat("key", count).Select((key, i) => (key + i, new DummyModel {Property = "property" + i}));
        
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