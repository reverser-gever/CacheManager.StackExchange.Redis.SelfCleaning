using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class CacheItemWithInsertionTime
    {
        public RedisValue[] Values { get; }

        public DateTime InsertionTime { get; }

        private CacheItemWithInsertionTime()
        {
            InsertionTime = DateTime.Now;
        }

        public CacheItemWithInsertionTime(IReadOnlyList<RedisValue> values) : this()
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
        }

        public CacheItemWithInsertionTime(RedisValue value) : this()
        {
            Values = new[] {value};
        }

        public CacheItemWithInsertionTime(IEnumerable<RedisValue> values, IEnumerable<HashEntry> hashEntries)
            // Concatenate the previously received values with the values from the entries
            : this(values.Concat(hashEntries.Select(entry => entry.Value)).ToList())
        {
        }
    }
}