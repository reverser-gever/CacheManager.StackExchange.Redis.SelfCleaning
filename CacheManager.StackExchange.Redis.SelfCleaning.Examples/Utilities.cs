using System;
using System.Threading.Tasks;

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