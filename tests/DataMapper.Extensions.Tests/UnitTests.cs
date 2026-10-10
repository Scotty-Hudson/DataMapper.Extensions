using System;
using System.Data;
using System.IO;
using System.Linq;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    public class UnitTests
    {
        private static readonly DataTable TestTable = LoadTestData();

        private static DataTable LoadTestData()
        {
            var table = new DataTable();
            table.ReadXml(Path.Combine(AppContext.BaseDirectory, "TestData.xml"));
            return table;
        }

        private void TestAllCustomerPropertiesById(Customer c)
        {
            var dr = TestTable.Select($"{nameof(c.Id)} = {c.Id}")[0];
            TestAllCustomerPropertiesById(c, dr);
        }

        private void TestAllCustomerPropertiesById(Customer c, DataRow dr)
        {
            Assert.Equal(dr.ConvertTo<string>(nameof(c.FirstName)), c.FirstName);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.LastName)), c.LastName);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.Email)), c.Email);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.PhoneNumber)), c.PhoneNumber);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.Address)), c.Address);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.City)), c.City);
            Assert.Equal(dr.ConvertTo<string>(nameof(c.State)), c.State);
            Assert.Equal(dr.ConvertTo(nameof(c.Zip), 0), c.Zip);
            Assert.Equal(dr[nameof(c.RewardsPoints)] == DBNull.Value ? null : dr[nameof(c.RewardsPoints)], c.RewardsPoints);
        }

        [Fact]
        public void DataReader_ReturnsCorrectListCount()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>();
                Assert.Equal(TestTable.Rows.Count, customerList.Count);
            }
        }

        [Fact]
        public void DataTable_ReturnsCorrectListCount()
        {
            var customerList = TestTable.MapTo<Customer>();
            Assert.Equal(TestTable.Rows.Count, customerList.Count);
        }

        [Fact]
        public void DataReader_AreAllPropertiesMappedCorrectFromAllRows()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>();
                foreach (var c in customerList)
                {
                    TestAllCustomerPropertiesById(c);
                }
            }
        }

        [Fact]
        public void DataTable_AreAllPropertiesMappedCorrectFromAllRows()
        {
            var customerList = TestTable.MapTo<Customer>();
            foreach (var c in customerList)
            {
                TestAllCustomerPropertiesById(c);
            }
        }

        [Fact]
        public void DataRow_AreAllPropertiesMappedCorrectFromAllRows()
        {
            foreach (DataRow dr in TestTable.Rows)
            {
                TestAllCustomerPropertiesById(dr.MapTo<Customer>(), dr);
            }
        }

        [Fact]
        public void DataReader_DoesNullableTypeSetNullCorrectly()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>();
                var cust = customerList.First(c => c.Id == 2);
                Assert.Null(cust.RewardsPoints);
            }
        }

        [Fact]
        public void DataTable_DoesNullableTypeSetNullCorrectly()
        {
            var customerList = TestTable.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Null(cust.RewardsPoints);
        }

        [Fact]
        public void DataRow_DoesNullableTypeSetNullCorrectly()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 2");
            var cust = dr[0].MapTo<Customer>();
            Assert.Null(cust.RewardsPoints);
        }

        [Fact]
        public void DataReader_DoesNonNullableTypeSetDefaultCorrectly()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>();
                var cust = customerList.First(c => c.Id == 2);
                Assert.Equal(0, cust.Zip);
            }
        }

        [Fact]
        public void DataTable_DoesNonNullableTypeSetDefaultCorrectly()
        {
            var customerList = TestTable.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataRow_DoesNonNullableTypeSetDefaultCorrectly()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 2");
            var cust = dr[0].MapTo<Customer>();
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataReader_DoesDBNullOverrideCorrectly()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo(new Customer { Zip = 0 });
                var cust = customerList.First(c => c.Id == 2);
                Assert.Equal(0, cust.Zip);
            }
        }

        [Fact]
        public void DataTable_DoesDBNullOverrideCorrectly()
        {
            var customerList = TestTable.MapTo(new Customer { Zip = 0 });
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataRow_DoesDBNullOverrideCorrectly()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 2");
            var cust = dr[0].MapTo(new Customer { Zip = 0 });
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataReader_DoesDBNullReturnCorrectlyForString()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>();
                var cust = customerList.First(c => c.Id == 3);
                Assert.Equal(string.Empty, cust.PhoneNumber);
            }
        }

        [Fact]
        public void DataTable_DoesDBNullReturnCorrectlyForString()
        {
            var customerList = TestTable.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 3);
            Assert.Equal(string.Empty, cust.PhoneNumber);
        }

        [Fact]
        public void DataRow_DoesDBNullReturnCorrectlyForString()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 3");
            var cust = dr[0].MapTo<Customer>();
            Assert.Equal(string.Empty, cust.PhoneNumber);
        }

        [Fact]
        public void DataReader_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            using (var reader = TestTable.CreateDataReader())
            {
                var customerList = reader.MapTo<Customer>(false);
                var cust = customerList.First(c => c.Id == 3);
                Assert.Null(cust.PhoneNumber);
            }
        }

        [Fact]
        public void DataTable_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            var customerList = TestTable.MapTo<Customer>(false);
            var cust = customerList.First(c => c.Id == 3);
            Assert.Null(cust.PhoneNumber);
        }

        [Fact]
        public void DataRow_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 3");
            var cust = dr[0].MapTo<Customer>(false);
            Assert.Null(cust.PhoneNumber);
        }

        [Fact]
        public void ConvertTo_DoesReturnEmptyStringForDBNull()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 3");
            var phone = dr[0].ConvertTo<string>(nameof(Customer.PhoneNumber));
            Assert.Equal(string.Empty, phone);
        }

        [Fact]
        public void ConvertTo_DoesNullValueOverrideCorrectly()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 2");
            var zip = dr[0].ConvertTo(nameof(Customer.Zip), 11111);
            Assert.Equal(11111, zip);
        }

        [Fact]
        public void ConvertTo_DoesFieldWithValueMapCorrectly()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 1");
            var firstName = dr[0].ConvertTo<string>(nameof(Customer.FirstName));
            Assert.Equal(dr[0][nameof(Customer.FirstName)], firstName);
        }

        [Fact]
        public void ConvertTo_DoesDbNullStringOverrideToNull()
        {
            var dr = TestTable.Select($"{nameof(Customer.Id)} = 2");
            var phone = dr[0].ConvertTo<string>(nameof(Customer.PhoneNumber), null);
            Assert.Null(phone);
        }
    }
}
