#nullable enable

using System;
using System.Data;
using Xunit;

namespace DataMapper.Extensions.Tests
{
    public class AnnotatedCustomer
    {
        public long Id { get; set; }

        // Non-nullable: the library guarantees a value, so the annotation is honest.
        public string Name { get; set; } = string.Empty;

        // Annotated nullable: stays null.
        public string? Notes { get; set; }

        // Per-member override, even though the member is annotated non-nullable.
        [DataMapper.Extensions.NullAs(NullHandling.Null)]
        public string ForcedNull { get; set; } = "template";

        // Per-member override: keep whatever the template POCO has.
        [DataMapper.Extensions.NullAs(NullHandling.Keep)]
        public string Kept { get; set; } = "template";
    }

    public class NullHandlingTests
    {
        private static DataRow BuildRow()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Notes", typeof(string));
            table.Columns.Add("ForcedNull", typeof(string));
            table.Columns.Add("Kept", typeof(string));

            table.Rows.Add(1L, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
            return table.Rows[0];
        }

        [Fact]
        public void NonNullableStringIsGuaranteedNonNull()
        {
            var customer = BuildRow().MapTo<AnnotatedCustomer>();
            Assert.Equal(string.Empty, customer.Name);
        }

        [Fact]
        public void AnnotatedNullableStringStaysNull()
        {
            var customer = BuildRow().MapTo<AnnotatedCustomer>();
            Assert.Null(customer.Notes);
        }

        [Fact]
        public void PerMemberOverrideForcesNull()
        {
            var customer = BuildRow().MapTo<AnnotatedCustomer>();
            Assert.Null(customer.ForcedNull);
        }

        [Fact]
        public void PerMemberOverrideKeepsTemplateValue()
        {
            var customer = BuildRow().MapTo<AnnotatedCustomer>();
            Assert.Equal("template", customer.Kept);
        }

        [Fact]
        public void NullHandlingOptionCanTurnThePolicyOffGlobally()
        {
            // Keep means "leave whatever the template POCO has", so a null stays null.
            var template = new AnnotatedCustomer { Name = null! };
            var customer = BuildRow().MapTo(template, new MapOptions { NullHandling = NullHandling.Keep });
            Assert.Null(customer.Name);
        }

        [Fact]
        public void UnannotatedPocosKeepTheOriginalBehaviour()
        {
            // Customer is declared in a file without #nullable enable, so every member is
            // treated as non-nullable, which is what the original bool flag assumed.
            var table = new DataTable();
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Rows.Add(1L, DBNull.Value);

            var customer = table.Rows[0].MapTo<Customer>();
            Assert.Equal(string.Empty, customer.PhoneNumber);
        }
    }
}
