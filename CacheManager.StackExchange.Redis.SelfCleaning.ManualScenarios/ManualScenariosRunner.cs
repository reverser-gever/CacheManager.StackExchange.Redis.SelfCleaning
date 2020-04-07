using System;
using System.Collections.Generic;
using System.Reflection;
using CacheManager.Core;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios.Scenarios;
using StackExchange.Redis;
using static CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios.Utilities;

namespace CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios
{
    public class ManualScenariosRunner
    {
        private const string DEFAULT_CLEAN_INTERVAL_IN_MS = "1000";
        private TimeSpan _cleanupInterval;
        private const string DEFAULT_TIME_TO_LIVE_IN_MS = "3000";
        private TimeSpan _timeToLive;
        private const string DEFAULT_CONNECTION_STRING = "localhost:6379";
        private string _connectionString;

        public void Run()
        {
            Console.WriteLine("Hello to you dear monsieur/mademoiselle, welcome to our Reidis-Mania! Lets run some weird scenarios!");
            Console.WriteLine();
            GetParametersFromUser();

            RunConfiguredScenarios();

            Console.WriteLine("\n\n\n Done running scenarios, press any key to exit. See you next time!");
            Console.Read();
        }

        private void GetParametersFromUser()
        {
            _connectionString =
                ReadInputOrDefault("Connection String", DEFAULT_CONNECTION_STRING);

            string cleanupIntervalMilliseconds = ReadInputOrDefault(
                "Cleanup Interval", DEFAULT_CLEAN_INTERVAL_IN_MS, "ms");
            
            _cleanupInterval = TimeSpan.FromMilliseconds(double.Parse(cleanupIntervalMilliseconds));

            string slidingExpirationSeconds = ReadInputOrDefault(
                "Sliding Expiration", DEFAULT_TIME_TO_LIVE_IN_MS, "ms");

            _timeToLive = TimeSpan.FromMilliseconds(double.Parse(slidingExpirationSeconds));

            Console.WriteLine();
        }

        private string ReadInputOrDefault(string fieldName, string defaultValue, string measurementsUnits = "")
        {
            Console.WriteLine($"Default configuration for field {fieldName} - [{defaultValue}{measurementsUnits}]");
            var userValue = ReadInput("different value (same measurements units) in order to change it, or just press ENTER if this is cool");

            Console.WriteLine();

            return userValue == string.Empty ? defaultValue : userValue;
        }

        private void RunConfiguredScenarios()
        {
            Console.WriteLine("\n\n\n ******************************* \n\n\n");
            new SimpleSingleExpiredItemScenario(CreateCacheManager<int>, _timeToLive).RunScenario();

            Console.WriteLine("\n\n\n ******************************* \n\n\n");
            new SelfCleaningHermeticityScenario(CreateCacheManager<double>, 1, _timeToLive).RunScenario();

            //RunSingleScenario(new SelfCleaningHermeticityScenario(CreateCacheManager<double>, 5, _timeToLive).RunScenario);
        }

        private ICacheManager<T> CreateCacheManager<T>()
        {
            // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
            var cacheManager = CacheFactory.Build<T>(part => part
               .WithProtoBufSerializer()
               .WithDefaultSelfCleaningRedisConfiguration(_connectionString, _cleanupInterval, _timeToLive,
                   out string configurationKey)
               .WithSelfCleaningRedisCacheHandle(configurationKey));


            #region Clear Static RedisConfiguration and stuff using Reflection (ugly...)
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
            #endregion


            return cacheManager;
        }

        //private ICacheManager<T> CreateCacheManagerWithCleanupIntervalAndTimeToLive<T>(
        //    TimeSpan cleanupInterval, TimeSpan timeToLive)
        //{

        //    // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
        //    return CacheFactory.Build<T>(part => part
        //        .WithProtoBufSerializer()
        //        .WithDefaultSelfCleaningRedisConfiguration(_connectionString, cleanupInterval, timeToLive,
        //            out string configurationKey)
        //        .WithSelfCleaningRedisCacheHandle(configurationKey));
        //}
    }
}