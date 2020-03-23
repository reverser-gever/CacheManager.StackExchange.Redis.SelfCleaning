using CacheManager.Core;

namespace CacheManager.StackExchange.Redis.SelfCleaning
{
    public interface ISelfCleaningRedisConfigurationProvider
    {
        SelfCleaningRedisConfiguration GetConfiguration(CacheHandleConfiguration configuration);
    }
}