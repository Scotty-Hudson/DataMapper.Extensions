using System;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    public enum Status { Active, Inactive }

    /// <summary>Every data type the mapper can produce, plus a member mapped by attribute.</summary>
    public class Customer
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }

        /// <summary>Mapped from the column named ZIP, which tests case insensitive matching.</summary>
        public int Zip { get; set; }

        public decimal? RewardsPoints { get; set; }

        [Column("CustomerKey")]
        public Guid Key { get; set; }

        public Status Status { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public double Score { get; set; }
        public short TinyValue { get; set; }
        public byte ByteValue { get; set; }
        public float FloatValue { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Column names that do not match the C# members.</summary>
    public class Account
    {
        [Column("account_id")]
        public long Id { get; set; }

        [Column("display_name")]
        public string DisplayName { get; set; }

        [DataMapper.Extensions.IgnoreMember]
        public string Secret { get; set; }

        public Status Status { get; set; }
        public Guid Key { get; set; }
        public int? Balance { get; set; }
        public TimeSpan Duration { get; set; }

        // Public fields are mapped as well as properties.
        public string Note;
    }

    /// <summary>A POCO with fields instead of properties.</summary>
    public class FieldOnlyRecord
    {
        public long Id;
        public string Name;
    }

    /// <summary>A member with no setter is skipped.</summary>
    public class ReadOnlyPoco
    {
        public long Id { get; }
        public string Name { get; set; }
    }

    /// <summary>A POCO with nothing to map.</summary>
    public class NoMemberPoco
    {
    }

    /// <summary>No parameterless constructor, so New&lt;T&gt; uses GetUninitializedObject.</summary>
    public class NoDefaultConstructorPoco
    {
        public NoDefaultConstructorPoco(long id) => Id = id;

        public long Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>Property initializers run because the type has a parameterless constructor.</summary>
    public class InitializerPoco
    {
        public long Id { get; set; }
        public string Tag { get; set; } = "default-tag";
    }

    public class InitOnlyPoco
    {
        public long Id { get; init; }
        public string Name { get; init; }
    }

    /// <summary>A POCO with only strings, so empty string values can be asserted on their own.</summary>
    public class StringOnlyPoco
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    // ---------- records ----------

    public record PositionalRecord(long Id, string Name);

    public record RecordWithInitializer(long Id, string Name)
    {
        public string Tag { get; set; } = "default-tag";
    }

    public record RecordWithDefaultConstructor
    {
        public long Id { get; set; }
        public string Tag { get; set; } = "default-tag";
    }

    public record RecordWithAttributes
    {
        [Column("account_id")]
        public long Id { get; init; }

        [IgnoreMember]
        public string Secret { get; init; }
    }

    /// <summary>No setter anywhere, so the mapper has nothing it can write.</summary>
    public record ImmutableRecord
    {
        public long Id { get; }
        public string Name { get; }
    }

    /// <summary>A property with no getter: the mapper can write it but never read it back.</summary>
    public class WriteOnlyPoco
    {
        private string _value;

        public long Id { get; set; }

        public string OnlySet { set => _value = value; }

        // Read-only, so mapping skips it; it only exists so the test can see what was written.
        public string Value => _value;
    }

    /// <summary>One member of each kind the null policy distinguishes.</summary>
    public class NullPolicyPoco
    {
        public long Id { get; set; }       // plain value type: Null is ignored
        public int? Count { get; set; }    // nullable value type: Null applies
        public string Name { get; set; }   // reference type: Null applies
    }

    // Single member POCOs, so a conversion failure is not dependent on reflection order.
    public class EnumOnlyPoco
    {
        public long Id { get; set; }
        public Status Status { get; set; }
    }

    public class GuidOnlyPoco
    {
        public long Id { get; set; }
        public Guid CustomerKey { get; set; }
    }

    public class TimeSpanOnlyPoco
    {
        public long Id { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
