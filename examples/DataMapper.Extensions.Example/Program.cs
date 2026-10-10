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
        private const int Rows = 1000;

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
            DefaultPoco(connection);
        }

        private static void InsertRows(SqliteConnection connection)
        {
            // Every tenth row has no display name and every seventh has no balance, so the null
            // policy has something to resolve when the rows are read back.
            var accounts = Enumerable.Range(1, Rows).Select(i => new Account
            {
                Id = i,
                DisplayName = i % 10 == 0 ? null : $"account-{i}",
                Status = (Status)(i % 3),
                Key = new Guid($"00000000-0000-0000-0000-{i:D12}"),
                Balance = i % 7 == 0 ? null : i * 10,
                Duration = TimeSpan.FromMinutes(i),
                CreatedOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                IsActive = i % 2 == 0,
                InternalNote = "never a column"
            });

            using var transaction = connection.BeginTransaction();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into account (account_id, display_name, status, account_key, balance,
                                     duration, created_on, is_active)
                values (@account_id, @display_name, @status, @account_key, @balance,
                        @duration, @created_on, @is_active)
                """;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var account in accounts)
            {
                command.Parameters.Clear();

                // Names come from the Column attributes, the enum is bound as its underlying value,
                // and a null becomes DBNull.Value. DateTime and TimeSpan are left at DbType.Object
                // by the library, so the String shown below is Microsoft.Data.Sqlite inferring the
                // type from the value, which is how it stores them in SQLite.
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

            stopwatch.Stop();
            transaction.Commit();

            Console.WriteLine($"Inserted {Rows} rows in {stopwatch.Elapsed.TotalMilliseconds:F1} ms, commit included.");
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
            Console.WriteLine("First five rows, streamed with EnumerateTo:");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int count = 0;
            long balanceSum = 0;

            using (var reader = command.ExecuteReader())
                foreach (var account in reader.EnumerateTo<Account>(options))
                {
                    count++;
                    balanceSum += account.Balance ?? 0;

                    if (count <= 5)
                        Console.WriteLine($"  {account.Id} {account.DisplayName} {account.Status} " +
                                          $"{account.Key} {account.Balance} {account.Duration} " +
                                          $"{account.CreatedOn:O} {account.IsActive}");
                }

            stopwatch.Stop();

            Console.WriteLine($"  {count} rows mapped in {stopwatch.Elapsed.TotalMilliseconds:F1} ms, " +
                              $"balance sum {balanceSum}");
        }

        private static void NullPolicy(SqliteConnection connection)
        {
            // 100 of the rows have no display name, so the policy has something to resolve across
            // the whole result set rather than on one row.
            (int Null, int Empty) Count(MapOptions options)
            {
                int nulls = 0;
                int empty = 0;

                using var command = connection.CreateCommand();
                command.CommandText = "select * from account";

                using var reader = command.ExecuteReader();
                foreach (var account in reader.EnumerateTo<Account>(options))
                    if (account.DisplayName == null) nulls++;
                    else if (account.DisplayName.Length == 0) empty++;

                return (nulls, empty);
            }

            Console.WriteLine();
            Console.WriteLine("Null policy across the whole result set:");

            var defaultCounts = Count(MapOptions.Default);
            var nullCounts = Count(new MapOptions { NullHandling = NullHandling.Null });

            Console.WriteLine($"  default (EmptyString): {defaultCounts.Empty} empty, {defaultCounts.Null} null");
            Console.WriteLine($"  NullHandling.Null:     {nullCounts.Empty} empty, {nullCounts.Null} null");
        }

        private static void DefaultPoco(SqliteConnection connection)
        {
            // A default POCO is the only way to get a value other than the policy default: the
            // clone starts from it, and a member that already has a value is left alone.
            // Rows that are a multiple of both 7 and 10 have neither a display name nor a balance,
            // so both defaults are visible on the same row.
            var defaults = new Account { DisplayName = "unknown", Balance = 0 };

            using var command = connection.CreateCommand();
            command.CommandText = "select * from account where display_name is null and balance is null order by account_id";

            Console.WriteLine();
            Console.WriteLine("Default POCO on the rows that come back null:");

            using var reader = command.ExecuteReader();
            foreach (var account in reader.EnumerateTo<Account>(defaults, MapOptions.Default).Take(3))
                Console.WriteLine($"  {account.Id} {Show(account.DisplayName)} balance {account.Balance}");
        }

        private static void Execute(SqliteConnection connection, string sql, object parameters = null)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.AddParameters(parameters);
            command.ExecuteNonQuery();
        }

        // A null and an empty string both print as "" in an interpolated string, so the example
        // needs a helper that can tell them apart. Strings are quoted, DBNull is named, and other
        // values print plainly.
        private static string Show(object value) => value switch
        {
            null => "null",
            DBNull => "DBNull",
            string text => $"\"{text}\"",
            _ => value.ToString()
        };
    }
}
