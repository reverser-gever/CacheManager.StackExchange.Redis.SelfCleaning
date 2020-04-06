using System;
using System.Linq;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Examples;
using CacheManager.StackExchange.Redis.SelfCleaning.Examples.Scenarios;
using static CacheManager.StackExchange.Redis.SelfCleaning.Examples.Utilities;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples
{
    public class ManualScenariosRunner
    {
        private TimeSpan _cleanupInterval;
        private TimeSpan _slidingExpiration;
        private string _connectionString;

        public void Run()
        {
            Console.WriteLine("Hello to you dear monsieur/mademoiselle, welcome to our Reidis-Mania! Lets run some weird scenarios!");
            GetParametersFromUser();

            new SimpleSingleExpiredItemScenario(CreateCacheManager<int>, _slidingExpiration).RunScenario();

            Console.WriteLine("\n\n\n Done running scenarios, press any key to exit. See you next time!");
        }

        private ICacheManager<T> CreateCacheManagerWithCleanupIntervalAndTimeToLive<T>(
            TimeSpan cleanupInterval, TimeSpan timeToLive)
        {

            // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
            return CacheFactory.Build<T>(part => part
                .WithProtoBufSerializer()
                .WithDefaultSelfCleaningRedisConfiguration(_connectionString, cleanupInterval, timeToLive,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));
        }

        private ICacheManager<T> CreateCacheManager<T>()
        {

            // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
            return CacheFactory.Build<T>(part => part
                .WithProtoBufSerializer()
                .WithDefaultSelfCleaningRedisConfiguration(_connectionString, _cleanupInterval, _slidingExpiration,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));
        }

        private void GetParametersFromUser()
        {
            // Get cache parameters from user
            var userName = ReadInput("your name please");
            Console.WriteLine($"Hahahaha {userName}, I dont really have what to do with your name ;) . OK, lets get serious.");


            _connectionString = ReadInput("Connection String");
            double cleanupIntervalMilliseconds = double.Parse(ReadInput("Cleanup Interval (ms)"));
            double slidingExpirationSeconds = double.Parse(ReadInput("Sliding Expiration (sec.)"));

            _cleanupInterval = TimeSpan.FromMilliseconds(cleanupIntervalMilliseconds);
            _slidingExpiration = TimeSpan.FromSeconds(slidingExpirationSeconds);
        }
    }
}