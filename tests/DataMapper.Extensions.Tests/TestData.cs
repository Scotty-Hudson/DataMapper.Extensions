using System;
using System.Data;
using System.IO;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// TestData.xml, read once per test class.
    ///
    /// This is a class fixture rather than a static: DataTable is not thread safe, and xUnit
    /// runs test classes in parallel by default, so a shared DataSet would let two classes
    /// race on the same table. Each class gets its own copy.
    /// </summary>
    public sealed class TestData
    {
        private readonly DataSet _dataSet;

        public TestData()
        {
            _dataSet = new DataSet();
            _dataSet.ReadXml(Path.Combine(AppContext.BaseDirectory, "TestData.xml"));
        }

        public DataTable Customer => _dataSet.Tables["Customer"];
        public DataTable Account => _dataSet.Tables["Account"];
        public DataTable SnakeCaseRecord => _dataSet.Tables["SnakeCaseRecord"];
        public DataTable FieldOnlyRecord => _dataSet.Tables["FieldOnlyRecord"];
        public DataTable BadData => _dataSet.Tables["BadData"];

        public DataRow Row(DataTable table, long id) => table.Select($"Id = {id}")[0];
    }
}
