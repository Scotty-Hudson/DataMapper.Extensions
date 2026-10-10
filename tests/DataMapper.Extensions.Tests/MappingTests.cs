using System;
using System.Data;
using System.Globalization;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// Column names, attributes, options and the custom converter. Data shapes and failure
    /// paths are in EdgeCaseTests.
    /// </summary>
    public class MappingTests : IClassFixture<TestData>
    {
        private readonly TestData _data;

        public MappingTests(TestData data) => _data = data;

        [Fact]
        public void ColumnAttributeMapsDifferentColumnName()
        {
            var account = _data.Account.Rows[0].MapTo<Account>();
            Assert.Equal(1L, account.Id);
            Assert.Equal("John Doe", account.DisplayName);

            // Public fields are mapped as well as properties.
            Assert.Equal("a note", account.Note);
        }

        [Fact]
        public void IgnoreMemberAttributeSkipsMember()
        {
            var account = _data.Account.Rows[0].MapTo<Account>();
            Assert.Equal("should be ignored", _data.Account.Rows[0]["Secret"]);
            Assert.Null(account.Secret);
        }

        [Fact]
        public void AttributeMappedColumnsFallBackToDefaultsWhenMissing()
        {
            var account = _data.Account.Rows[1].MapTo<Account>();
            Assert.Equal(2L, account.Id);
            Assert.Equal(string.Empty, account.DisplayName);
        }

        [Fact]
        public void MapsEnumsFromString()
        {
            Assert.Equal(Status.Inactive, _data.Account.Rows[0].MapTo<Account>().Status);
        }

        [Fact]
        public void MapsGuidFromString()
        {
            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), _data.Account.Rows[0].MapTo<Account>().Key);
        }

        [Fact]
        public void MapsTimeSpanFromString()
        {
            Assert.Equal(TimeSpan.Parse("01:23:45"), _data.Account.Rows[0].MapTo<Account>().Duration);
        }

        [Fact]
        public void NormalizedNamesMatchSnakeCaseColumns()
        {
            var poco = _data.SnakeCaseRecord.Rows[0].MapTo<Customer>(new MapOptions { MatchNormalizedNames = true });
            Assert.Equal("John", poco.FirstName);
            Assert.Equal(23.3m, poco.RewardsPoints);
        }

        [Fact]
        public void NormalizedNamesAreOffByDefault()
        {
            var poco = _data.SnakeCaseRecord.Rows[0].MapTo<Customer>();
            Assert.Equal(string.Empty, poco.FirstName);
            Assert.Null(poco.RewardsPoints);
        }

        [Fact]
        public void CustomConverterIsUsed()
        {
            var options = new MapOptions
            {
                Converter = (value, targetType) => targetType == typeof(string) ? value.ToString().Trim().ToUpper() : null
            };

            // Row 8 is the row with padded text.
            var poco = _data.Row(_data.Customer, 8).MapTo<StringOnlyPoco>(options);
            Assert.Equal("PADDED", poco.FirstName);
            Assert.Equal(string.Empty, poco.LastName);
        }

        [Fact]
        public void ConvertToUsesInvariantCultureByDefault()
        {
            // Row 7 holds "1.5" as text.
            Assert.Equal(1.5m, _data.Row(_data.Customer, 7).ConvertTo<decimal>("AmountText"));

            // A German culture reads the same text as 15 because '.' is its group separator,
            // which is why the default is no longer CultureInfo.CurrentCulture.
            var german = new MapOptions { Culture = CultureInfo.GetCultureInfo("de-DE") };
            Assert.Equal(15m, _data.Row(_data.Customer, 7).ConvertTo<decimal>(german, "AmountText"));
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
            Assert.Equal(Status.Inactive, _data.Row(_data.Customer, 7).ConvertTo<Status>("Status"));
        }

        [Fact]
        public void ConvertToSupportsGuid()
        {
            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), _data.Row(_data.Customer, 1).ConvertTo<Guid>("CustomerKey"));
        }
    }
}
