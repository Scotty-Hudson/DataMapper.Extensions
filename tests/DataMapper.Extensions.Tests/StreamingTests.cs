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
    public class StreamingTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable("Customer");
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("FirstName", typeof(string));
            table.Rows.Add(1L, "John");
            table.Rows.Add(2L, "Jake");
            table.Rows.Add(3L, "Bob");
            return table;
        }

        [Fact]
        public void DataTableEnumerateToStreamsRows()
        {
            var streamed = BuildTable().EnumerateTo<Customer>(MapOptions.Default).ToList();
            Assert.Equal(3, streamed.Count);
            Assert.Equal("Jake", streamed[1].FirstName);
        }

        [Fact]
        public void DataTableEnumerateToIsLazy()
        {
            // Only the first two rows are pulled, so the third is never mapped.
            var firstTwo = BuildTable().EnumerateTo<Customer>(MapOptions.Default).Take(2).ToList();
            Assert.Equal(2, firstTwo.Count);
            Assert.Equal("John", firstTwo[0].FirstName);
        }

        [Fact]
        public void DataReaderEnumerateToStreamsRecords()
        {
            using (var reader = BuildTable().CreateDataReader())
            {
                var streamed = reader.EnumerateTo<Customer>(MapOptions.Default).ToList();
                Assert.Equal(3, streamed.Count);
                Assert.Equal("Bob", streamed[2].FirstName);
            }
        }

        [Fact]
        public async Task DbDataReaderEnumerateToAsyncStreamsRecords()
        {
            using (var reader = BuildTable().CreateDataReader())
            {
                var streamed = new List<Customer>();
                await foreach (var customer in reader.EnumerateToAsync<Customer>(new Customer(), MapOptions.Default, CancellationToken.None))
                    streamed.Add(customer);

                Assert.Equal(3, streamed.Count);
                Assert.Equal("Jake", streamed[1].FirstName);
            }
        }

        [Fact]
        public async Task EnumerateToAsyncWithoutDefaultPoco()
        {
            using (var reader = BuildTable().CreateDataReader())
            {
                var streamed = new List<Customer>();
                await foreach (var customer in reader.EnumerateToAsync<Customer>(MapOptions.Default, TestContext.Current.CancellationToken))
                    streamed.Add(customer);

                Assert.Equal(3, streamed.Count);
                Assert.Equal("John", streamed[0].FirstName);
            }
        }

        [Fact]
        public async Task MapToAsyncReturnsAllRecords()
        {
            using (var reader = BuildTable().CreateDataReader())
            {
                var streamed = await reader.MapToAsync<Customer>(new Customer(), MapOptions.Default, CancellationToken.None);
                Assert.Equal(3, streamed.Count);
                Assert.Equal("John", streamed[0].FirstName);
            }
        }

        [Fact]
        public async Task MapToAsyncHonoursCancellationToken()
        {
            using (var reader = BuildTable().CreateDataReader())
            {
                var cancelled = new CancellationTokenSource();
                cancelled.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (var customer in reader.EnumerateToAsync<Customer>(new Customer(), MapOptions.Default, cancelled.Token))
                    { }
                });
            }
        }
    }
}
