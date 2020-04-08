using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios.Scenarios
{
    public class SelfCleaningHermeticityScenario : BaseSingleScenario<double>
    {
        private const int NUMBER_OF_MESSAGES = 10000;
        private readonly ICacheManager<double>[] _cacheManagers;
        private readonly long[] _cacheManagersAdditionCounter;

        private readonly ICollection<CacheItemRemovedEventArgs> _actualRemovedCacheItems;
        private readonly Random _random;

        protected override string ScenarioName => "Hermeticity";
        protected override string ScenarioDescription => 
            $"{NumberOfCacheManagerInstances} instances of CacheManagers. " +
            $"Send {NUMBER_OF_MESSAGES} messages to them randomly, and expect to get {NUMBER_OF_MESSAGES} removed cacheItems.";

        public SelfCleaningHermeticityScenario(int numberOfCacheManagerInstances,
            TimeSpan configuredTimeToLive, Func<ICacheManager<double>> createCacheManager)
            : base(numberOfCacheManagerInstances, configuredTimeToLive
                , createCacheManager)
        {
            _actualRemovedCacheItems = new Collection<CacheItemRemovedEventArgs>();

            _cacheManagers = new ICacheManager<double>[NumberOfCacheManagerInstances];
            _cacheManagersAdditionCounter = new long[NumberOfCacheManagerInstances];

            _random = new Random();
        }

        protected override void RunScenarioContent()
        {
            InitCacheManagers();

            for (int i = 0; i < NUMBER_OF_MESSAGES; i++)
            {
                int chosenCacheManagerIndex = _random.Next(_cacheManagers.Length);
                var cacheManger = _cacheManagers[chosenCacheManagerIndex];

                var cacheItem = new CacheItem<double>(i.ToString(), i);

                cacheManger.Add(cacheItem);

                // For detailed result:
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

                StartCacheHandles(cacheManager);
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
            builder.AppendLine($"Expected: {NUMBER_OF_MESSAGES} cacheItem were removed");

            //Actual
            builder.AppendLine($"Actual: {_actualRemovedCacheItems.Count} cacheItems were removed");

            IEnumerable<string> weirdRemovalReasonLinesToPrint = _actualRemovedCacheItems
                .Where(removedCacheItem => removedCacheItem.Reason != CacheItemRemovedReason.Expired)
                .Select(args => $"   Weird RemovalReason - key [{args.Key}], value [{args.Value}], due to [{args.Reason}]");

            builder.AppendJoin(Environment.NewLine, weirdRemovalReasonLinesToPrint);

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