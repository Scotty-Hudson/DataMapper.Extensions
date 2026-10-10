using System;
using System.Collections.Generic;
using System.Data;
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
