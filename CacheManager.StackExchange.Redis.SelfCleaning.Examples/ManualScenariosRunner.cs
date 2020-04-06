using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using CacheManager.Core;
using CacheManager.Redis;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Examples;
using CacheManager.StackExchange.Redis.SelfCleaning.Examples.Scenarios;
using StackExchange.Redis;
using static CacheManager.StackExchange.Redis.SelfCleaning.Examples.Utilities;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples
{
    public class ManualScenariosRunner
    {
        private TimeSpan _cleanupInterval = TimeSpan.FromSeconds(0.1);
        private TimeSpan _slidingExpiration = TimeSpan.FromSeconds(1);
        private string _connectionString = "localhost:6379";

        public void Run()
        {
            Console.WriteLine("Hello to you dear monsieur/mademoiselle, welcome to our Reidis-Mania! Lets run some weird scenarios!");
            GetParametersFromUser();

            RunConfiguredScenarios();
            
            Console.WriteLine("\n\n\n Done running scenarios, press any key to exit. See you next time!");
            Console.Read();
        }

        private void RunConfiguredScenarios()
        {
            RunAnotherScenario(new SimpleSingleExpiredItemScenario(CreateCacheManager<int>, _slidingExpiration).RunScenario);
            RunAnotherScenario(new SelfCleaningHermeticityScenario(CreateCacheManager<double>, 1, _slidingExpiration).RunScenario);
            //RunAnotherScenario(new SelfCleaningHermeticityScenario(CreateCacheManager<double>, 5, _slidingExpiration).RunScenario);
        }

        private void RunAnotherScenario(Action scenario)
        {
            Console.WriteLine("\n\n\n ******************************* \n\n\n");
            scenario();
        }

        private ICacheManager<T> CreateCacheManager<T>()
        {
            // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
            var cacheManager =  CacheFactory.Build<T>(part => part
                .WithProtoBufSerializer()
                .WithDefaultSelfCleaningRedisConfiguration(_connectionString, _cleanupInterval, _slidingExpiration,
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

        private void GetParametersFromUser()
        {
            Console.WriteLine($"Default configuration:\n" +
                              $" Connection String - [{_connectionString}]\n" +
                              $" Sliding Expiration (Time To Live) - [{_slidingExpiration.Milliseconds}] ms\n" +
                              $" CleanupInterval - [{_cleanupInterval.Milliseconds}] ms");

            var userChoice = ReadInput("something in order to change it, or just press ENTER if this is cool");

            if (userChoice == string.Empty)
            {
                Console.WriteLine();
                return;
            }

            // Get cache parameters from user
            var userName = ReadInput("your name please");
            Console.WriteLine($"Hahahaha {userName}, I dont really have what to do with your name ;) . OK, lets get serious.");


            _connectionString = ReadInput("Connection String");
            double cleanupIntervalMilliseconds = double.Parse(ReadInput("Cleanup Interval (ms)"));
            double slidingExpirationSeconds = double.Parse(ReadInput("Sliding Expiration (sec.)"));

            _cleanupInterval = TimeSpan.FromMilliseconds(cleanupIntervalMilliseconds);
            _slidingExpiration = TimeSpan.FromSeconds(slidingExpirationSeconds);

            Console.WriteLine();
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