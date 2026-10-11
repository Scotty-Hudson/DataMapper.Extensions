namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// Deliberately has no attributes at all. The columns are snake_case, so these members only
    /// match when MapOptions.MatchNormalizedNames is on. With it off, every name here keeps the
    /// value the POCO started with, which is what the Options page shows.
    /// </summary>
    public class Technician
    {
        public long TechnicianId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
