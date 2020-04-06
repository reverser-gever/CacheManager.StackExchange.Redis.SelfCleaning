using System;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class OnRemoveByHandleInvocation
    {
        public CacheItemRemovedEventArgs Args { get; set; }

        public DateTime RemovalTime { get; set; }
    }
}