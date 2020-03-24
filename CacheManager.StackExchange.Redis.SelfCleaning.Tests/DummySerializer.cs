using System;
using System.Collections.Generic;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class DummySerializer : ICacheSerializer
    {
        public static ICollection<object> SerializeInvocations { get; } = new List<object>();
        public static ICollection<(byte[], Type)> DeserializeInvocations { get; } = new List<(byte[], Type)>();
        public static ICollection<object> SerializeCacheItemInvocations { get; } = new List<object>();
        public static ICollection<(byte[], Type)> DeserializeCacheItemInvocations { get; } = new List<(byte[], Type)>();

        public byte[] Serialize<T>(T value)
        {
            SerializeInvocations.Add(value);
            return null;
        }

        public object Deserialize(byte[] data, Type target)
        {
            DeserializeInvocations.Add((data, target));
            return null;
        }

        public byte[] SerializeCacheItem<T>(CacheItem<T> value)
        {
            SerializeCacheItemInvocations.Add(value);
            return null;
        }

        public CacheItem<T> DeserializeCacheItem<T>(byte[] value, Type valueType)
        {
            DeserializeCacheItemInvocations.Add((value, valueType));
            return null;
        }
    }
}