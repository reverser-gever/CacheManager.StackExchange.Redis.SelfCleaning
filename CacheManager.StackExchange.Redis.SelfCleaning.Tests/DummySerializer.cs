using System;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class DummySerializer : ICacheSerializer
    {
        public event Action<object> SerializeCalled;
        public event Action<byte[], Type> DeserializeCalled;
        public event Action<object> SerializeCacheItemCalled;
        public event Action<byte[], Type> DeserializeCacheItemCalled;

        public byte[] Serialize<T>(T value)
        {
            SerializeCalled?.Invoke(value);
            return null;
        }

        public object Deserialize(byte[] data, Type target)
        {
            DeserializeCalled?.Invoke(data, target);
            return null;
        }

        public byte[] SerializeCacheItem<T>(CacheItem<T> value)
        {
            SerializeCacheItemCalled?.Invoke(value);
            return null;
        }

        public CacheItem<T> DeserializeCacheItem<T>(byte[] value, Type valueType)
        {
            DeserializeCacheItemCalled?.Invoke(value, valueType);
            return null;
        }
    }
}