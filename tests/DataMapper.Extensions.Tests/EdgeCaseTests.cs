using System;
using System.Collections.Generic;
using System.Data;
using Xunit;

namespace DataMapper.Extensions.Tests
{
    public class EdgeCaseTests(TestData data) : IClassFixture<TestData>
    {
        private static DataTable BuildTable(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();
            foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
            return table;
        }

        // ---------- data types ----------

        [Fact]
        public void FullRow_MapsEveryDataType()
        {
            var c = data.Row(data.Customer, 1).MapTo<Customer>();

            Assert.Equal(1L, c.Id);
            Assert.Equal("John", c.FirstName);
            Assert.Equal("Doe", c.LastName);
            Assert.Equal("johnDoe@maxmail.com", c.Email);
            Assert.Equal("345-231-9234", c.PhoneNumber);
            Assert.Equal("312 Brackish Rd", c.Address);
            Assert.Equal("Boston", c.City);
            Assert.Equal("MA", c.State);
            Assert.Equal(34567, c.Zip);                                   // column is ZIP
            Assert.Equal(23.3m, c.RewardsPoints);
            Assert.Equal(Status.Active, c.Status);                        // string -> enum
            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), c.Key);  // string -> Guid
            Assert.Equal(new TimeSpan(0, 1, 23, 45), c.Duration);         // string -> TimeSpan
            Assert.Equal(1980, c.DateOfBirth.Year);
            Assert.Equal(3, c.DateOfBirth.Month);
            Assert.Equal(15, c.DateOfBirth.Day);
            Assert.True(c.IsActive);
            Assert.Equal(98.6d, c.Score);
            Assert.Equal((short)7, c.TinyValue);
            Assert.Equal((byte)200, c.ByteValue);
            Assert.Equal(1.5f, c.FloatValue);
            Assert.Equal("a note", c.Notes);
        }

        [Fact]
        public void ColumnsThatAreNotMembersOfThePocoAreIgnored()
        {
            // ExtraField is a column but not a member of Customer. Mapping the same row with
            // and without that column has to give the same result.
            var withColumn = BuildTable(("Id", typeof(long)), ("FirstName", typeof(string)), ("ExtraField", typeof(decimal)));
            withColumn.Rows.Add(1L, "John", 3.9m);

            var withoutColumn = BuildTable(("Id", typeof(long)), ("FirstName", typeof(string)));
            withoutColumn.Rows.Add(1L, "John");

            var mapped = withColumn.Rows[0].MapTo<StringOnlyPoco>();
            var baseline = withoutColumn.Rows[0].MapTo<StringOnlyPoco>();

            Assert.Equal(baseline.Id, mapped.Id);
            Assert.Equal(baseline.FirstName, mapped.FirstName);
            Assert.Equal(baseline.LastName, mapped.LastName);
        }

        [Fact]
        public void ValuesAlreadyOfTheTargetTypeAreAssignedWithoutConverting()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "John");

            var row = table.Rows[0];
            Assert.Equal(1L, row.ConvertTo<long>("Id"));
            Assert.Equal("John", row.ConvertTo<string>("Name"));
        }

        [Fact]
        public void BoundaryValuesSurviveMapping()
        {
            var c = data.Row(data.Customer, 4).MapTo<Customer>();

            Assert.Equal(int.MaxValue, c.Zip);
            Assert.Equal(-9999.99m, c.RewardsPoints);
            Assert.Equal(double.MinValue, c.Score);
            Assert.Equal(short.MinValue, c.TinyValue);
            Assert.Equal(byte.MaxValue, c.ByteValue);
            Assert.Equal(float.MinValue, c.FloatValue);
            Assert.False(c.IsActive);
        }

        // ---------- missing values ----------

        [Fact]
        public void SparseRow_KeepsDefaultsAndAppliesTheNullStringRule()
        {
            var c = data.Row(data.Customer, 2).MapTo<Customer>();

            Assert.Equal("Jake", c.FirstName);
            Assert.Equal(0, c.Zip);
            Assert.Null(c.RewardsPoints);
            Assert.Equal(string.Empty, c.PhoneNumber);
            Assert.Equal(Status.Active, c.Status);
            Assert.Equal(Guid.Empty, c.Key);
            Assert.Equal(TimeSpan.Zero, c.Duration);
            Assert.Equal(default(DateTime), c.DateOfBirth);
            Assert.False(c.IsActive);
            Assert.Equal(0d, c.Score);
            Assert.Equal((short)0, c.TinyValue);
            Assert.Equal((byte)0, c.ByteValue);
            Assert.Equal(0f, c.FloatValue);
        }

        [Fact]
        public void EmptyStringsAreNotTreatedAsMissing()
        {
            var p = data.Row(data.Customer, 3).MapTo<StringOnlyPoco>();
            Assert.Equal("Bob", p.FirstName);
            Assert.Equal(string.Empty, p.LastName);
        }

        [Fact]
        public void RowWithOnlyTheKeyMapsEverythingElseToDefaults()
        {
            var c = data.Row(data.Customer, 9).MapTo<Customer>();
            Assert.Equal(9L, c.Id);
            Assert.Equal(string.Empty, c.FirstName);
            Assert.Equal(0, c.Zip);
            Assert.Null(c.RewardsPoints);
        }

        // ---------- failure paths ----------

        [Fact]
        public void InvalidEnumTextThrows()
        {
            Assert.Throws<ArgumentException>(() => data.Row(data.BadData, 5).ConvertTo<Status>("Status"));
        }

        [Fact]
        public void InvalidGuidTextThrows()
        {
            Assert.Throws<FormatException>(() => data.Row(data.BadData, 5).ConvertTo<Guid>("CustomerKey"));
        }

        [Fact]
        public void InvalidTimeSpanTextThrows()
        {
            Assert.Throws<FormatException>(() => data.Row(data.BadData, 5).ConvertTo<TimeSpan>("Duration"));
        }

        [Fact]
        public void EmptyTextForEnumGuidAndTimeSpanThrows()
        {
            var row = data.Row(data.BadData, 6);
            Assert.Throws<ArgumentException>(() => row.ConvertTo<Status>("Status"));
            Assert.Throws<FormatException>(() => row.ConvertTo<Guid>("CustomerKey"));
            Assert.Throws<FormatException>(() => row.ConvertTo<TimeSpan>("Duration"));
        }

        [Fact]
        public void MappingARowWithBadDataThrows()
        {
            // Single-member POCOs so the failure is not dependent on reflection order.
            Assert.Throws<ArgumentException>(() => data.Row(data.BadData, 5).MapTo<EnumOnlyPoco>());
            Assert.Throws<FormatException>(() => data.Row(data.BadData, 5).MapTo<GuidOnlyPoco>());
            Assert.Throws<FormatException>(() => data.Row(data.BadData, 5).MapTo<TimeSpanOnlyPoco>());
        }

        [Fact]
        public void InvalidNumericTextThrows()
        {
            var table = BuildTable(("Amount", typeof(string)));
            table.Rows.Add("abc");
            Assert.Throws<FormatException>(() => table.Rows[0].ConvertTo<decimal>("Amount"));
        }

        [Fact]
        public void NumericTextIsNotAValidBoolean()
        {
            Assert.Throws<FormatException>(() => data.Row(data.Customer, 7).ConvertTo<bool>("FlagText"));
        }

        [Fact]
        public void ConvertToOnAMissingColumnThrows()
        {
            Assert.Throws<ArgumentException>(() => data.Row(data.Customer, 1).ConvertTo<string>("Nope"));
        }

        [Fact]
        public void ConvertToOnDBNullForANonNullableValueTypeThrows()
        {
            Assert.Throws<InvalidCastException>(() => data.Row(data.Customer, 2).ConvertTo<int>(nameof(Customer.Zip)));
        }

        [Fact]
        public void ConvertToOnDBNullForANullableTargetReturnsNull()
        {
            Assert.Null(data.Row(data.Customer, 2).ConvertTo<int?>(nameof(Customer.Zip)));
            Assert.Null(data.Row(data.Customer, 2).ConvertTo<decimal?>(nameof(Customer.RewardsPoints)));
        }

        [Fact]
        public void ConvertToOnDBNullForAStringReturnsEmpty()
        {
            Assert.Equal(string.Empty, data.Row(data.Customer, 2).ConvertTo<string>(nameof(Customer.PhoneNumber)));
        }

        [Fact]
        public void ConvertToDefaultValueIsUsedForDBNull()
        {
            Assert.Equal(11111, data.Row(data.Customer, 2).ConvertTo(nameof(Customer.Zip), 11111));
        }

        [Fact]
        public void PublicFieldsAreMapped()
        {
            var record = data.FieldOnlyRecord.Rows[0].MapTo<FieldOnlyRecord>();
            Assert.Equal(1L, record.Id);
            Assert.Equal("Field Person", record.Name);

            var second = data.FieldOnlyRecord.Rows[1].MapTo<FieldOnlyRecord>();
            Assert.Equal(2L, second.Id);
            Assert.Equal(string.Empty, second.Name);
        }

        [Fact]
        public void MembersWithoutASetterAreSkipped()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "Person");

            var p = table.Rows[0].MapTo<ReadOnlyPoco>();
            Assert.Equal(0L, p.Id);            // no setter, so nothing was written
            Assert.Equal("Person", p.Name);
        }

        [Fact]
        public void PocoWithNoMembersMapsToAnEmptyObject()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var p = table.Rows[0].MapTo<NoMemberPoco>();
            Assert.NotNull(p);
        }

        [Fact]
        public void TypeWithoutAParameterlessConstructorIsMapped()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "Person");

            var p = table.Rows[0].MapTo<NoDefaultConstructorPoco>();
            Assert.Equal(1L, p.Id);
            Assert.Equal("Person", p.Name);
        }

        [Fact]
        public void PropertyInitializersRunForTypesWithADefaultConstructor()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var p = table.Rows[0].MapTo<InitializerPoco>();
            Assert.Equal("default-tag", p.Tag);
        }

        [Fact]
        public void InitOnlyPropertiesAreMapped()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "Person");

            var p = table.Rows[0].MapTo<InitOnlyPoco>();
            Assert.Equal(1L, p.Id);
            Assert.Equal("Person", p.Name);
        }

        // ---------- template defaults ----------

        [Fact]
        public void TemplateDefaultsArePreservedForMissingAndDBNullColumns()
        {
            // Row 9 has only the key, so every template value has to survive.
            var template = new Customer { Zip = -1, FirstName = "template", Status = Status.Inactive };
            var c = data.Row(data.Customer, 9).MapTo(template);

            Assert.Equal(9L, c.Id);
            Assert.Equal(-1, c.Zip);
            Assert.Equal("template", c.FirstName);
            Assert.Equal(Status.Inactive, c.Status);
        }

        [Fact]
        public void EveryRowGetsItsOwnInstanceSeededFromTheTemplate()
        {
            var template = new Customer { Zip = -1 };
            var rows = data.Customer.MapTo(template, new MapOptions { NullHandling = NullHandling.Keep });

            Assert.Equal(7, rows.Count);
            Assert.NotSame(rows[0], rows[1]);
            Assert.Equal(34567, rows[0].Zip);   // row 1 has a value
            Assert.Equal(-1, rows[1].Zip);      // row 2 is DBNull, so the template value is used
        }

        // ---------- reader ----------

        [Fact]
        public void ReaderMapsAllDataTypes()
        {
            using var reader = data.Customer.CreateDataReader();
            var list = reader.MapTo<Customer>();
            var first = list[0];

            Assert.Equal(7, list.Count);
            Assert.Equal(Status.Active, first.Status);
            Assert.Equal(new Guid("8a0f1c2e-1111-2222-3333-444455556666"), first.Key);
            Assert.Equal(new TimeSpan(0, 1, 23, 45), first.Duration);
            Assert.Equal((byte)200, first.ByteValue);
        }

        [Fact]
        public void ReaderFieldNamesAreCaseInsensitive()
        {
            var table = BuildTable(("zip", typeof(int)));
            table.Rows.Add(34567);

            using var reader = table.CreateDataReader();
            var p = reader.MapTo<Customer>();
            Assert.Equal(34567, p[0].Zip);
        }

        [Fact]
        public void ReaderWithDuplicateFieldNamesUsesTheLastOne()
        {
            using var reader = new FakeReader(("Id", 1L), ("Name", "first"), ("Name", "second"));
            var p = reader.MapTo<FieldOnlyRecord>();
            Assert.Equal("second", p[0].Name);
        }

        [Fact]
        public void EmptyReaderReturnsAnEmptyList()
        {
            var table = BuildTable(("Id", typeof(long)));

            using var reader = table.CreateDataReader();
            var list = reader.MapTo<FieldOnlyRecord>();
            Assert.Empty(list);
        }

        [Fact]
        public void ReaderIgnoresFieldsThatAreNotMembersOfThePoco()
        {
            using var reader = new FakeReader(("Id", 1L), ("Unknown", "value"));
            var p = reader.MapTo<FieldOnlyRecord>();
            Assert.Equal(1L, p[0].Id);
            Assert.Equal(string.Empty, p[0].Name);
        }

        // ---------- reverse direction ----------

        [Fact]
        public void ToDataTableUsesTypesAttributesAndSkipsIgnoredMembers()
        {
            var table = new List<Account> { new Account { Id = 7, DisplayName = "John" } }.ToDataTable("Account");

            Assert.Equal("Account", table.TableName);
            Assert.True(table.Columns.Contains("account_id"));
            Assert.True(table.Columns.Contains("display_name"));
            Assert.False(table.Columns.Contains("Secret"));
            Assert.Equal(7L, table.Rows[0]["account_id"]);

            // Enum members keep their type, nullable members use the underlying type.
            Assert.Equal(typeof(Status), table.Columns["Status"].DataType);
            Assert.Equal(typeof(int), table.Columns[nameof(Account.Balance)].DataType);
            Assert.Equal(0, table.Rows[0]["Status"]);   // DataTable stores an enum as its underlying type
            Assert.Equal(DBNull.Value, table.Rows[0][nameof(Account.Balance)]);
        }

        [Fact]
        public void ToDataTableWithNoRowsProducesColumnsOnly()
        {
            var table = new List<Customer>().ToDataTable();
            Assert.Equal(typeof(Customer).GetProperties().Length, table.Columns.Count);
            Assert.Equal(0, table.Rows.Count);
        }

        [Fact]
        public void RoundTripThroughDataTablePreservesValues()
        {
            var customers = new List<Customer> { new Customer { Id = 1, FirstName = "John", RewardsPoints = 23.3m } };
            var back = customers.ToDataTable().MapTo<Customer>();

            Assert.Single(back);
            Assert.Equal(1L, back[0].Id);
            Assert.Equal("John", back[0].FirstName);
            Assert.Equal(23.3m, back[0].RewardsPoints);
        }

        [Fact]
        public void RoundTripThroughDataReaderPreservesValues()
        {
            var customers = new List<Customer> { new Customer { Id = 2, FirstName = "Jake" } };

            using var reader = customers.ToDataReader();
            var back = reader.MapTo<Customer>();
            Assert.Equal(2L, back[0].Id);
            Assert.Equal("Jake", back[0].FirstName);
        }
    }
}
