using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CacheManager.Core;
using CacheManager.StackExchange.Redis.SelfCleaning.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning.ManualScenarios.Scenarios
{
    public abstract class BaseSingleScenario<T>
    {
        protected abstract string ScenarioName { get; }
        protected abstract string ScenarioDescription { get; }

        protected readonly int NumberOfCacheManagerInstances;

        protected readonly TimeSpan ConfiguredTimeToLive;
        protected readonly Func<ICacheManager<T>> CreateCacheManager;

        protected BaseSingleScenario(int numberOfCacheManagerInstances, 
            TimeSpan configuredTimeToLive, Func<ICacheManager<T>> createCacheManager)
        {
            NumberOfCacheManagerInstances = numberOfCacheManagerInstances;
            ConfiguredTimeToLive = configuredTimeToLive;
            CreateCacheManager = createCacheManager;
        }

        public void RunScenario()
        {
            Console.WriteLine($"Running {ScenarioName} scenario, with use {NumberOfCacheManagerInstances} instances of self cleaning redis cache manager");
            Console.WriteLine($"Description: {ScenarioDescription}");

            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            Exception exception = null;
            try
            {
                RunScenarioContent();
            }
            catch (Exception e)
            {
                exception = e;
            }

            stopwatch.Stop();

            Console.WriteLine("\n.\n.\n.\n");

            Console.WriteLine($"Total scenario execution last for [{stopwatch.ElapsedMilliseconds}] milliseconds");

            Console.WriteLine($"Results: {GetScenarioResults()}");

            if (exception != null)
            {
                Console.WriteLine($"Exceptione was thrown during scenario");
                Console.WriteLine($"   EXCEPTION: {exception}");
            }
            else
            {
                Console.WriteLine($"No exception was thrown during the scenario");
            }
            
            Dispose();
        }

        protected void StartCacheHandles(ICacheManager<T> cacheManager)
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