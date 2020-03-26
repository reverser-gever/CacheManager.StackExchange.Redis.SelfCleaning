using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Timers;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    [TestFixture]
    public class SelfCleaningRedisIntegrationTests
    {
        private const double TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD = 100;  
        
        private Mock<IServer> _serverMock;
        private Mock<IDatabase> _databaseMock;
        private Mock<IConnectionMultiplexer> _connectionMock;
        
        private IDictionary<RedisKey, (RedisValue[] Values, DateTime InsertionTime)> _fauxDatabase;

        private TimeSpan _timeToLive;
        private ICacheManager<DummyModel> _cache;

        private ICollection<(CacheItemRemovedEventArgs Args, DateTime Time)> _onRemoveByHandleInvocations;

        [OneTimeSetUp]
        public void Setup()
        {
            _serverMock = new Mock<IServer>();
            _databaseMock = new Mock<IDatabase>();
            _connectionMock = new Mock<IConnectionMultiplexer>();

            _fauxDatabase = new ConcurrentDictionary<RedisKey, (RedisValue[], DateTime)>();
            
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
                .Returns<int, RedisValue, int, long, int, CommandFlags>(
                    (db, pattern, pageSize, cursor, pageOffset, flags) => _fauxDatabase.Keys);
        }

        private void SetupDatabaseMock()
        {
            _databaseMock.SetupGet(database => database.Database).Returns(0);
            _databaseMock.Setup(database => database.KeyIdleTime(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns<RedisKey, CommandFlags>((key, flags) => DateTime.Now - _fauxDatabase[key].InsertionTime);
            
            void Put(byte[] hash, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
            {
                RedisValue[] newValues = {values[0], values[2], values[3], values[4], values[1], values[5], true};
                (RedisValue[], DateTime) tuple = (newValues, DateTime.Now);

                RedisKey key = keys[0];

                if (_fauxDatabase.ContainsKey(key))
                {
                    _fauxDatabase[key] = tuple;
                }
                else
                {
                    _fauxDatabase.Add(key, tuple);
                }
            }

            // Put
            _databaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values != null), It.IsAny<CommandFlags>()))
                .Callback<byte[], RedisKey[], RedisValue[], CommandFlags>(Put);

            // Get
            _databaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values == null), It.IsAny<CommandFlags>()))
                .Returns<byte[], RedisKey[], RedisValue[], CommandFlags>((hash, keys, values, flags) =>
                    RedisResult.Create(_fauxDatabase[keys[0]].Values));
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
            double cleanupInterval = 100;
            var cleanupTimer = new DefaultTimer(cleanupInterval);
            _timeToLive = TimeSpan.FromSeconds(3);

            _cache = CacheFactory.Build<DummyModel>(part => part
                .WithProtoBufSerializer()
                .WithSelfCleaningRedisConfiguration(_connectionMock.Object, cleanupTimer, _timeToLive,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));

            _onRemoveByHandleInvocations = new List<(CacheItemRemovedEventArgs, DateTime)>();
            _cache.OnRemoveByHandle += (sender, args) => _onRemoveByHandleInvocations.Add((args, DateTime.Now));

            _cache.CacheHandles.OfType<IStartable>().Single().Start();
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            _onRemoveByHandleInvocations.Clear();
        }
        
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cache.Dispose();
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void Put_AddSomeAmountOfKeys_OnRemoveByHandleCalledAfterTimeToLiveWithCorrectValueAndReason(int amountOfKeys)
        {
            // Arrange
            IEnumerable<(string, DummyModel)> keys = Enumerable.Repeat("key", amountOfKeys)
                .Select((key, i) => (key + i, new DummyModel {Property = "property" + i}));
            
            // Act
            var keysWithInsertionTimes = new List<(string, DummyModel, DateTime)>();
            
            foreach ((string key, DummyModel value) in keys)
            {
                _cache[key] = value;
                DateTime insertionTime = DateTime.Now;
                keysWithInsertionTimes.Add((key, value, insertionTime));
            }
            
            Task.Delay((int) (_timeToLive.TotalMilliseconds + TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD)).Wait();
            
            // Assert

            IDictionary<string, (CacheItemRemovedEventArgs Args, DateTime Time)> invocationsDictionary =
                _onRemoveByHandleInvocations.ToDictionary(tuple => tuple.Args.Key, tuple => (tuple.Args, tuple.Time));

            foreach ((string expectedKey, DummyModel expectedValue, DateTime insertionTime) in keysWithInsertionTimes)
            {
                CollectionAssert.Contains(invocationsDictionary.Keys, expectedKey);

                (CacheItemRemovedEventArgs args, DateTime removeTime) = invocationsDictionary[expectedKey];
                
                Assert.AreEqual(CacheItemRemovedReason.Expired, args.Reason);
                Assert.AreEqual(expectedValue, args.Value);

                double timeAlive = (removeTime - insertionTime).TotalMilliseconds;
                double differenceBetweenTimeAliveAndTimeToLive = Math.Abs(timeAlive - _timeToLive.TotalMilliseconds);
                Assert.LessOrEqual(differenceBetweenTimeAliveAndTimeToLive,
                    TIME_TO_LIVE_MILLISECONDS_DIFFERENCE_THRESHOLD);
            }
        }
    }
}