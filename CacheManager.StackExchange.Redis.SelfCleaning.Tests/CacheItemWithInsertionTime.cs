using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class CacheItemWithInsertionTime
    {
        public RedisValue[] Values { get; }

        public DateTime InsertionTime { get; }

        public CacheItemWithInsertionTime(IReadOnlyList<RedisValue> values)
        {
            Values = new[]
            {
                values[0], // Value 
                values[2], // Expiration mode
                values[3], // Expiration timeout (milliseconds)
                values[4], // Created UTC (ticks)
                values[1], // Assembly Qualified Name
                values[5], // Uses expiration defaults
                true
            };
            
            InsertionTime = DateTime.Now;
        }
    }
}