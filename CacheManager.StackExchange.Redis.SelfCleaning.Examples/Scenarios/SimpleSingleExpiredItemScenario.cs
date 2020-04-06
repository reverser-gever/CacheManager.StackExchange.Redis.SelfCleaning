using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples.Scenarios
{
    public class SimpleSingleExpiredItemScenario : BaseSingleScenario<int>
    {
        private ICacheManager<int> _cacheManager;

        private CacheItem<int> _cacheItem;
        private List<CacheItemRemovedEventArgs> _actualRemovedCacheItems;

        public SimpleSingleExpiredItemScenario(Func<ICacheManager<int>> createCacheManager, TimeSpan configuredTimeToLive)
            : base(createCacheManager, "Simple Single Expired Item", 1,
                "Adding one item to redis, wait more than TTL and the item should be removed due to timeout", configuredTimeToLive)
        {
            _actualRemovedCacheItems = new List<CacheItemRemovedEventArgs>();
        }

        protected override void RunScenarioContent()
        {
            InitCacheManager();

            _cacheItem = new CacheItem<int>("Moishe", 23958);
            _cacheManager.Add(_cacheItem);

            Utilities.Wait(ConfiguredTimeToLive * 3);
        }

        private void InitCacheManager()
        {
            _cacheManager = CreateCacheManager();

            _cacheManager.OnRemoveByHandle += (sender, args) => _actualRemovedCacheItems.Add(args);

            StartStartablesCacheHandles(_cacheManager);
        }

        protected override string GetScenarioResults()
        {
            StringBuilder builder = new StringBuilder();

            // Expected
            builder.AppendLine("Expected: 1 cacheItem was removed:");
            builder.AppendLine(
                $"   Key [{_cacheItem.Key}], value [{_cacheItem.Value}], due to [{nameof(CacheItemRemovedReason.Expired)}]");

            //Actual
            builder.AppendLine($"Actual: {_actualRemovedCacheItems.Count} cacheItems were removed:");
            foreach (var removedCacheItem in _actualRemovedCacheItems)
            {
                builder.AppendLine(
                    $"   Key [{removedCacheItem.Key}], value [{removedCacheItem.Value}], due to [{removedCacheItem.Reason}]");
            }

            return builder.ToString();
        }

        protected override void Dispose()
        {
            _cacheManager.Dispose();
        }
    }
}