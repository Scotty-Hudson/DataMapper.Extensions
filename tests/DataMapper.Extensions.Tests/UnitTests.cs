using System;
using System.Data;
using System.Linq;
using Xunit;

namespace DataMapper.Extensions.Tests
{
    public class UnitTests : IClassFixture<TestData>
    {
        private readonly TestData _data;

        public UnitTests(TestData data) => _data = data;

        private void TestAllCustomerPropertiesById(Customer c)
        {
            var dr = _data.Customer.Select($"{nameof(c.Id)} = {c.Id}")[0];
            TestAllCustomerPropertiesById(c, dr);
        }

        private  static void TestAllCustomerPropertiesById(Customer c, DataRow dr)
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
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>();
            Assert.Equal(_data.Customer.Rows.Count, customerList.Count);
        }

        [Fact]
        public void DataTable_ReturnsCorrectListCount()
        {
            var customerList = _data.Customer.MapTo<Customer>();
            Assert.Equal(_data.Customer.Rows.Count, customerList.Count);
        }

        [Fact]
        public void DataReader_AreAllPropertiesMappedCorrectFromAllRows()
        {
            // Rows 5 and beyond hold deliberately bad data, so they are not part of this test.
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>();
            foreach (var c in customerList)
            {
                TestAllCustomerPropertiesById(c);
            }
        }

        [Fact]
        public void DataTable_AreAllPropertiesMappedCorrectFromAllRows()
        {
            var customerList = _data.Customer.MapTo<Customer>();
            foreach (var c in customerList.Where(c => c.Id <= 4))
            {
                TestAllCustomerPropertiesById(c);
            }
        }

        [Fact]
        public void DataRow_AreAllPropertiesMappedCorrectFromAllRows()
        {
            foreach (DataRow dr in _data.Customer.Rows)
            {
                TestAllCustomerPropertiesById(dr.MapTo<Customer>(), dr);
            }
        }

        [Fact]
        public void DataReader_DoesNullableTypeSetNullCorrectly()
        {
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Null(cust.RewardsPoints);
        }

        [Fact]
        public void DataTable_DoesNullableTypeSetNullCorrectly()
        {
            var customerList = _data.Customer.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Null(cust.RewardsPoints);
        }

        [Fact]
        public void DataRow_DoesNullableTypeSetNullCorrectly()
        {
            var cust = _data.Row(_data.Customer, 2).MapTo<Customer>();
            Assert.Null(cust.RewardsPoints);
        }

        [Fact]
        public void DataReader_DoesNonNullableTypeSetDefaultCorrectly()
        {
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataTable_DoesNonNullableTypeSetDefaultCorrectly()
        {
            var customerList = _data.Customer.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataRow_DoesNonNullableTypeSetDefaultCorrectly()
        {
            var cust = _data.Row(_data.Customer, 2).MapTo<Customer>();
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataReader_DoesDBNullOverrideCorrectly()
        {
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo(new Customer { Zip = 0 });
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataTable_DoesDBNullOverrideCorrectly()
        {
            var customerList = _data.Customer.MapTo(new Customer { Zip = 0 });
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataRow_DoesDBNullOverrideCorrectly()
        {
            var cust = _data.Row(_data.Customer, 2).MapTo(new Customer { Zip = 0 });
            Assert.Equal(0, cust.Zip);
        }

        [Fact]
        public void DataReader_DoesDBNullReturnCorrectlyForString()
        {
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(string.Empty, cust.PhoneNumber);
        }

        [Fact]
        public void DataTable_DoesDBNullReturnCorrectlyForString()
        {
            var customerList = _data.Customer.MapTo<Customer>();
            var cust = customerList.First(c => c.Id == 2);
            Assert.Equal(string.Empty, cust.PhoneNumber);
        }

        [Fact]
        public void DataRow_DoesDBNullReturnCorrectlyForString()
        {
            var cust = _data.Row(_data.Customer, 2).MapTo<Customer>();
            Assert.Equal(string.Empty, cust.PhoneNumber);
        }

        [Fact]
        public void DataReader_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            using var reader = _data.Customer.CreateDataReader();
            var customerList = reader.MapTo<Customer>(false);
            var cust = customerList.First(c => c.Id == 2);
            Assert.Null(cust.PhoneNumber);
        }

        [Fact]
        public void DataTable_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            var customerList = _data.Customer.MapTo<Customer>(false);
            var cust = customerList.First(c => c.Id == 2);
            Assert.Null(cust.PhoneNumber);
        }

        [Fact]
        public void DataRow_DoesDBNullReturnCorrectlyForStringWithNullOverrideTurnedOff()
        {
            var cust = _data.Row(_data.Customer, 2).MapTo<Customer>(false);
            Assert.Null(cust.PhoneNumber);
        }

        [Fact]
        public void ConvertTo_DoesReturnEmptyStringForDBNull()
        {
            var phone = _data.Row(_data.Customer, 2).ConvertTo<string>(nameof(Customer.PhoneNumber));
            Assert.Equal(string.Empty, phone);
        }

        [Fact]
        public void ConvertTo_DoesNullValueOverrideCorrectly()
        {
            var zip = _data.Row(_data.Customer, 2).ConvertTo(nameof(Customer.Zip), 11111);
            Assert.Equal(11111, zip);
        }

        [Fact]
        public void ConvertTo_DoesFieldWithValueMapCorrectly()
        {
            var firstName = _data.Row(_data.Customer, 1).ConvertTo<string>(nameof(Customer.FirstName));
            Assert.Equal(_data.Row(_data.Customer, 1)[nameof(Customer.FirstName)], firstName);
        }

        [Fact]
        public void ConvertTo_DoesDbNullStringOverrideToNull()
        {
            var phone = _data.Row(_data.Customer, 2).ConvertTo<string>(nameof(Customer.PhoneNumber), null);
            Assert.Null(phone);
        }
    }
}
