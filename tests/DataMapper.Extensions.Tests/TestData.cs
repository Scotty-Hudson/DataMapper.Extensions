using System;
using System.Data;
using System.IO;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// TestData.xml is read once and exposes one table per scenario.
    /// </summary>
    public static class TestData
    {
        private static readonly DataSet DataSet = Load();

        public static DataTable Customer => DataSet.Tables["Customer"];
        public static DataTable Account => DataSet.Tables["Account"];
        public static DataTable SnakeCaseRecord => DataSet.Tables["SnakeCaseRecord"];
        public static DataTable FieldOnlyRecord => DataSet.Tables["FieldOnlyRecord"];
        public static DataTable BadData => DataSet.Tables["BadData"];

        private static DataSet Load()
        {
            var dataSet = new DataSet();
            dataSet.ReadXml(Path.Combine(AppContext.BaseDirectory, "TestData.xml"));
            return dataSet;
        }

        public static DataRow Row(DataTable table, long id) => table.Select($"Id = {id}")[0];
    }
}
