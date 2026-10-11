using System.Data;

namespace DataMapper.Extensions.Web.Models
{
    /// <summary>One parameter on a DbCommand, as the parameter builder produced it.</summary>
    public sealed record BoundParameter(string Source, string Name, object? Value, DbType DbType);
}
