using DataMapper.Extensions;
using Microsoft.Extensions.Primitives;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// The filter from the query string. The Column names here are the parameter names in the
    /// SQL, which is the same rule the mapper uses for columns.
    /// </summary>
    public class JobFilter
    {
        [Column("status")] public JobStatus? Status { get; set; }
        [Column("technician_id")] public long? TechnicianId { get; set; }
        [Column("customer_like")] public string? CustomerLike { get; set; }
        [Column("from_date")] public DateOnly? FromDate { get; set; }
        [Column("min_amount")] public decimal? MinAmount { get; set; }

        // Paging is not a parameter. Without IgnoreMember these would be bound onto the command,
        // and the provider would see parameters the SQL never mentions.
        [IgnoreMember] public int Page { get; set; } = 1;
        [IgnoreMember] public int PageSize { get; set; } = 25;

        public static JobFilter FromQuery(IQueryCollection query)
            => FromValues(key => query.TryGetValue(key, out var value) ? value.ToString() : null);

        public static JobFilter FromForm(IFormCollection form)
            => FromValues(key => form.TryGetValue(key, out var value) ? value.ToString() : null);

        private static JobFilter FromValues(Func<string, string?> get)
        {
            var filter = new JobFilter();

            if (Enum.TryParse<JobStatus>(get("status"), true, out var status)) filter.Status = status;
            if (long.TryParse(get("technician_id"), out var technicianId)) filter.TechnicianId = technicianId;
            if (!string.IsNullOrWhiteSpace(get("customer_like"))) filter.CustomerLike = get("customer_like");
            if (DateOnly.TryParse(get("from_date"), null, out var fromDate)) filter.FromDate = fromDate;
            if (decimal.TryParse(get("min_amount"), null, out var minAmount)) filter.MinAmount = minAmount;

            if (int.TryParse(get("page"), out var page) && page > 1) filter.Page = page;
            if (int.TryParse(get("page_size"), out var pageSize) && pageSize is > 0 and <= 200) filter.PageSize = pageSize;

            return filter;
        }

        /// <summary>The filter rendered back into the query string, so links and forms keep state.</summary>
        public string ToQueryString(int page = 1)
        {
            var parts = FilterParts();

            return $"page={page}&page_size={PageSize}" + (parts.Length > 0 ? $"&{parts}" : string.Empty);
        }

        /// <summary>The filter without a page, which is what the htmx chunk links need.</summary>
        public string ToFilterQueryString() => FilterParts();

        private string FilterParts()
        {
            var parts = new List<string>();

            if (Status.HasValue) parts.Add($"status={Status}");
            if (TechnicianId.HasValue) parts.Add($"technician_id={TechnicianId}");
            if (CustomerLike != null) parts.Add($"customer_like={Uri.EscapeDataString(CustomerLike)}");
            if (FromDate.HasValue) parts.Add($"from_date={FromDate.Value.ToString("yyyy-MM-dd")}");
            if (MinAmount.HasValue) parts.Add($"min_amount={MinAmount}");

            return string.Join("&", parts);
        }
    }
}
