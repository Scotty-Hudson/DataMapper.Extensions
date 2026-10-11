using DataMapper.Extensions;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// A job row. Every attribute here is the same rule the parameter builder uses, so the
    /// column names in the SQL and the parameter names on the command come from one source,
    /// and <see cref="IgnoreMemberAttribute"/> members are left out of both.
    /// </summary>
    public class Job
    {
        [Column("job_id")] public long JobId { get; set; }

        // Non-nullable, so the default policy guarantees this is never null: a DBNull column
        // comes back as an empty string instead.
        [Column("customer_name")] public string CustomerName { get; set; } = string.Empty;

        // An enum from an integer column.
        [Column("status")] public JobStatus Status { get; set; }

        // A Guid from a text column, which Convert.ChangeType alone cannot do.
        [Column("job_key")] public Guid Key { get; set; }

        // DateOnly and TimeOnly are not IConvertible. SQLite has no real column types, so both
        // come back as strings and the extensions parse them.
        [Column("scheduled_on")] public DateOnly ScheduledOn { get; set; }
        [Column("start_time")] public TimeOnly StartTime { get; set; }
        [Column("duration")] public TimeSpan Duration { get; set; }

        [Column("amount")] public decimal? Amount { get; set; }
        [Column("technician_id")] public long TechnicianId { get; set; }

        // Genuinely optional, so this one is allowed to stay null. The Column is not needed for
        // mapping, which is case insensitive, but it is needed for parameters: SQLite matches
        // parameter names exactly, and the SQL says @notes.
        [Column("notes")]
        [NullAs(NullHandling.Null)]
        public string? Notes { get; set; }

        // A padded column, which is what the custom converter on the Options page has something
        // to do with.
        [Column("display_label")] public string DisplayLabel { get; set; } = string.Empty;

        // Never a column and never a parameter.
        [IgnoreMember] public string InternalNote { get; set; } = "computed in the app, not in the database";
    }
}
