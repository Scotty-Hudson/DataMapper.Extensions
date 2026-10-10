using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace DataMapper.Extensions
{
    /// <summary>
    /// Builds DbParameter objects from an object or a dictionary so the caller does not have to
    /// hand-code parameter binding.
    ///
    /// This is the parameter half of a query layer only: nothing here executes anything. The
    /// command text, the connection lifetime and transactions remain the caller's job.
    ///
    /// Two members with the same name, or the same ColumnAttribute name, produce two parameters
    /// with the same name. Providers reject duplicate parameter names, so that is a model error
    /// this library does not try to repair.
    /// </summary>
    public static class ParameterExtensions
    {
        // Types a provider can be told about explicitly. Anything not in this map is left at
        // DbType.Object, which lets the provider infer the type from the value.
        //
        // DateTime and TimeSpan are deliberately absent, matching Dapper's own type map: for
        // those types setting DbType is not useful, and providers infer them correctly.
        private static readonly Dictionary<Type, DbType> TypeMap = new()
        {
            [typeof(byte)] = DbType.Byte,
            [typeof(sbyte)] = DbType.SByte,
            [typeof(short)] = DbType.Int16,
            [typeof(ushort)] = DbType.UInt16,
            [typeof(int)] = DbType.Int32,
            [typeof(uint)] = DbType.UInt32,
            [typeof(long)] = DbType.Int64,
            [typeof(ulong)] = DbType.UInt64,
            [typeof(bool)] = DbType.Boolean,
            [typeof(byte[])] = DbType.Binary,
            [typeof(char)] = DbType.StringFixedLength,
            [typeof(decimal)] = DbType.Decimal,
            [typeof(double)] = DbType.Double,
            [typeof(float)] = DbType.Single,
            [typeof(Guid)] = DbType.Guid,
            [typeof(string)] = DbType.String,
            [typeof(object)] = DbType.Object
        };

        /// <summary>
        /// Adds one parameter to the command. A null value becomes DBNull.Value.
        /// </summary>
        /// <param name="command">The command to add the parameter to.</param>
        /// <param name="value">Value to bind.</param>
        /// <param name="name">Parameter name. When omitted the value is added without a name.</param>
        /// <param name="options">Options. Only <see cref="MapOptions.Converter"/> is used here.</param>
        /// <remarks>
        /// A null argument carries no type, so DbType is left at Object. When the declared type is
        /// known, use the object overload instead: it sets DbType from the member type even when
        /// the value is null, which is what providers need for a null parameter.
        /// </remarks>
        public static DbParameter AddParameter(this DbCommand command, object value, string name = null, MapOptions options = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            return AddParameter(command, name, value, value?.GetType() ?? typeof(object), options);
        }

        /// <summary>
        /// Adds a parameter for every member of <paramref name="parameters"/>, or for every entry
        /// of a dictionary. Names come from a ColumnAttribute or from the member name, which is
        /// the same rule the mapper uses.
        /// </summary>
        /// <param name="command">The command to add the parameters to.</param>
        /// <param name="parameters">An object, an anonymous type, or a dictionary of name to value.</param>
        /// <param name="options">Options. Only <see cref="MapOptions.Converter"/> is used here.</param>
        /// <returns>The number of parameters added.</returns>
        public static int AddParameters(this DbCommand command, object parameters, MapOptions options = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (parameters == null) return 0;

            options ??= MapOptions.Default;

            // A dictionary is a set of name and value pairs, not a POCO. Checking the non-generic
            // IDictionary as well matters: Dictionary&lt;string, int&gt; implements it but does not
            // implement IDictionary&lt;string, object&gt;, and its properties would otherwise be
            // bound as parameters instead of its entries.
            if (parameters is IDictionary<string, object> generic)
            {
                foreach (var pair in generic)
                    AddParameter(command, pair.Key, pair.Value, pair.Value?.GetType() ?? typeof(object), options);

                return generic.Count;
            }

            if (parameters is IDictionary any)
            {
                foreach (DictionaryEntry entry in any)
                    AddParameter(command, entry.Key?.ToString(), entry.Value, entry.Value?.GetType() ?? typeof(object), options);

                return any.Count;
            }

            var accessor = MemberAccessor.Create(parameters.GetType());
            var count = 0;

            foreach (var member in accessor.GetMembers())
            {
                // Parameters are read out of the object, so read-only members count. This is the
                // one place where the mapper's "skip members that cannot be written" rule does not
                // apply: an anonymous type, which is what people pass here, is entirely read-only.
                AddParameter(command, member.SourceName, member.Get(parameters), member.Type, options);
                count++;
            }

            return count;
        }

        private static DbParameter AddParameter(DbCommand command, string name, object value, Type declaredType, MapOptions options)
        {
            var parameter = command.CreateParameter();

            // A dictionary key that is not a string, or a null key, can arrive here as null.
            parameter.ParameterName = name ?? string.Empty;

            var resolved = ResolveValue(value, declaredType, options);
            parameter.Value = resolved;

            // DbType has to describe the value that is actually bound. When a converter produced a
            // different type, the declared type is no longer the right answer. When there is no
            // value to infer from, the declared type is the only thing a provider can use.
            var typeForDbType = resolved == DBNull.Value ? declaredType : resolved.GetType();

            if (TryGetDbType(typeForDbType, out var dbType))
                parameter.DbType = dbType;

            command.Parameters.Add(parameter);
            return parameter;
        }

        private static object ResolveValue(object value, Type declaredType, MapOptions options)
        {
            if (options?.Converter != null)
            {
                var converted = options.Converter(value, declaredType);
                if (converted != null) return converted;
            }

            // A parameter has no POCO to apply a null policy to, so null is always DBNull.Value.
            if (value == null) return DBNull.Value;

            var underlying = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

            // Box an enum as its underlying type. A provider can store the number, not the enum,
            // and this is the same choice Dapper makes.
            if (underlying.IsEnum)
                return Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), options?.Culture ?? CultureInfo.InvariantCulture);

            return value;
        }

        private static bool TryGetDbType(Type type, out DbType dbType)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsEnum) underlying = Enum.GetUnderlyingType(underlying);
            return TypeMap.TryGetValue(underlying, out dbType);
        }
    }
}
