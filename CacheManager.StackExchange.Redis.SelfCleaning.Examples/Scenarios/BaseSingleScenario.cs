using System;
using System.Diagnostics;
using System.Linq;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Examples.Scenarios
{
    public abstract class BaseSingleScenario<T>
    {
        private string _scenarioName;
        private string _scenarioDescription;
        private int _numberOfCacheManagerInstances;

        protected readonly TimeSpan ConfiguredTimeToLive;
        protected Func<ICacheManager<T>> CreateCacheManager;
        //protected Func<ICacheManager<T>, TimeSpan, TimeSpan> CreateCacheManagerCustomConfig;

        protected BaseSingleScenario(Func<ICacheManager<T>> createCacheManager, //Func<ICacheManager<T>, TimeSpan, TimeSpan> createCacheManagerCustomConfig, 
            string scenarioName,
            int numberOfCacheManagerInstances, string scenarioDescription, TimeSpan configuredTimeToLive)
        {
            CreateCacheManager = createCacheManager;
            //CreateCacheManagerCustomConfig = createCacheManagerCustomConfig;
            _scenarioDescription = scenarioDescription;
            ConfiguredTimeToLive = configuredTimeToLive;
            _numberOfCacheManagerInstances = numberOfCacheManagerInstances;
            _scenarioName = scenarioName;
        }

        public void RunScenario()
        {
            Console.WriteLine($"Running {_scenarioName} scenario, with use {_numberOfCacheManagerInstances} instances of self cleaning redis cache manager");
            Console.WriteLine($"Description: {_scenarioDescription}");

            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            RunScenarioContent();
            stopwatch.Stop();

            Console.WriteLine("\n.\n.\n.\n");

            Console.WriteLine($"Total scenario execution last for [{stopwatch.ElapsedMilliseconds}] milliseconds");
            Console.WriteLine($"Results: {GetScenarioResults()}");
            Dispose();
        }

        protected void StartStartablesCacheHandles<T>(ICacheManager<T> cacheManager)
        {
            // Start the startable cache handles 
            foreach (IStartable startable in cacheManager.CacheHandles.OfType<IStartable>())
            {
                startable.Start();
            }
        }

        protected abstract void RunScenarioContent();
        protected abstract string GetScenarioResults();
        protected abstract void Dispose();
    }
}