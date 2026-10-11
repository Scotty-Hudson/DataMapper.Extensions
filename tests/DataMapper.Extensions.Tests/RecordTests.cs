using System;
using System.Collections.Generic;
using System.Data;
using Xunit;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// Records map the same way classes do: they are reference types, and the positional and
    /// init accessors are writable through reflection. These tests pin that behaviour, and pin
    /// the two shapes that do not work.
    /// </summary>
    public class RecordTests
    {
        private static DataTable BuildTable(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();
            foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
            return table;
        }

        private static DataTable BuildRecordTable()
        {
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "John");
            table.Rows.Add(2L, DBNull.Value);
            return table;
        }

        [Fact]
        public void PositionalRecordMapsFromARow()
        {
            var record = BuildRecordTable().Rows[0].MapTo<PositionalRecord>();
            Assert.Equal(1L, record.Id);
            Assert.Equal("John", record.Name);
        }

        [Fact]
        public void PositionalRecordMapsFromAList()
        {
            var records = BuildRecordTable().MapTo<PositionalRecord>();
            Assert.Equal(2, records.Count);
            Assert.Equal("John", records[0].Name);
            Assert.Equal(string.Empty, records[1].Name);   // DBNull, null string rule
        }

        [Fact]
        public void PositionalRecordMapsThroughAReader()
        {
            using var reader = BuildRecordTable().CreateDataReader();
            var records = reader.MapTo<PositionalRecord>();
            Assert.Equal(2, records.Count);
            Assert.Equal("John", records[0].Name);
        }

        [Fact]
        public void RecordUsesTheTemplateDefaultsForDBNull()
        {
            var record = BuildRecordTable().Rows[1].MapTo(new PositionalRecord(0, "template"));
            Assert.Equal(2L, record.Id);
            Assert.Equal("template", record.Name);
        }

        [Fact]
        public void RecordAttributesAreUsed()
        {
            var table = BuildTable(("account_id", typeof(long)), ("Secret", typeof(string)));
            table.Rows.Add(7L, "should be ignored");

            var record = table.Rows[0].MapTo<RecordWithAttributes>();
            Assert.Equal(7L, record.Id);
            Assert.Null(record.Secret);
        }

        [Fact]
        public void RecordRoundTripPreservesValues()
        {
            var records = new List<PositionalRecord> { new PositionalRecord(1, "John") };
            var back = records.ToDataTable().MapTo<PositionalRecord>();

            Assert.Single(back);
            Assert.Equal(1L, back[0].Id);
            Assert.Equal("John", back[0].Name);
            Assert.Equal(records[0], back[0]);   // value equality, which records provide
        }

        [Fact]
        public void RecordWithADefaultConstructorRunsFieldInitializers()
        {
            var table = BuildTable(("Id", typeof(long)));
            table.Rows.Add(1L);

            var record = table.Rows[0].MapTo<RecordWithDefaultConstructor>();
            Assert.Equal("default-tag", record.Tag);
        }

        // ---------- the shapes that do not work ----------

        [Fact]
        public void PositionalRecordDoesNotRunFieldInitializers()
        {
            // A positional record has no parameterless constructor, so New<T> uses
            // GetUninitializedObject and the field initializer never runs. The null policy then
            // supplies the empty string. Use a record with a parameterless constructor, or give
            // the initializer a column, if the initializer has to run.
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "John");

            var record = table.Rows[0].MapTo<RecordWithInitializer>();
            Assert.NotEqual("default-tag", record.Tag);
            Assert.Equal(string.Empty, record.Tag);
        }

        [Fact]
        public void ImmutableRecordMembersWithoutSettersAreSkipped()
        {
            // Get-only members have no setter, so the mapper cannot write them and the record
            // keeps its defaults. Mapping through the constructor is not supported. Note that the
            // null policy does not apply either: a member with no setter is not in the map at all,
            // so a read-only string is not guaranteed non-null.
            var table = BuildTable(("Id", typeof(long)), ("Name", typeof(string)));
            table.Rows.Add(1L, "John");

            var record = table.Rows[0].MapTo<ImmutableRecord>();
            Assert.Equal(0L, record.Id);
            Assert.Null(record.Name);
        }
    }
}
