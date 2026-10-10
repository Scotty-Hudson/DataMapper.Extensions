using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    public class RoundTripTests
    {
        [Fact]
        public void ToDataTableProducesColumnsAndRows()
        {
            var customers = new List<Customer>
            {
                new Customer { Id = 1, FirstName = "John", Zip = 34567, RewardsPoints = 23.3m },
                new Customer { Id = 2, FirstName = "Jake" }
            };

            var table = customers.ToDataTable("Customer");

            Assert.Equal("Customer", table.TableName);
            Assert.Equal(typeof(Customer).GetProperties().Length, table.Columns.Count);
            Assert.Equal(2, table.Rows.Count);

            // Nullable members produce columns of the underlying type.
            Assert.Equal(typeof(decimal), table.Columns[nameof(Customer.RewardsPoints)].DataType);
            Assert.Equal(DBNull.Value, table.Rows[1][nameof(Customer.RewardsPoints)]);
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

            using (var reader = customers.ToDataReader())
            {
                var back = reader.MapTo<Customer>();
                Assert.Equal(2L, back[0].Id);
                Assert.Equal("Jake", back[0].FirstName);
            }
        }

        [Fact]
        public void ColumnAttributesAreUsedForGeneratedColumns()
        {
            var accounts = new List<Account> { new Account { Id = 7, DisplayName = "John" } };
            var table = accounts.ToDataTable();

            Assert.True(table.Columns.Contains("account_id"));
            Assert.True(table.Columns.Contains("display_name"));
            Assert.Equal(7L, table.Rows[0]["account_id"]);

            // Ignored members are not turned into columns.
            Assert.False(table.Columns.Contains("Secret"));
        }
    }
}
