using ProtoBuf;

namespace CacheManager.StackExchange.Redis.SelfCleaning.Tests
{
    [ProtoContract]
    public class DummyModel
    {
        [ProtoMember(1)]
        public string Property { get; set; }

        public override bool Equals(object obj)
        {
            return obj != null && obj is DummyModel other && Property.Equals(other.Property);
        }

        public override int GetHashCode()
        {
            return (Property != null ? Property.GetHashCode() : 0);
        }
    }
}