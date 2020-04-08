using System;
using System.Linq;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples
{
    public static class Program
    {
        public static void Main()
        {
            // Get cache parameters from user
            string connectionString = ReadInput("Connection String");
            double cleanupIntervalMilliseconds = double.Parse(ReadInput("Cleanup Interval (ms)"));
            double slidingExpirationSeconds = double.Parse(ReadInput("Sliding Expiration (sec.)"));

            TimeSpan cleanupInterval = TimeSpan.FromMilliseconds(cleanupIntervalMilliseconds);
            TimeSpan slidingExpiration = TimeSpan.FromSeconds(slidingExpirationSeconds);

            // Build the cache with a self-cleaning redis handle (as well as a ProtoBuf serializer)
            ICacheManager<string> cacheManager = CacheFactory.Build<string>(part => part
                .WithProtoBufSerializer()
                .WithDefaultSelfCleaningRedisConfiguration(connectionString, cleanupInterval, slidingExpiration,
                    out string configurationKey)
                .WithSelfCleaningRedisCacheHandle(configurationKey));

            // Subscribe to OnRemoveByHandle, the event notifying about expiration
            cacheManager.OnRemoveByHandle += (sender, args) =>
                Console.WriteLine($"Key \"{args.Key}\" was removed ({args.Reason}), its value was \"{args.Value}\"");

            // Start the startable cache handles 
            foreach (IStartable startable in cacheManager.CacheHandles.OfType<IStartable>())
            {
                startable.Start();
            }

            InputLoop(cacheManager);

            cacheManager.Dispose();
        }
        private static void InputLoop(ICache<string> cache)
        {
            Console.WriteLine("Example running.");
            Console.WriteLine("To add a key, press '+'.");
            Console.WriteLine("To stop, press 'q'.");

            var exitFlag = false;
            while (!exitFlag)
            {
                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.Q:
                        exitFlag = true;
                        break;
                    case ConsoleKey.OemPlus:
                        string key = ReadInput("key");
                        string value = ReadInput("value");
                        cache[key] = value;
                        break;
                }
            }
        }

        private static string ReadInput(string name)
        {
            Console.WriteLine($"Enter {name}:");
            return Console.ReadLine();
        }
    }
}