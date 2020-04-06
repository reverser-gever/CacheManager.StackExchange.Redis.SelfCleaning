using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples.Scenarios
{
    public class SelfCleaningHermeticityScenario : BaseSingleScenario<double>
    {
        private ICacheManager<double>[] _cacheManagers;
        private long[] _cacheManagersAdditionCounter;

        private List<CacheItem<double>> _expectedRemovedCacheItems;
        private List<CacheItemRemovedEventArgs> _actualRemovedCacheItems;
        private Random _rn;

        public SelfCleaningHermeticityScenario(Func<ICacheManager<double>> createCacheManager,
            int numberOfCacheManagerInstances, TimeSpan configuredTimeToLive)
            : base(createCacheManager, "Hermeticity", numberOfCacheManagerInstances,
                $"{numberOfCacheManagerInstances} instances of CacheManagers. " +
                $"Send 10000 messages to them randomly, and expect to get 10000 removed cacheItems. " +
                $"Also, zero exceptions is expected."
                , configuredTimeToLive)
        {
            _expectedRemovedCacheItems = new List<CacheItem<double>>();
            _actualRemovedCacheItems = new List<CacheItemRemovedEventArgs>();

            _cacheManagers = new ICacheManager<double>[NumberOfCacheManagerInstances];
            _cacheManagersAdditionCounter = new long[NumberOfCacheManagerInstances];

            _rn = new Random(DateTime.Now.Millisecond);
        }

        protected override void RunScenarioContent()
        {
            InitCacheManagers();
            
            for (int i = 0; i < 1000; i++)
            {
                int chosenCacheManagerIndex = _rn.Next(0, _cacheManagers.Length);
                var cacheManger = _cacheManagers[chosenCacheManagerIndex];

                var cacheItem = new CacheItem<double>(i.ToString(), i);

                cacheManger.Add(cacheItem);

                // For detailed result:
                _expectedRemovedCacheItems.Add(cacheItem);
                _cacheManagersAdditionCounter[chosenCacheManagerIndex]++;
            }


            Utilities.Wait(ConfiguredTimeToLive * 3);
        }

        private void InitCacheManagers()
        {
            for (int i = 0; i < NumberOfCacheManagerInstances; i++)
            {
                var cacheManager = CreateCacheManager();

                cacheManager.OnRemoveByHandle += (sender, args) => _actualRemovedCacheItems.Add(args);
                _cacheManagers[i] = cacheManager;

                StartStartablesCacheHandles(cacheManager);
            }
        }

        protected override string GetScenarioResults()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("Number of messages processed by every cacheManager instance:");
            for (int i = 0; i < NumberOfCacheManagerInstances; i++)
            {
                builder.AppendLine($"   Instance [{i}] processed {_cacheManagersAdditionCounter[i]} messages.");
            }

            // Expected
            builder.AppendLine("Expected: 10000 cacheItem were removed");

            //Actual
            builder.AppendLine($"Actual: {_actualRemovedCacheItems.Count} cacheItems were removed");

            foreach (var removedCacheItem in _actualRemovedCacheItems)
            {
                if (removedCacheItem.Reason != CacheItemRemovedReason.Expired)
                {
                    builder.AppendLine(
                        $"   Weird RemovalReason - key [{removedCacheItem.Key}], value [{removedCacheItem.Value}], due to [{removedCacheItem.Reason}]");
                }
            }

            return builder.ToString();
        }

        protected override void Dispose()
        {
            foreach (var cacheManager in _cacheManagers)
            {
                cacheManager.Dispose();
            }
        }
    }
}