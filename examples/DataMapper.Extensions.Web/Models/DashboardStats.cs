using DataMapper.Extensions;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>Mapped from a single aggregate row.</summary>
    public class DashboardStats
    {
        [Column("total_jobs")] public long TotalJobs { get; set; }
        [Column("completed")] public long Completed { get; set; }
        [Column("open_jobs")] public long OpenJobs { get; set; }
        [Column("total_amount")] public decimal TotalAmount { get; set; }
        [Column("null_notes")] public long NullNotes { get; set; }
        [Column("null_customer_name")] public long NullCustomerName { get; set; }
        [Column("technicians")] public long Technicians { get; set; }
    }
}
