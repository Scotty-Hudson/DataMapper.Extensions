namespace DataMapper.Extensions.Web.Models
{
    /// <summary>
    /// A page of results plus the SQL and the parameters that produced it. The page shows both,
    /// because the point of the library is that the SQL is the caller's job.
    /// </summary>
    public sealed record QueryResult(
        string Sql,
        IReadOnlyList<BoundParameter> Parameters,
        IReadOnlyList<Job> Jobs,
        int Page,
        int PageSize,
        long Total,
        long ElapsedMs);
}
