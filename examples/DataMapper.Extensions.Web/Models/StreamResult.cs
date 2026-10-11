namespace DataMapper.Extensions.Web.Models
{
    /// <summary>One record as the streaming endpoint yielded it, timed from the start of the chunk.</summary>
    public sealed record StreamRow(int Position, Job Job, long ElapsedMs);

    /// <summary>One chunk of a streamed result set, as the htmx endpoint produced it.</summary>
    public sealed record StreamResult(
        IReadOnlyList<StreamRow> Rows,
        int Next,
        bool More,
        int ChunkSize,
        long ElapsedMs,
        string Outcome);
}
