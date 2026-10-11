using System.Data;
using System.Globalization;
using System.Text;

namespace DataMapper.Extensions.Web.Data
{
    /// <summary>
    /// Writes a DataTable to CSV. The table comes from ToDataTable, so the header is the member
    /// names or the Column names, and IgnoreMember members are already gone.
    /// </summary>
    public static class CsvBuilder
    {
        public static string Build(DataTable table)
        {
            var columns = table.Columns.Cast<DataColumn>().ToList();
            var builder = new StringBuilder();

            builder.AppendLine(string.Join(",", columns.Select(c => Escape(c.ColumnName))));

            foreach (DataRow row in table.Rows)
                builder.AppendLine(string.Join(",", columns.Select(c => Escape(Format(row[c])))));

            return builder.ToString();
        }

        private static string Format(object value)
        {
            if (value is DBNull) return string.Empty;

            // Invariant culture, the same choice the mapper makes by default.
            return value switch
            {
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                decimal amount => amount.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
    }
}
