using System;

namespace DataMapper.Extensions
{
    /// <summary>
    /// Maps a property or field to a column with a different name. When this is not
    /// present the column name is assumed to be the name of the member.
    /// </summary>
    /// <example>
    /// <code>
    /// public class Customer
    /// {
    ///     [Column("customer_id")]
    ///     public long Id { get; set; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ColumnAttribute : Attribute
    {
        /// <summary>Name of the column this member is mapped from.</summary>
        public ColumnAttribute(string name) => Name = name;

        /// <summary>Name of the column this member is mapped from.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// Excludes a property or field from mapping. Named IgnoreMemberAttribute rather than Ignore so it does not clash with NUnit's IgnoreAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class IgnoreMemberAttribute : Attribute
    {
    }

    /// <summary>
    /// Overrides the null handling for a single member, so a POCO can say which fields must
    /// always have a value and which ones are genuinely optional.
    /// </summary>
    /// <example>
    /// <code>
    /// public class Customer
    /// {
    ///     public string PhoneNumber { get; set; } = string.Empty; // guaranteed non-null
    ///
    ///     [NullAs(NullHandling.Null)]
    ///     public string Notes { get; set; }                        // genuinely optional
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class NullAsAttribute : Attribute
    {
        /// <summary>Null handling to use for this member instead of the option default.</summary>
        public NullAsAttribute(NullHandling handling) => Handling = handling;

        /// <summary>Null handling to use for this member instead of the option default.</summary>
        public NullHandling Handling { get; }
    }
}
