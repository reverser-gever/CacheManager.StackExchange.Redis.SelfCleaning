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

        protected int NumberOfCacheManagerInstances;

        protected readonly TimeSpan ConfiguredTimeToLive;
        protected Func<ICacheManager<T>> CreateCacheManager;

        private Exception _exception;
        //protected Func<ICacheManager<T>, TimeSpan, TimeSpan> CreateCacheManagerCustomConfig;

        protected BaseSingleScenario(Func<ICacheManager<T>> createCacheManager, //Func<ICacheManager<T>, TimeSpan, TimeSpan> createCacheManagerCustomConfig, 
            int numberOfCacheManagerInstances, TimeSpan configuredTimeToLive)
        {
            CreateCacheManager = createCacheManager;
            //CreateCacheManagerCustomConfig = createCacheManagerCustomConfig;
            ConfiguredTimeToLive = configuredTimeToLive;
            NumberOfCacheManagerInstances = numberOfCacheManagerInstances;
        }

        public void RunScenario()
        {
            Console.WriteLine($"Running {ScenarioName} scenario, with use {NumberOfCacheManagerInstances} instances of self cleaning redis cache manager");
            Console.WriteLine($"Description: {ScenarioDescription}");

            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            try
            {
                RunScenarioContent();
            }
            catch (Exception e)
            {
                _exception = e;
            }

            stopwatch.Stop();

            Console.WriteLine("\n.\n.\n.\n");

            Console.WriteLine($"Total scenario execution last for [{stopwatch.ElapsedMilliseconds}] milliseconds");

            Console.WriteLine($"Results: {GetScenarioResults()}");

            if (_exception != null)
            {
                Console.WriteLine($"Exceptione was thrown during scenario");
                Console.WriteLine($"\n   EXCEPTION: {_exception}");
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