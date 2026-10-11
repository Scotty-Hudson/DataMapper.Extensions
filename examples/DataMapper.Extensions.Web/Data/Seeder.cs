using System.Data;
using DataMapper.Extensions;
using DataMapper.Extensions.Web.Models;
using Microsoft.Data.Sqlite;

namespace DataMapper.Extensions.Web.Data
{
    /// <summary>
    /// Seeds the database once. The data is shaped so that every feature of the package has
    /// something to do: snake_case columns, text-encoded Guid, TimeSpan, DateOnly and TimeOnly,
    /// nulls planted at regular intervals, a decimal string that a German culture reads as a
    /// different number, and padded text for the custom converter.
    /// </summary>
    public static class Seeder
    {
        private const int Rows = 1000;

        public static void Seed(IDatabase database)
        {
            using var connection = database.Open();

            Execute(connection, """
                create table if not exists technician (
                    technician_id integer primary key,
                    first_name    text,
                    last_name     text,
                    email         text,
                    is_active     integer
                )
                """);

            Execute(connection, """
                create table if not exists job (
                    job_id        integer primary key,
                    customer_name text,
                    status        integer,
                    job_key       text,
                    scheduled_on  text,
                    start_time    text,
                    duration      text,
                    amount        text,
                    notes         text,
                    technician_id integer,
                    display_label text
                )
                """);

            Execute(connection, """
                create table if not exists archive (
                    job_id        integer,
                    customer_name text,
                    status        integer,
                    job_key       text,
                    scheduled_on  text,
                    start_time    text,
                    duration      text,
                    amount        text,
                    notes         text,
                    archived_on   text
                )
                """);

            if (Count(connection, "job") == 0)
            {
                SeedTechnicians(connection);
                SeedJobs(connection);
            }
        }

        private static void SeedTechnicians(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();

            // The POCO has no Column attributes, so the parameter names are the member names.
            command.CommandText = """
                insert into technician (technician_id, first_name, last_name, email, is_active)
                values (@TechnicianId, @FirstName, @LastName, @Email, @IsActive)
                """;

            for (var i = 1; i <= 12; i++)
            {
                command.Parameters.Clear();
                command.AddParameters(new Technician
                {
                    TechnicianId = i,
                    FirstName = $"Tech{i}",
                    LastName = i % 4 == 0 ? null! : $"Surname{i}",
                    Email = $"tech{i}@dispatch.example",
                    IsActive = i % 5 != 0
                });
                command.ExecuteNonQuery();
            }
        }

        private static void SeedJobs(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            // The same attributes that produce the columns on the way back out produce these
            // parameter names, so the SQL and the model agree without any mapping code.
            command.CommandText = """
                insert into job (job_id, customer_name, status, job_key, scheduled_on, start_time,
                                 duration, amount, notes, technician_id, display_label)
                values (@job_id, @customer_name, @status, @job_key, @scheduled_on, @start_time,
                        @duration, @amount, @notes, @technician_id, @display_label)
                """;

            for (var i = 1; i <= Rows; i++)
            {
                command.Parameters.Clear();

                command.AddParameters(new Job
                {
                    JobId = i,
                    // Every tenth row has no customer name, so the null policy has something to resolve.
                    CustomerName = i % 10 == 0 ? null! : $"customer-{i}",
                    Status = (JobStatus)(i % 5),
                    Key = new Guid($"00000000-0000-0000-0000-{i:D12}"),
                    ScheduledOn = new DateOnly(2026, 1, 1).AddDays(i % 60),
                    StartTime = new TimeOnly(8, 0).AddMinutes(i % 500),
                    Duration = TimeSpan.FromMinutes(i % 240),
                    // Every seventh row has no amount. Row 43 holds "1.5", which a German culture
                    // reads as 15 because the period is its thousands separator.
                    Amount = i % 7 == 0 ? null : i == 43 ? 1.5m : i * 10,
                    // Null on every tenth row, padded text on every third so the converter has work.
                    Notes = i % 10 == 0 ? null : i % 3 == 0 ? $"  note-{i}  " : $"note-{i}",
                    TechnicianId = i % 12 + 1,
                    DisplayLabel = $"job-{i}"
                });

                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static long Count(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"select count(*) from {table}";
            return (long)(command.ExecuteScalar() ?? 0L);
        }
    }
}
