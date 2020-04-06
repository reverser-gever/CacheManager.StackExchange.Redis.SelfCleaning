using System;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests.Integrations
{
    [TestFixture]
    public class SelfCleaningRedisWithoutScriptingIntegrationTests : SelfCleaningRedisIntegrationTestsBase
    {
        protected override Version RedisVersion => Version.Parse("2.0.0");
        
        protected override void SetupDatabaseMockByVersion()
        {
            // For older versions of Redis, RedisCacheHandle uses HashSet and HashGet to access the cache in the Put and
            // Get methods. Below, we mock the behavior of Redis with our own faux database, by setting up callbacks and
            // return values in response to invocations of said methods.
            
            // Put
            DatabaseMock
                .Setup(database => database.HashSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, RedisValue, When, CommandFlags>((key, hash, value, when, flags) =>
                    PutInFauxDatabase(key, new CacheItemWithInsertionTime(value)))
                .Returns(true);

            DatabaseMock
                .Setup(database =>
                    database.HashSet(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, HashEntry[], CommandFlags>((key, hashEntries, flags) =>
                    PutInFauxDatabase(key, new CacheItemWithInsertionTime(FauxDatabase[key].Values, hashEntries)));

            // Get
            DatabaseMock
                .Setup(database =>
                    database.HashGet(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
                .Returns<RedisKey, RedisValue[], CommandFlags>((key, values, flags) => FauxDatabase[key].Values);
        }
    }
}