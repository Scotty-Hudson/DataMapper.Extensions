using System;
using System.Collections;
using System.Data;
using System.Linq;

namespace DataMapper.Extensions.Tests
{
    /// <summary>
    /// A minimal IDataReader that can hold duplicate field names, which a DataTable cannot.
    /// Only the members the mapper actually uses are implemented.
    /// </summary>
    public class FakeReader : IDataReader
    {
        private readonly (string Name, object Value)[] _fields;
        private int _position = -1;

        public FakeReader(params (string, object)[] fields) => _fields = fields;

        public int FieldCount => _fields.Length;
        public string GetName(int i) => _fields[i].Name;
        public int GetOrdinal(string name) => Array.FindIndex(_fields, f => f.Name == name);
        public object GetValue(int i) => _fields[i].Value;
        public bool IsDBNull(int i) => _fields[i].Value == null || _fields[i].Value == DBNull.Value;

        public bool Read()
        {
            if (_position >= _fields.Length - 1) return false;
            _position++;
            return true;
        }

        public bool NextResult() => false;
        public void Close() { }
        public void Dispose() { }
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public DataTable GetSchemaTable() => null;

        public object this[int i] => _fields[i].Value;
        public object this[string name] => _fields[GetOrdinal(name)].Value;

        public IEnumerator GetEnumerator() => Enumerable.Empty<object>().GetEnumerator();

        // Not used by the mapper.
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => throw new NotSupportedException();
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public int GetInt32(int i) => throw new NotSupportedException();
        public long GetInt64(int i) => throw new NotSupportedException();
        public string GetString(int i) => throw new NotSupportedException();
        public int GetValues(object[] values) => throw new NotSupportedException();
    }
}
