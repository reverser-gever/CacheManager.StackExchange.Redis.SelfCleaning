using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios.Scenarios
{
    public class SimpleSingleExpiredItemScenario : BaseSingleScenario<int>
    {
        private ICacheManager<int> _cacheManager;

        private CacheItem<int> _cacheItem;
        private readonly ICollection<CacheItemRemovedEventArgs> _actualRemovedCacheItems;

        protected override string ScenarioName => "Simple Single Expired Item";

        protected override string ScenarioDescription =>
            "Adding one item to redis, wait more than TTL and the item should be removed due to timeout";

        public SimpleSingleExpiredItemScenario(Func<ICacheManager<int>> createCacheManager, TimeSpan configuredTimeToLive)
            : base(createCacheManager, 1, configuredTimeToLive)
        {
            _actualRemovedCacheItems = new Collection<CacheItemRemovedEventArgs>();
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

            StartCacheHandles(_cacheManager);
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

            IEnumerable<string> removedCacheItemsLinesToPrint = _actualRemovedCacheItems
                .Select(args => $"   Key [{args.Key}], value [{args.Value}], due to [{args.Reason}]");
            builder.AppendJoin(Environment.NewLine, removedCacheItemsLinesToPrint);

            return builder.ToString();
        }

        protected override void Dispose()
        {
            _cacheManager.Dispose();
        }
    }
}