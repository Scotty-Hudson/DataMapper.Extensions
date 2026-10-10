using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMapper.Extensions
{
    /// <summary>
    /// Compiled, cached member access. The delegates for a type are generated once and
    /// reused, which is what keeps these extensions faster than plain reflection.
    ///
    /// This replaces the old FastMember dependency: that package only ships a .NET
    /// Framework build and throws MissingMethodException on modern .NET because
    /// AppDomain.DefineDynamicAssembly no longer exists.
    /// </summary>
    internal sealed class MemberAccessor
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        private static readonly ConcurrentDictionary<Type, MemberAccessor> Cache = new();

        public static MemberAccessor Create(Type type) => Cache.GetOrAdd(type, t => new MemberAccessor(t));

        private readonly List<MemberHandle> _members;

        private MemberAccessor(Type type)
        {
            _members = new List<MemberHandle>();
            var nullability = new NullabilityInfoContext();

            foreach (var property in type.GetProperties(Flags))
            {
                // Indexers (this[int]) are not members a mapper can name, so they are skipped.
                if (property.GetIndexParameters().Length > 0) continue;

                var getter = property.GetGetMethod();
                var setter = property.GetSetMethod();
                if (getter == null && setter == null) continue;

                AddMember(property, property.PropertyType,
                    getter != null ? BuildGetter(type, property) : null,
                    setter != null ? BuildSetter(type, property) : null,
                    IsNullableReference(nullability, property));
            }

            foreach (var field in type.GetFields(Flags))
                AddMember(field, field.FieldType, BuildGetter(type, field), BuildSetter(type, field),
                    IsNullableReference(nullability, field));
        }

        private void AddMember(MemberInfo member, Type memberType, Func<object, object> get, Action<object, object> set, bool isNullableReference)
        {
            if (member.GetCustomAttribute<IgnoreMemberAttribute>() != null) return;

            _members.Add(new MemberHandle(
                member.Name,
                member.GetCustomAttribute<ColumnAttribute>()?.Name,
                member.GetCustomAttribute<NullAsAttribute>()?.Handling,
                memberType,
                isNullableReference,
                get,
                set));
        }

        // The compiler's own annotation for the member. Anonymous (a project with nullable
        // disabled) is treated as non-nullable, which is what the library has always assumed.
        private static bool IsNullableReference(NullabilityInfoContext nullability, MemberInfo member)
        {
            var info = member is PropertyInfo property
                ? nullability.Create(property)
                : nullability.Create((FieldInfo)member);

            return info.ReadState == NullabilityState.Nullable;
        }

        public IReadOnlyList<MemberHandle> GetMembers() => _members;

        private static Func<object, object> BuildGetter(Type type, MemberInfo member)
        {
            var target = Expression.Parameter(typeof(object), "target");
            var typed = Expression.Convert(target, type);
            Expression body = member is PropertyInfo property
                ? Expression.Property(typed, property)
                : Expression.Field(typed, (FieldInfo)member);

            return Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), target).Compile();
        }

        private static Action<object, object> BuildSetter(Type type, MemberInfo member)
        {
            var target = Expression.Parameter(typeof(object), "target");
            var value = Expression.Parameter(typeof(object), "value");
            var typed = Expression.Variable(type, "typed");

            Expression assignment = member is PropertyInfo property
                ? Expression.Assign(Expression.Property(typed, property), Expression.Convert(value, property.PropertyType))
                : Expression.Assign(Expression.Field(typed, (FieldInfo)member), Expression.Convert(value, ((FieldInfo)member).FieldType));

            var body = Expression.Block(new[] { typed },
                Expression.Assign(typed, Expression.Convert(target, type)),
                assignment);

            return Expression.Lambda<Action<object, object>>(body, target, value).Compile();
        }
    }

    internal sealed class MemberHandle
    {
        private readonly Func<object, object> _get;
        private readonly Action<object, object> _set;

        public MemberHandle(string name, string columnName, NullHandling? handling, Type type,
            bool isNullableReference, Func<object, object> get, Action<object, object> set)
        {
            Name = name;
            ColumnName = columnName;
            Handling = handling;
            Type = type;
            IsNullableReference = isNullableReference;
            _get = get;
            _set = set;
        }

        public string Name { get; }

        /// <summary>Column name from a ColumnAttribute, or null when the member name is used.</summary>
        public string ColumnName { get; }

        /// <summary>Null handling from a NullHandlingAttribute, or null when the option applies.</summary>
        public NullHandling? Handling { get; }

        /// <summary>True when the compiler annotates the member as nullable.</summary>
        public bool IsNullableReference { get; }

        /// <summary>The name used to look the value up in the data source.</summary>
        public string SourceName => ColumnName ?? Name;

        public Type Type { get; }
        public bool CanRead => _get != null;
        public bool CanWrite => _set != null;

        public object Get(object target) => _get(target);
        public void Set(object target, object value) => _set(target, value);
    }

    // A member paired with the column it reads from. Column is null when the data source
    // has no column for the member, which is still needed for the null-string rule.
    internal readonly struct ColumnEntry
    {
        public ColumnEntry(MemberHandle member, DataColumn column)
        {
            Member = member;
            Column = column;
        }

        public MemberHandle Member { get; }
        public DataColumn Column { get; }
    }

    // A member paired with the reader ordinal it reads from. Ordinal is -1 when the reader
    // has no field for the member.
    internal readonly struct FieldEntry
    {
        public FieldEntry(MemberHandle member, int ordinal)
        {
            Member = member;
            Ordinal = ordinal;
        }

        public MemberHandle Member { get; }
        public int Ordinal { get; }
    }

    // Fast object creation: a compiled lambda for types with a default constructor,
    // otherwise an uninitialized object. Taken from StackOverflow answer 21, titled
    // "Fast creation of objects instead of Activator.CreateInstance(type)".
    internal static class New<T>
    {
        internal static readonly Func<T> Instance = Creator();

        private static Func<T> Creator()
        {
            var t = typeof(T);
            if (t == typeof(string))
                return () => (T)(object)string.Empty;

            if (t.HasDefaultConstructor())
                return Expression.Lambda<Func<T>>(Expression.New(t)).Compile();

            return () => (T)RuntimeHelpers.GetUninitializedObject(t);
        }
    }

    /// <summary>
    /// Extension methods for mapping ADO.NET and DataTable data onto POCOs.
    /// </summary>
    public static class DataMapperExtensions
    {
        internal static bool HasDefaultConstructor(this Type t)
            => t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null;

        private static T ClonePoco<T>(T defaultPoco, List<MemberHandle> membersToCopy) where T : class
        {
            // Copy the values of the POCO passed in to a new POCO.
            var clonedPoco = New<T>.Instance();
            foreach (var member in membersToCopy)
                member.Set(clonedPoco, member.Get(defaultPoco));
            return clonedPoco;
        }

        // Only members that differ from a brand new instance need copying, so mapping a
        // plain "new POCO()" default costs nothing per row.
        private static List<MemberHandle> MembersToCopy<T>(MemberAccessor accessor, T defaultPoco) where T : class
        {
            var fresh = New<T>.Instance();
            var membersToCopy = new List<MemberHandle>();

            foreach (var member in accessor.GetMembers())
                if (member.CanRead && member.CanWrite && !Equals(member.Get(defaultPoco), member.Get(fresh)))
                    membersToCopy.Add(member);

            return membersToCopy;
        }

        /// <summary>
        /// Converts a value to <paramref name="targetType"/>. Handles nullable types,
        /// enums, Guid and TimeSpan, which Convert.ChangeType alone cannot.
        /// </summary>
        private static object ConvertValue(object value, Type targetType, MapOptions options)
        {
            if (options.Converter != null)
            {
                var converted = options.Converter(value, targetType);
                if (converted != null) return converted;
            }

            // DBNull becomes null for a nullable target type. For a non-nullable target this
            // falls through to Convert.ChangeType, which is what produced the original
            // behaviour (DBNull to string gives an empty string).
            //
            // Note that ConvertTo does not apply NullHandling: that is a mapping policy, and
            // ConvertTo has no POCO to apply it to. Use the defaultValue overload for a fallback.
            if (value is DBNull && Nullable.GetUnderlyingType(targetType) != null) return null;

            if (targetType.IsInstanceOfType(value)) return value;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType.IsInstanceOfType(value)) return value;

            if (underlyingType.IsEnum)
                return value is string enumName
                    ? Enum.Parse(underlyingType, enumName, true)
                    : Enum.ToObject(underlyingType, value);

            if (value is string text)
            {
                if (underlyingType == typeof(Guid)) return Guid.Parse(text);
                if (underlyingType == typeof(TimeSpan)) return TimeSpan.Parse(text, options.Culture);
            }

            return Convert.ChangeType(value, underlyingType, options.Culture);
        }

        private static string Normalize(string name)
            => name.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

        // What to do when the source has no value for a member.
        private static void ApplyNullPolicy(MemberHandle member, object poco, MapOptions options)
        {
            if (!member.CanWrite) return;

            var handling = member.Handling ?? options.NullHandling;

            switch (handling)
            {
                case NullHandling.Keep:
                    return;

                case NullHandling.Null:
                    // Only meaningful for reference types and nullable value types.
                    if (!member.Type.IsValueType || Nullable.GetUnderlyingType(member.Type) != null)
                        member.Set(poco, null);
                    return;

                case NullHandling.EmptyString:
                    // A non-nullable string is guaranteed non-null. Other reference types keep the
                    // template value, because there is no empty instance of an arbitrary type.
                    if (member.Type == typeof(string) && !member.IsNullableReference && member.Get(poco) == null)
                        member.Set(poco, string.Empty);
                    return;
            }
        }

        // The member to column map is built once per table and reused for every row, so the
        // row loop does no name lookups at all.
        private static List<ColumnEntry> BuildColumnMap(MemberAccessor accessor, DataTable table, MapOptions options)
        {
            var map = new List<ColumnEntry>(accessor.GetMembers().Count);

            foreach (var member in accessor.GetMembers())
            {
                if (!member.CanWrite) continue;

                var name = member.SourceName;
                var column = table.Columns.Contains(name) ? table.Columns[name] : null;

                if (column == null && options.MatchNormalizedNames)
                {
                    var target = Normalize(name);
                    foreach (DataColumn candidate in table.Columns)
                        if (Normalize(candidate.ColumnName) == target) { column = candidate; break; }
                }

                map.Add(new ColumnEntry(member, column));
            }

            return map;
        }

        // List rather than IReadOnlyList: foreach over IReadOnlyList goes through IEnumerable and
        // allocates a boxed enumerator for every row.
        private static T MapRow<T>(DataRow row, T poco, List<ColumnEntry> map, MapOptions options) where T : class
        {
            for (var i = 0; i < map.Count; i++)
            {
                var entry = map[i];

                if (entry.Column == null)
                {
                    // No column for this member: keep the POCO value, but still apply the
                    // null-string rule.
                    ApplyNullPolicy(entry.Member, poco, options);
                    continue;
                }

                var value = row[entry.Column];
                if (value is DBNull)
                {
                    ApplyNullPolicy(entry.Member, poco, options);
                    continue;
                }

                entry.Member.Set(poco, ConvertValue(value, entry.Member.Type, options));
            }

            return poco;
        }

        private static List<FieldEntry> BuildFieldMap(MemberAccessor accessor, IDataReader reader, MapOptions options)
        {
            // Cache the field names in the reader for use in our while loop for efficiency.
            var readerFieldLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> normalizedLookup = options.MatchNormalizedNames
                ? new Dictionary<string, int>()
                : null;

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                readerFieldLookup[name] = i;
                if (normalizedLookup != null) normalizedLookup[Normalize(name)] = i;
            }

            var map = new List<FieldEntry>(accessor.GetMembers().Count);
            foreach (var member in accessor.GetMembers())
            {
                if (!member.CanWrite) continue;

                var name = member.SourceName;
                var ordinal = -1;
                if (readerFieldLookup.TryGetValue(name, out var found)) ordinal = found;
                else if (normalizedLookup != null && normalizedLookup.TryGetValue(Normalize(name), out found)) ordinal = found;

                map.Add(new FieldEntry(member, ordinal));
            }

            return map;
        }

        private static T MapRecord<T>(IDataRecord record, T poco, List<FieldEntry> map, MapOptions options) where T : class
        {
            for (var i = 0; i < map.Count; i++)
            {
                var entry = map[i];

                if (entry.Ordinal < 0)
                {
                    ApplyNullPolicy(entry.Member, poco, options);
                    continue;
                }

                if (record.IsDBNull(entry.Ordinal))
                {
                    ApplyNullPolicy(entry.Member, poco, options);
                    continue;
                }

                entry.Member.Set(poco, ConvertValue(record.GetValue(entry.Ordinal), entry.Member.Type, options));
            }

            return poco;
        }

        /// <summary>
        /// Builds a DataTable from a sequence of POCOs. Column names come from the member
        /// names or from a ColumnAttribute, and nullable members produce columns of the
        /// underlying type. Useful for SqlBulkCopy and for building test data.
        /// </summary>
        public static DataTable ToDataTable<T>(this IEnumerable<T> source, string tableName = "Table") where T : class
        {
            var accessor = MemberAccessor.Create(typeof(T));
            var members = accessor.GetMembers().Where(m => m.CanRead).ToList();

            var table = new DataTable(tableName);
            foreach (var member in members)
                table.Columns.Add(member.SourceName, Nullable.GetUnderlyingType(member.Type) ?? member.Type);

            foreach (var item in source)
            {
                var row = table.NewRow();
                foreach (var member in members)
                    row[member.SourceName] = member.Get(item) ?? DBNull.Value;
                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Builds a DbDataReader from a sequence of POCOs, for example to feed SqlBulkCopy.
        /// The whole sequence is materialised into a DataTable first.
        /// </summary>
        public static DbDataReader ToDataReader<T>(this IEnumerable<T> source, string tableName = "Table") where T : class
            => source.ToDataTable(tableName).CreateDataReader();

        /// <summary>
        /// Converts a column value to <typeparamref name="T"/>.
        /// </summary>
        public static T ConvertTo<T>(this DataRow row, string columnName)
            => (T)ConvertValue(row[columnName], typeof(T), MapOptions.Default);

        /// <summary>
        /// Converts a column value to <typeparamref name="T"/>, returning
        /// <paramref name="defaultValue"/> when the column is DBNull.
        /// </summary>
        public static T ConvertTo<T>(this DataRow row, string columnName, T defaultValue)
        {
            if (row[columnName] is DBNull) return defaultValue;
            return row.ConvertTo<T>(columnName);
        }

        /// <summary>
        /// Converts a column value to <typeparamref name="T"/> using the given options.
        /// The options come first so this cannot be confused with the
        /// ConvertTo&lt;T&gt;(row, columnName, defaultValue) overload.
        /// </summary>
        public static T ConvertTo<T>(this DataRow row, MapOptions options, string columnName)
            => (T)ConvertValue(row[columnName], typeof(T), options);

        /// <summary>
        /// Maps a DataRow to a new POCO. Column names are matched to property names
        /// case insensitively; columns that are not properties of the POCO are ignored.
        /// </summary>
        public static T MapTo<T>(this DataRow row, bool nullStringsToEmpty = true)
            where T : class => row.MapTo(New<T>.Instance(), nullStringsToEmpty);

        /// <summary>
        /// Maps a DataRow onto <paramref name="defaultPoco"/>. Only columns with a non-null
        /// value are written, so values already set on <paramref name="defaultPoco"/> are kept
        /// for missing or DBNull columns.
        /// </summary>
        public static T MapTo<T>(this DataRow row, T defaultPoco, bool nullStringsToEmpty = true) where T : class
            => row.MapTo(defaultPoco, MapOptions.Default.WithNullStringsToEmpty(nullStringsToEmpty));

        /// <summary>
        /// Maps a DataRow to a new POCO using the given options.
        /// </summary>
        public static T MapTo<T>(this DataRow row, MapOptions options) where T : class
            => row.MapTo(New<T>.Instance(), options);

        /// <summary>
        /// Maps a DataRow onto <paramref name="defaultPoco"/> using the given options.
        /// </summary>
        public static T MapTo<T>(this DataRow row, T defaultPoco, MapOptions options) where T : class
            => MapRow(row, defaultPoco, BuildColumnMap(MemberAccessor.Create(typeof(T)), row.Table, options), options);

        /// <summary>
        /// Maps every row of a DataTable to a new POCO and returns the list.
        /// </summary>
        public static List<T> MapTo<T>(this DataTable table, bool nullStringsToEmpty = true)
            where T : class => table.MapTo(New<T>.Instance(), nullStringsToEmpty);

        /// <summary>
        /// Maps every row of a DataTable to a clone of <paramref name="defaultPoco"/>.
        /// </summary>
        public static List<T> MapTo<T>(this DataTable table, T defaultPoco, bool nullStringsToEmpty = true) where T : class
            => table.MapTo(defaultPoco, MapOptions.Default.WithNullStringsToEmpty(nullStringsToEmpty));

        /// <summary>
        /// Maps every row of a DataTable to a new POCO using the given options.
        /// </summary>
        public static List<T> MapTo<T>(this DataTable table, MapOptions options) where T : class
            => table.MapTo(New<T>.Instance(), options);

        /// <summary>
        /// Maps every row of a DataTable to a clone of <paramref name="defaultPoco"/> using the given options.
        /// </summary>
        public static List<T> MapTo<T>(this DataTable table, T defaultPoco, MapOptions options) where T : class
            => table.EnumerateTo(defaultPoco, options).ToList();

        /// <summary>
        /// Maps each row of a DataTable to a new POCO, one row at a time.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this DataTable table) where T : class
            => table.EnumerateTo(New<T>.Instance(), MapOptions.Default);

        /// <summary>
        /// Maps each row of a DataTable to a new POCO using the given options, one row at a time.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this DataTable table, MapOptions options) where T : class
            => table.EnumerateTo(New<T>.Instance(), options);

        /// <summary>
        /// Maps each row of a DataTable to a clone of <paramref name="defaultPoco"/>, one row at a time.
        /// Nothing is buffered, so this can be used for large tables.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this DataTable table, T defaultPoco, MapOptions options) where T : class
        {
            var accessor = MemberAccessor.Create(typeof(T));
            var map = BuildColumnMap(accessor, table, options);
            var membersToCopy = MembersToCopy(accessor, defaultPoco);

            foreach (DataRow row in table.Rows)
                yield return MapRow(row, ClonePoco(defaultPoco, membersToCopy), map, options);
        }

        /// <summary>
        /// Maps every record returned by the reader to a new POCO and returns the list.
        /// Fields that are not properties of the POCO are ignored.
        /// </summary>
        public static List<T> MapTo<T>(this IDataReader reader, bool nullStringsToEmpty = true)
            where T : class => reader.MapTo(New<T>.Instance(), nullStringsToEmpty);

        /// <summary>
        /// Maps every record returned by the reader to a clone of <paramref name="defaultPoco"/>.
        /// </summary>
        public static List<T> MapTo<T>(this IDataReader reader, T defaultPoco, bool nullStringsToEmpty = true) where T : class
            => reader.MapTo(defaultPoco, MapOptions.Default.WithNullStringsToEmpty(nullStringsToEmpty));

        /// <summary>
        /// Maps every record returned by the reader to a new POCO using the given options.
        /// </summary>
        public static List<T> MapTo<T>(this IDataReader reader, MapOptions options) where T : class
            => reader.MapTo(New<T>.Instance(), options);

        /// <summary>
        /// Maps every record returned by the reader to a clone of <paramref name="defaultPoco"/> using the given options.
        /// </summary>
        public static List<T> MapTo<T>(this IDataReader reader, T defaultPoco, MapOptions options) where T : class
            => reader.EnumerateTo(defaultPoco, options).ToList();

        /// <summary>
        /// Maps each record returned by the reader to a new POCO, one record at a time.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this IDataReader reader) where T : class
            => reader.EnumerateTo(New<T>.Instance(), MapOptions.Default);

        /// <summary>
        /// Maps each record returned by the reader to a new POCO using the given options,
        /// one record at a time.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this IDataReader reader, MapOptions options) where T : class
            => reader.EnumerateTo(New<T>.Instance(), options);

        /// <summary>
        /// Maps each record returned by the reader to a clone of <paramref name="defaultPoco"/>, one record at a
        /// time. Nothing is buffered, so this works for result sets that do not fit in memory.
        /// </summary>
        public static IEnumerable<T> EnumerateTo<T>(this IDataReader reader, T defaultPoco, MapOptions options) where T : class
        {
            var accessor = MemberAccessor.Create(typeof(T));
            var map = BuildFieldMap(accessor, reader, options);
            var membersToCopy = MembersToCopy(accessor, defaultPoco);

            while (reader.Read())
                yield return MapRecord(reader, ClonePoco(defaultPoco, membersToCopy), map, options);
        }

        /// <summary>
        /// Maps each record returned by the reader to a new POCO using the given options,
        /// one record at a time, asynchronously.
        /// </summary>
        public static IAsyncEnumerable<T> EnumerateToAsync<T>(this DbDataReader reader, MapOptions options,
            CancellationToken cancellationToken = default) where T : class
            => reader.EnumerateToAsync(New<T>.Instance(), options, cancellationToken);

        /// <summary>
        /// Maps each record returned by the reader to a clone of <paramref name="defaultPoco"/>, one record at a
        /// time, asynchronously.
        /// </summary>
        public static async IAsyncEnumerable<T> EnumerateToAsync<T>(this DbDataReader reader, T defaultPoco, MapOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
        {
            var accessor = MemberAccessor.Create(typeof(T));
            var map = BuildFieldMap(accessor, reader, options);
            var membersToCopy = MembersToCopy(accessor, defaultPoco);

            while (true)
            {
                // Some DbDataReader implementations ignore the token, so check it here as well
                // to make cancellation reliable between records.
                cancellationToken.ThrowIfCancellationRequested();

                if (!await reader.ReadAsync(cancellationToken)) break;

                yield return MapRecord(reader, ClonePoco(defaultPoco, membersToCopy), map, options);
            }
        }

        /// <summary>
        /// Maps every record returned by the reader to a new POCO using the given options, asynchronously.
        /// </summary>
        public static async Task<List<T>> MapToAsync<T>(this DbDataReader reader, MapOptions options,
            CancellationToken cancellationToken = default) where T : class
            => await reader.MapToAsync(New<T>.Instance(), options, cancellationToken);

        /// <summary>
        /// Maps every record returned by the reader to a clone of <paramref name="defaultPoco"/>, asynchronously.
        /// </summary>
        public static async Task<List<T>> MapToAsync<T>(this DbDataReader reader, T defaultPoco, MapOptions options,
            CancellationToken cancellationToken = default) where T : class
        {
            var pocoList = new List<T>();
            await foreach (var poco in reader.EnumerateToAsync(defaultPoco, options, cancellationToken))
                pocoList.Add(poco);
            return pocoList;
        }

        // Kept for the original bool overloads: true meant "null strings to empty",
        // false meant "leave the template value".
        private static MapOptions WithNullStringsToEmpty(this MapOptions options, bool value)
            => new MapOptions
            {
                Culture = options.Culture,
                NullHandling = value ? NullHandling.EmptyString : NullHandling.Keep,
                MatchNormalizedNames = options.MatchNormalizedNames,
                Converter = options.Converter
            };
    }
}
