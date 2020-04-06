using System;
using System.Linq;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples
{
    public static class Utilities
    {

        public static string ReadInput(string name)
        {
            Console.WriteLine($"Enter {name}:");
            return Console.ReadLine();
        }

        public static void Wait(TimeSpan delay) => Task.Delay(delay).Wait();
    }
}