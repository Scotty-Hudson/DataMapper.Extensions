using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DataMapper.Extensions;
using DataMapper.Extensions.Web.Models;

namespace DataMapper.Extensions.Web.Data
{
    /// <summary>
    /// The whole data layer of the app. There is no ORM here: every row is mapped by the
    /// extension methods, and every parameter is bound by the parameter builder. The SQL, the
    /// connection lifetime and the transaction stay in this class, which is exactly the boundary
    /// the package draws — it builds parameters and maps values, it does not execute anything.
    /// </summary>
    public sealed class JobRepository
    {
        // The columns are snake_case, so normalized matching is what lets a member named
        // CustomerName read the column customer_name.
        private static readonly MapOptions Options = new() { MatchNormalizedNames = true };

        private readonly IDatabase _database;

        public JobRepository(IDatabase database) => _database = database;

        public async Task<QueryResult> QueryAsync(JobFilter filter, CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();

            using var connection = _database.Open();
            using var command = connection.CreateCommand();

            var where = BuildWhere(filter);
            command.CommandText = "select * from job " + where.Sql + " order by job_id limit @limit offset @offset";

            // Only the filters the visitor actually used. AddParameters binds every member it is
            // given, so a filter that is not in the SQL must not be in the dictionary either:
            // a NULL parameter in "status = @status" would match nothing.
            command.AddParameters(where.Parameters);
            command.AddParameter(filter.PageSize, "limit");
            command.AddParameter((filter.Page - 1) * filter.PageSize, "offset");

            List<Job> jobs;
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                jobs = await reader.MapToAsync<Job>(Options, cancellationToken);

            var total = CountTotal(connection, filter);

            return new QueryResult(
                command.CommandText,
                ParameterInspector.Inspect(command, "filter"),
                jobs,
                filter.Page,
                filter.PageSize,
                total,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        /// <summary>
        /// One row as a DataRow, which is the only path that has ConvertTo. The page maps the row
        /// onto the model and then converts single columns, so both are visible.
        /// </summary>
        public async Task<DataRow> GetRowAsync(long id, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select * from job where job_id = @job_id";
            command.AddParameter(id, "job_id");

            var table = new DataTable("Job");
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                table.Load(reader);

            return table.Rows.Count > 0 ? table.Rows[0] : null!;
        }

        /// <summary>
        /// One record at a time, nothing buffered. This is what a large result set needs: MapTo
        /// materialises the whole thing, EnumerateTo does not.
        /// </summary>
        public async IAsyncEnumerable<Job> StreamAsync(JobFilter filter, int limit,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();

            var where = BuildWhere(filter);
            command.CommandText = "select * from job " + where.Sql + " order by job_id limit @limit";
            command.AddParameters(where.Parameters);
            command.AddParameter(limit, "limit");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // The token is checked between records, so a caller that stops early stops the read
            // rather than draining the result set.
            await foreach (var job in reader.EnumerateToAsync<Job>(Options, cancellationToken))
                yield return job;
        }

        /// <summary>
        /// One chunk of a streamed result set, continuing from a cursor. htmx asks for the next
        /// chunk when the previous one is revealed, so the page fills up without a circuit. Each
        /// chunk is its own request, which is why the cursor is a job_id rather than a live reader:
        /// nothing can survive between requests.
        /// </summary>
        public async Task<StreamResult> GetChunkAsync(JobFilter filter, int after, int chunk, int? cancelAfter,
            CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();

            var where = BuildWhere(filter);
            command.CommandText = "select * from job " + where.Sql +
                (after > 0 ? " and job_id > @after" : string.Empty) + " order by job_id limit @limit";

            command.AddParameters(where.Parameters);

            if (after > 0) command.AddParameter(after, "after");
            command.AddParameter(chunk, "limit");

            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var started = Stopwatch.GetTimestamp();
            var rows = new List<StreamRow>();
            var outcome = string.Empty;

            try
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                await foreach (var job in reader.EnumerateToAsync<Job>(Options, source.Token))
                {
                    rows.Add(new StreamRow(rows.Count + 1, job, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));

                    if (cancelAfter is > 0 && rows.Count == cancelAfter)
                    {
                        outcome = $", then cancelled after {rows.Count} records";
                        source.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The token is checked between records, so this is where an early stop lands.
            }

            var last = rows.Count > 0 ? (int)rows[^1].Job.JobId : after;

            return new StreamResult(rows, last, outcome.Length == 0 && rows.Count == chunk, chunk,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome);
        }

        public async Task<List<JobExport>> ExportRowsAsync(JobFilter filter, int limit, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();

            var where = BuildWhere(filter);
            command.CommandText = "select * from job " + where.Sql + " order by job_id limit @limit";
            command.AddParameters(where.Parameters);
            command.AddParameter(limit, "limit");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = await reader.MapToAsync<Job>(Options, cancellationToken);

            return rows.Select(ToExport).ToList();
        }

        public async Task<DataTable> ExportTableAsync(JobFilter filter, int limit, CancellationToken cancellationToken)
            => (await ExportRowsAsync(filter, limit, cancellationToken)).ToDataTable("JobExport");

        /// <summary>
        /// The reverse direction. On SQL Server this is one SqlBulkCopy.WriteToServerAsync(reader)
        /// call; SQLite has no bulk copy, so the same reader is read row by row inside a
        /// transaction. The parameter names come from the same Column attributes that produced the
        /// columns, which is why the insert statement and the record agree.
        /// </summary>
        public async Task<int> ArchiveAsync(IEnumerable<JobExport> rows, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var transaction = connection.BeginTransaction();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into archive (job_id, customer_name, status, job_key, scheduled_on,
                                     start_time, duration, amount, notes, archived_on)
                values (@job_id, @customer_name, @status, @job_key, @scheduled_on,
                        @start_time, @duration, @amount, @notes, @archived_on)
                """;

            using var reader = rows.ToDataReader("JobExport");
            var inserted = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                command.Parameters.Clear();

                for (var i = 0; i < reader.FieldCount; i++)
                    command.AddParameter(reader.GetValue(i), reader.GetName(i));

                command.AddParameter(DateTime.UtcNow, "archived_on");
                await command.ExecuteNonQueryAsync(cancellationToken);
                inserted++;
            }

            transaction.Commit();
            return inserted;
        }

        /// <summary>One aggregate row mapped onto a POCO.</summary>
        public async Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();

            command.CommandText = """
                select count(*)                                        as total_jobs,
                       sum(case when status = 3 then 1 else 0 end)    as completed,
                       sum(case when status in (0, 1, 2) then 1 else 0 end) as open_jobs,
                       coalesce(sum(cast(amount as real)), 0)         as total_amount,
                       sum(case when notes is null then 1 else 0 end) as null_notes,
                       sum(case when customer_name is null then 1 else 0 end) as null_customer_name,
                       count(distinct technician_id)                  as technicians
                from job
                """;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var stats = await reader.MapToAsync<DashboardStats>(Options, cancellationToken);

            return stats[0];
        }

        /// <summary>
        /// Rows chosen so every policy has something to resolve: 10, 20 and 30 have no customer
        /// name and no notes, 7, 14 and 21 have no amount. The template POCO is what the Keep
        /// policy keeps, and what the default policy falls back to.
        /// </summary>
        public async Task<List<Job>> GetSampleAsync(Job template, MapOptions options, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select * from job where job_id in (7, 10, 14, 20, 21, 30, 42) order by job_id";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.MapToAsync<Job>(template, options, cancellationToken);
        }

        public async Task<List<PlainJob>> GetPlainSampleAsync(PlainJob template, MapOptions options, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select * from job where job_id in (7, 10, 14, 20, 21, 30, 42) order by job_id";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.MapToAsync<PlainJob>(template, options, cancellationToken);
        }

        public async Task<List<Technician>> GetTechniciansAsync(MapOptions options, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select * from technician order by technician_id limit 8";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.MapToAsync<Technician>(options, cancellationToken);
        }

        public async Task<List<JobExport>> GetArchivedAsync(int limit, CancellationToken cancellationToken)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select * from archive order by archived_on desc limit @limit";
            command.AddParameter(limit, "limit");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.MapToAsync<JobExport>(Options, cancellationToken);
        }

        private static (string Sql, Dictionary<string, object> Parameters) BuildWhere(JobFilter filter)
        {
            // The names in this dictionary are the names in the SQL, and they are the Column
            // names on the filter, so there is one source of truth for both.
            var sql = new StringBuilder("where 1 = 1");
            var parameters = new Dictionary<string, object>();

            if (filter.Status.HasValue)
            {
                sql.Append(" and status = @status");
                parameters["status"] = filter.Status;
            }

            if (filter.TechnicianId.HasValue)
            {
                sql.Append(" and technician_id = @technician_id");
                parameters["technician_id"] = filter.TechnicianId;
            }

            if (!string.IsNullOrWhiteSpace(filter.CustomerLike))
            {
                sql.Append(" and customer_name like @customer_like");
                parameters["customer_like"] = $"%{filter.CustomerLike}%";
            }

            if (filter.FromDate.HasValue)
            {
                sql.Append(" and scheduled_on >= @from_date");
                parameters["from_date"] = filter.FromDate.Value.ToString("yyyy-MM-dd");
            }

            if (filter.MinAmount.HasValue)
            {
                sql.Append(" and cast(amount as real) >= @min_amount");
                parameters["min_amount"] = filter.MinAmount;
            }

            return (sql.ToString(), parameters);
        }

        private static long CountTotal(DbConnection connection, JobFilter filter)
        {
            var where = BuildWhere(filter);

            using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from job " + where.Sql;
            command.AddParameters(where.Parameters);
            return (long)(command.ExecuteScalar() ?? 0L);
        }

        private static JobExport ToExport(Job job) => new(
            job.JobId,
            job.CustomerName,
            job.Status,
            job.Key,
            job.ScheduledOn,
            job.StartTime,
            job.Duration,
            job.Amount,
            job.Notes);
    }
}
