namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    public class DummyModel
    {
        public string Property { get; set; }

        public override bool Equals(object obj)
        {
            return obj != null && obj is DummyModel other && Property.Equals(other.Property);
        }

        public override int GetHashCode()
        {
            return Property != null ? Property.GetHashCode() : 0;
        }
    }
}