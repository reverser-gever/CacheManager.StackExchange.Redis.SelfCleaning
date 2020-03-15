using System;
using System.Timers;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Timers
{
    public class DefaultTimer : ITimer
    {
        private readonly Timer _timer;
        
        public event Action Elapsed;

        public DefaultTimer(double interval)
        {
            _timer = new Timer(interval);
        }
        
        public void Start()
        {
            _timer.Elapsed += OnElapsed;
            _timer.Start();
        }

        public void Dispose()
        {
            _timer.Elapsed -= OnElapsed;
            _timer.Dispose();
        }

        private void OnElapsed(object sender, ElapsedEventArgs args)
        {
            Elapsed?.Invoke();
        }
    }
}