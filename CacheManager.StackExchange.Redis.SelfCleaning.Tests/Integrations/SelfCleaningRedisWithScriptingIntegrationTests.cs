using System;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests.Integrations
{
    [TestFixture]
    public class SelfCleaningRedisWithScriptingIntegrationTests : SelfCleaningRedisIntegrationTestsBase
    {
        protected override Version RedisVersion => Version.Parse("3.0.504");

        protected override void SetupDatabaseMockByVersion()
        {
            // For newer versions of Redis, RedisCacheHandle uses ScriptEvaluate to access the cache in the Put and Get
            // methods. Below, we mock the behavior of Redis with our own faux database, by setting up callbacks and
            // return values in response to invocations of said method.
            
            // Put
            DatabaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values != null), It.IsAny<CommandFlags>()))
                .Callback<byte[], RedisKey[], RedisValue[], CommandFlags>((hash, keys, values, flags) =>
                    PutInFauxDatabase(keys[0], new CacheItemWithInsertionTime(values)));
            
            // Get
            DatabaseMock
                .Setup(database => database.ScriptEvaluate(It.IsAny<byte[]>(), It.IsAny<RedisKey[]>(),
                    It.Is<RedisValue[]>(values => values == null), It.IsAny<CommandFlags>()))
                .Returns<byte[], RedisKey[], RedisValue[], CommandFlags>((hash, keys, values, flags) =>
                    RedisResult.Create(FauxDatabase[keys[0]].Values));
        }
    }
}