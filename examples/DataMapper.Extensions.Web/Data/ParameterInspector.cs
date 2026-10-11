using System.Data;
using System.Data.Common;
using DataMapper.Extensions.Web.Models;

namespace DataMapper.Extensions.Web.Data
{
    /// <summary>
    /// Reads back what AddParameters and AddParameter put on a command, so the pages can show the
    /// parameter names, the values, and the DbType the builder chose. Nothing here executes
    /// anything: the library only builds parameters, and this reads the collection it filled.
    /// </summary>
    public static class ParameterInspector
    {
        public static List<BoundParameter> Inspect(DbCommand command, string source)
        {
            var parameters = new List<BoundParameter>();

            foreach (DbParameter parameter in command.Parameters)
                parameters.Add(new BoundParameter(source, parameter.ParameterName, parameter.Value, parameter.DbType));

            return parameters;
        }
    }
}
