using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using DataMapper.Extensions;
using Microsoft.Data.Sqlite;

namespace DataMapper.Extensions.Example
{
    /// <summary>
    /// The same attributes drive both directions. The Column names the mapper reads are the names
    /// the parameter builder produces, so the parameter names in the SQL match the column names in
    /// the table, and IgnoreMember is left out of both.
    /// </summary>
    public class Account
    {
        [Column("account_id")] public long Id { get; set; }
        [Column("display_name")] public string DisplayName { get; set; }
        [Column("status")] public Status Status { get; set; }
        [Column("account_key")] public Guid Key { get; set; }
        [Column("balance")] public int? Balance { get; set; }
        [Column("duration")] public TimeSpan Duration { get; set; }
        [Column("created_on")] public DateTime CreatedOn { get; set; }
        [Column("is_active")] public bool IsActive { get; set; }

        // Not a column, so it is left out of the parameters and out of the mapping.
        [IgnoreMember] public string InternalNote { get; set; }
    }

    public enum Status { Active, Inactive, Suspended }

    public static class Program
    {
        public static void Main()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            Execute(connection, """
                create table account (
                    account_id   integer,
                    display_name text,
                    status       integer,
                    account_key  text,
                    balance      integer,
                    duration     text,
                    created_on   text,
                    is_active    integer
                )
                """);

            InsertRows(connection);
            ReadRows(connection);
            NullPolicy(connection);
        }

        private static void InsertRows(SqliteConnection connection)
        {
            var accounts = new[]
            {
                new Account
                {
                    Id = 1,
                    DisplayName = "Scot",
                    Status = Status.Active,
                    Key = Guid.NewGuid(),
                    Balance = 120,
                    Duration = TimeSpan.FromMinutes(90),
                    CreatedOn = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc),
                    IsActive = true,
                    InternalNote = "never a column"
                },
                new Account
                {
                    Id = 2,
                    DisplayName = "Hudson",
                    Status = Status.Inactive,
                    Key = Guid.NewGuid(),
                    Balance = null,
                    Duration = TimeSpan.FromMinutes(15),
                    CreatedOn = new DateTime(2026, 3, 2, 11, 5, 0, DateTimeKind.Utc),
                    IsActive = false,
                    InternalNote = "never a column"
                }
            };

            using var command = connection.CreateCommand();
            command.CommandText = """
                insert into account (account_id, display_name, status, account_key, balance,
                                     duration, created_on, is_active)
                values (@account_id, @display_name, @status, @account_key, @balance,
                        @duration, @created_on, @is_active)
                """;

            foreach (var account in accounts)
            {
                command.Parameters.Clear();

                // Names come from the Column attributes, the enum is bound as its underlying value,
                // and a null Balance becomes DBNull.Value. DateTime and TimeSpan are left at
                // DbType.Object by the library, so the String printed for them below is
                // Microsoft.Data.Sqlite inferring the type from the value, which is how it stores
                // them in SQLite.
                command.AddParameters(account);

                if (account.Id == 1)
                {
                    Console.WriteLine();
                    Console.WriteLine("Parameters built from the model:");
                    foreach (DbParameter parameter in command.Parameters)
                        Console.WriteLine($"  {parameter.ParameterName} = {Show(parameter.Value)} ({parameter.DbType})");
                }

                command.ExecuteNonQuery();
            }

            Console.WriteLine($"Inserted {accounts.Length} rows.");
        }

        private static void ReadRows(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "select * from account where status = @status order by account_id";

            // An anonymous type is entirely read-only, which is why the parameter builder reads
            // members the mapper would skip.
            command.AddParameters(new { status = Status.Active });

            Console.WriteLine();
            Console.WriteLine("Parameters bound for the query:");
            foreach (DbParameter parameter in command.Parameters)
                Console.WriteLine($"  {parameter.ParameterName} = {Show(parameter.Value)} ({parameter.DbType})");

            // SQLite columns have no real types, so a text column reads back as a string. That is
            // what puts the Guid, TimeSpan and DateTime conversions to work.
            var options = new MapOptions { MatchNormalizedNames = true };

            Console.WriteLine();
            Console.WriteLine("Mapped rows:");
            using var reader = command.ExecuteReader();
            foreach (var account in reader.EnumerateTo<Account>(options))
                Console.WriteLine($"  {account.Id} {account.DisplayName} {account.Status} " +
                                  $"{account.Key} {account.Balance} {account.Duration} " +
                                  $"{account.CreatedOn:O} {account.IsActive}");
        }

        private static void NullPolicy(SqliteConnection connection)
        {
            // AddParameters with no parameters object adds nothing, so this is the same helper
            // used for a plain Execute.
            Execute(connection,
                "insert into account (account_id, display_name, status) values (@account_id, @display_name, @status)",
                new { account_id = 3, display_name = (string)null, status = Status.Suspended });

            Account Map(MapOptions options)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "select * from account where account_id = @account_id";
                command.AddParameters(new { account_id = 3 });

                using var reader = command.ExecuteReader();
                return reader.EnumerateTo<Account>(options).First();
            }

            Console.WriteLine();
            Console.WriteLine("Null policy on a null display_name:");
            Console.WriteLine($"  default (EmptyString): {Show(Map(MapOptions.Default).DisplayName)}");
            Console.WriteLine($"  NullHandling.Null:     {Show(Map(new MapOptions { NullHandling = NullHandling.Null }).DisplayName)}");
        }

        // A null and an empty string both print as "" in an interpolated string, so the example
        // needs a helper that can tell them apart. Strings are quoted; other values print plainly.
        private static string Show(object value) => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            _ => value.ToString()
        };

        private static void Execute(SqliteConnection connection, string sql, object parameters = null)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.AddParameters(parameters);
            command.ExecuteNonQuery();
        }
    }
}
