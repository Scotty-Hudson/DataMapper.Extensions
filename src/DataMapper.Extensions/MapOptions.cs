using System;
using System.Globalization;

namespace DataMapper.Extensions
{
    /// <summary>
    /// What to do when a value is DBNull, or when the data source has no column for the member.
    /// </summary>
    public enum NullHandling
    {
        /// <summary>
        /// Default. A member that the compiler considers non-nullable is guaranteed to be non-null:
        /// a non-nullable string becomes an empty string. Members annotated as nullable keep null.
        /// Other reference types keep whatever value the template POCO already has, because there is
        /// no such thing as an empty instance of an arbitrary type.
        /// </summary>
        EmptyString,

        /// <summary>Leave whatever value the template POCO already has.</summary>
        Keep,

        /// <summary>Set the member to null.</summary>
        Null
    }

    /// <summary>
    /// Options for the MapTo and ConvertTo extension methods.
    /// </summary>
    public class MapOptions
    {
        /// <summary>
        /// Shared default options. Do not mutate this instance; create your own
        /// MapOptions if you need different settings.
        /// </summary>
        public static MapOptions Default { get; } = new MapOptions();

        /// <summary>
        /// Culture used when converting values. Defaults to InvariantCulture.
        /// Note that earlier versions of this library used CultureInfo.CurrentCulture,
        /// which can silently corrupt numeric data (a column value of "1.5" becomes 15
        /// under a German culture).
        /// </summary>
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// What to do with DBNull values and columns that are not in the POCO.
        /// Defaults to <see cref="NullHandling.EmptyString"/>.
        /// </summary>
        public NullHandling NullHandling { get; set; } = NullHandling.EmptyString;

        /// <summary>
        /// Also match column names ignoring case, underscores and spaces, so a column
        /// named "first_name" maps to a property named FirstName.
        /// </summary>
        public bool MatchNormalizedNames { get; set; }

        /// <summary>
        /// Optional custom converter, called before the built-in conversion. Return null
        /// to fall back to the built-in conversion.
        /// </summary>
        public Func<object, Type, object> Converter { get; set; }
    }
}
