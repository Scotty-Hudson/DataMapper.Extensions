using System;
using System.Data;
using System.Globalization;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    public enum Status { Active, Inactive }

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

    public class MappingTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable("Account");
            table.Columns.Add("account_id", typeof(long));
            table.Columns.Add("display_name", typeof(string));
            table.Columns.Add("Secret", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add("Balance", typeof(int));
            table.Columns.Add("Duration", typeof(string));
            table.Columns.Add("Note", typeof(string));

            table.Rows.Add(1L, "John Doe", "should be ignored", "Inactive",
                "8a0f1c2e-1111-2222-3333-444455556666", DBNull.Value, "01:23:45", "a note");

            return table;
        }

        [Fact]
        public void ColumnAttributeMapsDifferentColumnName()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Equal(1L, account.Id);
            Assert.Equal("John Doe", account.DisplayName);
        }

        [Fact]
        public void IgnoreMemberAttributeSkipsMember()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Null(account.Secret);
        }

        [Fact]
        public void MapsPublicFields()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Equal("a note", account.Note);
        }

        [Fact]
        public void MapsEnumsFromString()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Equal(Status.Inactive, account.Status);
        }

        [Fact]
        public void MapsGuidFromString()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), account.Key);
        }

        [Fact]
        public void MapsTimeSpanFromString()
        {
            var account = BuildTable().Rows[0].MapTo<Account>();
            Assert.Equal(TimeSpan.Parse("01:23:45"), account.Duration);
        }

        [Fact]
        public void NormalizedNamesMatchSnakeCaseColumns()
        {
            var table = new DataTable();
            table.Columns.Add("first_name", typeof(string));
            table.Columns.Add("RewardsPoints", typeof(decimal));
            table.Rows.Add("John", 23.3m);

            var poco = table.Rows[0].MapTo<Customer>(new MapOptions { MatchNormalizedNames = true });
            Assert.Equal("John", poco.FirstName);
            Assert.Equal(23.3m, poco.RewardsPoints);
        }

        [Fact]
        public void CustomConverterIsUsed()
        {
            var table = new DataTable();
            table.Columns.Add("FirstName", typeof(string));
            table.Rows.Add("shout");

            var options = new MapOptions
            {
                Converter = (value, targetType) => targetType == typeof(string) ? value.ToString().ToUpper() : null
            };

            var poco = table.Rows[0].MapTo<Customer>(options);
            Assert.Equal("SHOUT", poco.FirstName);
        }

        [Fact]
        public void ConvertToUsesInvariantCultureByDefault()
        {
            var table = new DataTable();
            table.Columns.Add("Amount", typeof(string));
            table.Rows.Add("1.5");

            // Invariant culture always reads "1.5" as one and a half.
            Assert.Equal(1.5m, table.Rows[0].ConvertTo<decimal>("Amount"));

            // A German culture reads the same text as 15 because '.' is its group separator,
            // which is why the default is no longer CultureInfo.CurrentCulture.
            var german = new MapOptions { Culture = CultureInfo.GetCultureInfo("de-DE") };
            Assert.Equal(15m, table.Rows[0].ConvertTo<decimal>(german, "Amount"));
        }

        [Fact]
        public void ConvertToSupportsNullableTargetTypes()
        {
            var table = new DataTable();
            table.Columns.Add("Zip", typeof(int));
            table.Rows.Add(34567);

            Assert.Equal(34567, table.Rows[0].ConvertTo<int?>("Zip"));
        }

        [Fact]
        public void ConvertToSupportsEnums()
        {
            var table = new DataTable();
            table.Columns.Add("Status", typeof(string));
            table.Rows.Add("Inactive");

            Assert.Equal(Status.Inactive, table.Rows[0].ConvertTo<Status>("Status"));
        }

        [Fact]
        public void ConvertToSupportsGuid()
        {
            var table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Rows.Add("8a0f1c2e-1111-2222-3333-444455556666");

            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), table.Rows[0].ConvertTo<Guid>("Key"));
        }
    }
}
