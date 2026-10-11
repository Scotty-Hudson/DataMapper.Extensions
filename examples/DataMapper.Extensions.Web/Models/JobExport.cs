using DataMapper.Extensions;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// A positional record. Records map the same way classes do, and the same attributes drive
    /// the reverse direction: these names are what ToDataTable produces as columns and what
    /// AddParameters produces as parameter names.
    ///
    /// A positional record has no parameterless constructor, so it is created uninitialized and
    /// field initializers do not run. That is why nothing here has a default value.
    /// </summary>
    public record JobExport(
        [property: Column("job_id")] long JobId,
        [property: Column("customer_name")] string CustomerName,
        [property: Column("status")] JobStatus Status,
        [property: Column("job_key")] Guid Key,
        [property: Column("scheduled_on")] DateOnly ScheduledOn,
        [property: Column("start_time")] TimeOnly StartTime,
        [property: Column("duration")] TimeSpan Duration,
        [property: Column("amount")] decimal? Amount,
        [property: Column("notes")] string? Notes);
}
