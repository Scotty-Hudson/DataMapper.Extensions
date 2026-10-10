using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Xunit;
using DataMapper.Extensions;

namespace DataMapper.Extensions.Tests
{
    public class ParameterTests
    {
        private static DbCommand NewCommand() => new FakeConnection().CreateCommand();

        private static DbParameter Get(DbCommand command, string name) => (DbParameter)command.Parameters[name];

        [Fact]
        public void Anonymous_Type_Uses_Member_Names()
        {
            var command = NewCommand();
            var added = command.AddParameters(new { id = 42, name = "Scot" });

            Assert.Equal(2, added);
            Assert.Equal("id", Get(command, "id").ParameterName);
            Assert.Equal(42, Get(command, "id").Value);
            Assert.Equal("Scot", Get(command, "name").Value);
        }

        [Fact]
        public void Read_Only_Members_Are_Included()
        {
            // The mapper skips members it cannot write, so a read-only type maps to nothing.
            // Parameters read values out of the object, so the same type binds fine. This is what
            // makes anonymous types work here: they are entirely read-only.
            var command = NewCommand();
            var added = command.AddParameters(new ReadOnlyParameterSource(7, "record"));

            Assert.Equal(2, added);
            Assert.Equal(7L, Get(command, "Id").Value);
            Assert.Equal("record", Get(command, "Name").Value);
        }

        [Fact]
        public void Column_Attribute_Renames_The_Parameter()
        {
            var command = NewCommand();
            var account = new Account { Id = 9, DisplayName = "name", Secret = "hidden" };
            command.AddParameters(account);

            Assert.Equal("account_id", Get(command, "account_id").ParameterName);
            Assert.Equal(9L, Get(command, "account_id").Value);
            Assert.Equal("name", Get(command, "display_name").Value);
        }

        [Fact]
        public void IgnoreMember_Is_Excluded()
        {
            var command = NewCommand();
            command.AddParameters(new Account { Id = 1, DisplayName = "x", Secret = "hidden" });

            Assert.False(command.Parameters.Contains("Secret"));

            // account_id, display_name, Status, Key, Balance, Duration and Note. Secret is the
            // only member excluded.
            Assert.Equal(7, command.Parameters.Count);
        }

        [Fact]
        public void Public_Fields_Are_Included()
        {
            var command = NewCommand();
            command.AddParameters(new FieldOnlyRecord { Id = 3, Name = "field" });

            Assert.Equal(2, command.Parameters.Count);
            Assert.Equal(3L, Get(command, "Id").Value);
            Assert.Equal("field", Get(command, "Name").Value);
        }

        [Fact]
        public void Null_Becomes_DbNull()
        {
            var command = NewCommand();
            command.AddParameters(new { id = (string)null });

            Assert.Equal(DBNull.Value, Get(command, "id").Value);
        }

        [Fact]
        public void Null_Parameter_Object_Adds_Nothing()
        {
            var command = NewCommand();
            Assert.Equal(0, command.AddParameters(null));
            Assert.Empty(command.Parameters);
        }

        [Fact]
        public void Enum_Is_Bound_As_Its_Underlying_Value()
        {
            var command = NewCommand();
            command.AddParameters(new Account { Status = Status.Inactive });

            var parameter = Get(command, "Status");
            Assert.Equal(1, parameter.Value);
            Assert.Equal(DbType.Int32, parameter.DbType);
        }

        [Fact]
        public void Nullable_Member_With_A_Value_Keeps_Its_DbType()
        {
            var command = NewCommand();
            command.AddParameters(new Account { Balance = 500 });

            Assert.Equal(500, Get(command, "Balance").Value);
            Assert.Equal(DbType.Int32, Get(command, "Balance").DbType);
        }

        [Fact]
        public void Nullable_Member_Without_A_Value_Is_DbNull()
        {
            var command = NewCommand();
            command.AddParameters(new Account { Balance = null });

            Assert.Equal(DBNull.Value, Get(command, "Balance").Value);
        }

        [Fact]
        public void DbType_Is_Set_For_Known_Types()
        {
            var command = NewCommand();
            command.AddParameters(new
            {
                bytes = new byte[] { 1, 2 },
                boolean = true,
                decimalValue = 1.25m,
                doubleValue = 2.5d,
                floatValue = 3.5f,
                guid = Guid.NewGuid(),
                intValue = 1,
                longValue = 2L,
                shortValue = (short)3,
                stringValue = "text"
            });

            Assert.Equal(DbType.Binary, Get(command, "bytes").DbType);
            Assert.Equal(DbType.Boolean, Get(command, "boolean").DbType);
            Assert.Equal(DbType.Decimal, Get(command, "decimalValue").DbType);
            Assert.Equal(DbType.Double, Get(command, "doubleValue").DbType);
            Assert.Equal(DbType.Single, Get(command, "floatValue").DbType);
            Assert.Equal(DbType.Guid, Get(command, "guid").DbType);
            Assert.Equal(DbType.Int32, Get(command, "intValue").DbType);
            Assert.Equal(DbType.Int64, Get(command, "longValue").DbType);
            Assert.Equal(DbType.Int16, Get(command, "shortValue").DbType);
            Assert.Equal(DbType.String, Get(command, "stringValue").DbType);
        }

        [Fact]
        public void DateTime_And_TimeSpan_Leave_DbType_At_Object()
        {
            // Providers infer these from the value, which is why the type map does not cover them.
            var command = NewCommand();
            command.AddParameters(new Account { Duration = TimeSpan.FromMinutes(2) });

            Assert.Equal(DbType.Object, Get(command, "Duration").DbType);
            Assert.Equal(TimeSpan.FromMinutes(2), Get(command, "Duration").Value);
        }

        [Fact]
        public void Dictionary_Source_Uses_The_Keys_As_Names()
        {
            var command = NewCommand();
            var values = new Dictionary<string, object> { ["id"] = 42, ["name"] = "Scot" };

            Assert.Equal(2, command.AddParameters(values));
            Assert.Equal(42, Get(command, "id").Value);
            Assert.Equal("Scot", Get(command, "name").Value);
        }

        [Fact]
        public void Null_Member_Still_Gets_Its_DbType_From_The_Declared_Type()
        {
            // The declared type is known even when the value is null, which is what a provider
            // needs for a null parameter. AddParameter(value, name) cannot do this, because a
            // null argument carries no type.
            var command = NewCommand();
            command.AddParameters(new Account { DisplayName = null });

            Assert.Equal(DBNull.Value, Get(command, "display_name").Value);
            Assert.Equal(DbType.String, Get(command, "display_name").DbType);
        }

        [Fact]
        public void Custom_Converter_Is_Applied()
        {
            var options = new MapOptions
            {
                Converter = (value, type) => type == typeof(Status) ? (int)value * 100 : null
            };

            var command = NewCommand();
            command.AddParameters(new Account { Status = Status.Active }, options);

            Assert.Equal(0, Get(command, "Status").Value);
        }

        [Fact]
        public void Custom_Converter_Cannot_Turn_Null_Into_A_Value()
        {
            // The converter returning null falls through, so a null value is still DBNull.
            var options = new MapOptions { Converter = (value, type) => null };

            var command = NewCommand();
            command.AddParameters(new { id = (string)null }, options);

            Assert.Equal(DBNull.Value, Get(command, "id").Value);
        }

        [Fact]
        public void AddParameter_Adds_A_Single_Parameter()
        {
            var command = NewCommand();
            var parameter = command.AddParameter(42, "id");

            Assert.Same(parameter, Get(command, "id"));
            Assert.Equal(42, parameter.Value);
            Assert.Equal(DbType.Int32, parameter.DbType);
        }

        [Fact]
        public void AddParameter_Without_A_Name_Uses_An_Empty_Name()
        {
            var command = NewCommand();
            var parameter = command.AddParameter(42);

            Assert.Equal(string.Empty, parameter.ParameterName);
            Assert.Single(command.Parameters);
        }

        [Fact]
        public void AddParameter_With_A_Null_Value_Has_No_Type_To_Infer()
        {
            var command = NewCommand();
            var parameter = command.AddParameter(null, "id");

            Assert.Equal(DBNull.Value, parameter.Value);
            Assert.Equal(DbType.Object, parameter.DbType);
        }

        [Fact]
        public void AddParameter_Leaves_DbType_At_Object_For_An_Unmapped_Type()
        {
            var command = NewCommand();
            var parameter = command.AddParameter(DateTime.Now, "when");

            Assert.Equal(DbType.Object, parameter.DbType);
        }

        [Fact]
        public void AddParameter_Boxes_An_Enum_When_No_Options_Are_Passed()
        {
            var command = NewCommand();
            var parameter = command.AddParameter(Status.Inactive, "status");

            Assert.Equal(1, parameter.Value);
            Assert.Equal(DbType.Int32, parameter.DbType);
        }

        [Fact]
        public void Null_Command_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ((DbCommand)null).AddParameter(1, "id"));
            Assert.Throws<ArgumentNullException>(() => ((DbCommand)null).AddParameters(new { id = 1 }));
        }
    }
}
