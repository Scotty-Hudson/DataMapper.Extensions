using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// The paths the other test files do not reach: every public overload, the odd member
    /// shapes, and the branches inside the conversion and null-policy helpers.
    /// </summary>
    public class CoverageTests
    {
        private static DataTable BuildTable(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();
            foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
            return table;
        }

        // ---------- every overload ----------

        [Fact]
        public void DataTableMapToWithOptionsOverload()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, DBNull.Value);

            var list = table.MapTo<NullPolicyPoco>(new MapOptions { NullHandling = NullHandling.Keep });
            Assert.Single(list);
            Assert.Equal(1L, list[0].Id);
        }

        [Fact]
        public void DataTableEnumerateToWithoutOptionsOverload()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var list = table.EnumerateTo<NullPolicyPoco>().ToList();
            Assert.Single(list);
            Assert.Equal(1L, list[0].Id);
        }

        [Fact]
        public void DataReaderMapToWithOptionsOverload()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "Person");

            using (var reader = table.CreateDataReader())
            {
                var list = reader.MapTo<NullPolicyPoco>(new MapOptions { NullHandling = NullHandling.Keep });
                Assert.Single(list);
                Assert.Equal("Person", list[0].Name);
            }
        }

        [Fact]
        public void DataReaderEnumerateToWithoutOptionsOverload()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            using (var reader = table.CreateDataReader())
            {
                var list = reader.EnumerateTo<NullPolicyPoco>().ToList();
                Assert.Single(list);
                Assert.Equal(1L, list[0].Id);
            }
        }

        [Fact]
        public async Task MapToAsyncWithOptionsOverload()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            using (var reader = table.CreateDataReader())
            {
                var list = await reader.MapToAsync<NullPolicyPoco>(
                    new MapOptions { NullHandling = NullHandling.Keep }, TestContext.Current.CancellationToken);
                Assert.Single(list);
                Assert.Equal(1L, list[0].Id);
            }
        }

        [Fact]
        public async Task EnumerateToAsyncCanBeCancelledBetweenRecords()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "one");
            table.Rows.Add(2L, "two");

            using (var reader = table.CreateDataReader())
            {
                var cancelled = new CancellationTokenSource();
                var seen = 0;

                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (var poco in reader.EnumerateToAsync<NullPolicyPoco>(new NullPolicyPoco(), MapOptions.Default, cancelled.Token))
                    {
                        seen++;
                        cancelled.Cancel();
                    }
                });

                Assert.Equal(1, seen);
            }
        }

        // ---------- odd member shapes ----------

        [Fact]
        public void WriteOnlyPropertiesAreWrittenButNeverRead()
        {
            var table = BuildTable(("Id", typeof(long)), ("OnlySet", typeof(string)));
            table.Rows.Add(1L, "written");

            var p = table.Rows[0].MapTo<WriteOnlyPoco>();
            Assert.Equal(1L, p.Id);
            Assert.Equal("written", p.Value);
        }

        [Fact]
        public void WriteOnlyMemberSkipsTheNullPolicyBecauseItCannotBeRead()
        {
            // ApplyNullPolicy cannot read the current value of a setter-only member, so the
            // guarantee is skipped. Before the guard this threw NullReferenceException.
            var table = BuildTable(("Id", typeof(long)), ("OnlySet", typeof(string)));
            table.Rows.Add(1L, DBNull.Value);

            var p = table.Rows[0].MapTo<WriteOnlyPoco>();
            Assert.Equal(1L, p.Id);

            // Same for a member with no column at all.
            var missing = BuildTable(("Id", typeof(long)));
            missing.Rows.Add(2L);
            Assert.Equal(2L, missing.Rows[0].MapTo<WriteOnlyPoco>().Id);
        }

        [Fact]
        public void NullOptionsUseTheDefault()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(DBNull.Value, DBNull.Value);

            var p = table.MapTo<StringOnlyPoco>((MapOptions)null)[0];
            Assert.Equal(string.Empty, p.FirstName);

            Assert.Null(table.Rows[0].ConvertTo<long?>((MapOptions)null, "Id"));

            // A nullable target that has a value. A boxed long is reported as an instance of
            // Nullable<long>, so the first instance-of check in ConvertValue returns it.
            table.Rows[0]["Id"] = 1L;
            Assert.Equal(1L, table.Rows[0].ConvertTo<long?>("Id"));

            // The reader path has its own option handling, so it needs its own check.
            using (var reader = table.CreateDataReader())
            {
                var r = reader.MapTo<StringOnlyPoco>((MapOptions)null)[0];
                Assert.Equal(string.Empty, r.FirstName);
            }
        }

        [Fact]
        public void ConvertToObjectOnDbNullReturnsDbNullValue()
        {
            // object is not a nullable target type in the reflection sense, so the nullable rule
            // does not apply and the value passes through unchanged.
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(DBNull.Value);

            Assert.Equal(DBNull.Value, table.Rows[0].ConvertTo<object>("Id"));
        }

        [Fact]
        public void ToDataTableThrowsOnDuplicateColumnNames()
        {
            var rows = new[] { new DuplicateColumnPoco { A = "x", B = "y" } };
            Assert.Throws<DuplicateNameException>(() => rows.ToDataTable());
        }

        // ---------- DateOnly and TimeOnly ----------

        [Fact]
        public void DateOnlyAndTimeOnlyFromStrings()
        {
            var table = BuildTable(("Day", typeof(string)), ("Time", typeof(string)));
            table.Rows.Add("2026-01-01", "10:30:00");

            Assert.Equal(new DateOnly(2026, 1, 1), table.Rows[0].ConvertTo<DateOnly>("Day"));
            Assert.Equal(new TimeOnly(10, 30, 0), table.Rows[0].ConvertTo<TimeOnly>("Time"));
        }

        [Fact]
        public void DateOnlyAndTimeOnlyFromDateTime()
        {
            // They are not IConvertible, so Convert.ChangeType cannot bridge to them. Providers
            // commonly return these columns as DateTime.
            var table = BuildTable(("Moment", typeof(DateTime)));
            var moment = new DateTime(2026, 1, 1, 10, 30, 0);
            table.Rows.Add(moment);

            Assert.Equal(new DateOnly(2026, 1, 1), table.Rows[0].ConvertTo<DateOnly>("Moment"));
            Assert.Equal(new TimeOnly(10, 30, 0), table.Rows[0].ConvertTo<TimeOnly>("Moment"));

            // A DateTime value with a target that is neither of those falls through to
            // Convert.ChangeType, which is the branch the two assertions above do not reach.
            Assert.Equal(moment.ToString(CultureInfo.InvariantCulture), table.Rows[0].ConvertTo<string>("Moment"));
        }

        // ---------- duplicate names ----------

        [Fact]
        public void ReaderDuplicateNormalizedNamesTakeTheLastOne()
        {
            var reader = new FakeReader(("first_name", "first"), ("firstname", "second"));
            var p = reader.MapTo<StringOnlyPoco>(new MapOptions { MatchNormalizedNames = true })[0];

            Assert.Equal("second", p.FirstName);
        }

        [Fact]
        public void DataTableDuplicateNormalizedNamesTakeTheLastOne()
        {
            // The DataTable path used to take the first match, which disagreed with the reader.
            var table = BuildTable(("first_name", typeof(string)), ("firstname", typeof(string)));
            table.Rows.Add("first", "second");

            var p = table.MapTo<StringOnlyPoco>(new MapOptions { MatchNormalizedNames = true })[0];
            Assert.Equal("second", p.FirstName);
        }

        [Fact]
        public void ReaderWithANullFieldNameIsSkipped()
        {
            var reader = new FakeReader((null, "ignored"), ("Name", "kept"));
            var p = reader.MapTo<FieldOnlyRecord>()[0];

            Assert.Equal("kept", p.Name);
        }

        [Fact]
        public void ReaderSkipsMembersWithoutASetter()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "Person");

            using (var reader = table.CreateDataReader())
            {
                var p = reader.MapTo<ReadOnlyPoco>()[0];
                Assert.Equal(0L, p.Id);
                Assert.Equal("Person", p.Name);
            }
        }

        [Fact]
        public void ReaderMatchesNormalizedNames()
        {
            var table = BuildTable(("first_name", typeof(string)), ("rewards_points", typeof(decimal)));
            table.Rows.Add("John", 23.3m);

            using (var reader = table.CreateDataReader())
            {
                var p = reader.MapTo<Customer>(new MapOptions { MatchNormalizedNames = true })[0];
                Assert.Equal("John", p.FirstName);
                Assert.Equal(23.3m, p.RewardsPoints);
            }
        }

        [Fact]
        public void MappingToStringProducesEmptyStrings()
        {
            // string is a class with no writable members, so the only thing the mapper can do
            // is create one, which New<T> specialises.
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var list = table.MapTo<string>();
            Assert.Single(list);
            Assert.Equal(string.Empty, list[0]);
        }

        // ---------- conversion branches ----------

        [Fact]
        public void NullableTargetWithAValueOfTheUnderlyingType()
        {
            var table = BuildTable(("Count", typeof(int)));
            table.Rows.Add(5);

            Assert.Equal(5, table.Rows[0].ConvertTo<int?>("Count"));
        }

        [Fact]
        public void NullableTargetConvertedFromText()
        {
            var table = BuildTable(("Count", typeof(string)));
            table.Rows.Add("5");

            Assert.Equal(5, table.Rows[0].ConvertTo<int?>("Count"));
        }

        [Fact]
        public void NullHandlingNullOnValueTypesIsIgnored()
        {
            var table = BuildTable(("Id", typeof(long)), ("Count", typeof(int)), ("Name", typeof(string)));
            table.Rows.Add(1L, DBNull.Value, DBNull.Value);

            var p = table.Rows[0].MapTo<NullPolicyPoco>(new MapOptions { NullHandling = NullHandling.Null });
            Assert.Equal(1L, p.Id);                 // plain value type: nothing to null out
            Assert.Null(p.Count);                   // nullable value type: becomes null
            Assert.Null(p.Name);                    // reference type: becomes null
        }

        [Fact]
        public void ConverterReturningNullFallsBackToTheNormalConversion()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var options = new MapOptions { Converter = (value, targetType) => null };
            var p = table.Rows[0].MapTo<NullPolicyPoco>(options);
            Assert.Equal(1L, p.Id);
        }
    }
}
