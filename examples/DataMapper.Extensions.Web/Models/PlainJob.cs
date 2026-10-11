using DataMapper.Extensions;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// The same columns as <see cref="Job"/>, but with no NullAs on notes, so the option applies to
    /// that member. The null policy page uses this as the control: the only difference between the
    /// two models is the attribute, and the only difference in the output is notes.
    /// </summary>
    public class PlainJob
    {
        [Column("job_id")] public long JobId { get; set; }
        [Column("customer_name")] public string CustomerName { get; set; } = string.Empty;
        [Column("notes")] public string? Notes { get; set; }
        [Column("display_label")] public string DisplayLabel { get; set; } = string.Empty;
        [Column("amount")] public decimal? Amount { get; set; }
    }
}
