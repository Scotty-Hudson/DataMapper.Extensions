# DataMapper.Extensions

Extension methods that map `IDataReader`, `DataTable` and `DataRow` rows onto POCOs and records,
map POCOs and records back into a `DataTable` or `DbDataReader`, and bind parameters from an object
onto a `DbCommand`.

Modern .NET only: `net8.0`, `net9.0` and `net10.0`. No package dependencies.

This is the successor to [DotNetDataExtensions](https://github.com/Scotty-Hudson/DotNetDataExtensions),
which is kept as it was. That version targeted .NET Framework 4.0 and depended on FastMember,
which no longer runs on modern .NET.

## Mapping

Column names are matched to property or public field names case insensitively. Members that are
not in the data source, and columns that are `DBNull`, keep whatever value the POCO already has.

```c#
var customers = reader.MapTo<Customer>();
var customer = row.MapTo<Customer>();
var customers = table.MapTo<Customer>();
```

Pass a POCO to declare defaults for columns that come back as `DBNull`:

```c#
var customers = reader.MapTo(new Customer { Zip = -1 });
```

Instances come from a compiled constructor lambda. A type without a parameterless constructor is
created uninitialized, so its field initializers do not run. Positional records are in that group.

## Column names and attributes

```c#
public class Account
{
    [Column("account_id")]
    public long Id { get; set; }

    [Column("display_name")]
    public string DisplayName { get; set; }

    [DataMapper.Extensions.NullAs(NullHandling.Null)]
    public string Notes { get; set; }

    [IgnoreMember]
    public string Secret { get; set; }

    // Public fields are mapped as well as properties.
    public string Note;
}
```

`MatchNormalizedNames` also matches ignoring case, underscores and spaces, so a column named
`first_name` maps to `FirstName`.

## Records

Records map the same way classes do, including positional records, `init` accessors, attributes,
the reader path and `ToDataTable`:

```c#
public record Person(long Id, string Name);

var people = reader.MapTo<Person>();
```

Three things to know:

- **Get-only members are skipped.** A member with no setter cannot be written, so an immutable
  record such as `public record Person { public long Id { get; } }` keeps its defaults. Mapping
  through the constructor is not supported. Read-only members are also not in the member map, so
  the null policy does not apply to them and a read-only string is not guaranteed non-null.
- **Field initializers do not run on positional records.** They have no parameterless
  constructor, so the general rule in the Mapping section applies. A record with a parameterless
  constructor runs its initializers as normal.
- **Record structs are not supported**, because the generic methods are constrained to `where T : class`.

## Null handling

Nulls enter the code base at one place, the data source, so that is where they are resolved. The
default policy guarantees that a member the compiler considers non-nullable never ends up null,
which means the rest of the code base does not need `!= null` checks:

```c#
public class Customer
{
    public string FirstName { get; set; } = string.Empty;   // guaranteed non-null
    public string? Notes { get; set; }                      // annotated nullable, stays null

    [NullAs(NullHandling.Keep)]
    public string Kept { get; set; } = "template";          // keeps the template value
}
```

The policy uses the compiler's own nullable annotations (`NullabilityInfoContext`). A project with
nullable disabled is treated as non-nullable throughout.

Three settings: `EmptyString` (default), `Keep` (leave whatever the POCO has), and `Null`
(force null). Only strings can be turned into an empty string; other reference types keep the
value the POCO already has. `Null` is ignored for a plain value type, because there is no null to
assign to one.

The policy applies to members the mapper can write. A member with no setter is not in the member
map at all, so the guarantee does not hold for a read-only string.

## Options

```c#
var options = new MapOptions
{
    Culture = CultureInfo.InvariantCulture,   // default
    NullHandling = NullHandling.EmptyString,  // default
    MatchNormalizedNames = false,            // default
    Converter = (value, targetType) =>       // optional custom conversion
        targetType == typeof(string) ? value.ToString().Trim() : null
};

var account = row.MapTo<Account>(options);
var amount = row.ConvertTo<decimal>(options, "Amount");
```

Values are converted with `Convert.ChangeType`, except for cases it cannot handle and that the
extensions support: nullable target types, enums, `Guid`, `TimeSpan`, `DateOnly` and `TimeOnly` from
strings, `DateTime` to `DateOnly` or `TimeOnly`, and `TimeSpan` to `TimeOnly`. The last two matter
because `DateOnly` and `TimeOnly` are not `IConvertible`, and a provider returns a date column as
`DateTime` and a time column as `TimeSpan`. A value that is already the target type is assigned
without converting. Passing `null` for the options uses the default.

`CultureInfo.InvariantCulture` is the default. The original version used `CurrentCulture`, which
can silently corrupt numeric data: a column value of `"1.5"` becomes `15` under a German culture.

## Streaming and async

`MapTo` materialises the whole result set. `EnumerateTo` yields one object at a time, so a large
result set does not have to be held in memory:

```c#
foreach (var customer in reader.EnumerateTo<Customer>())
    Handle(customer);

var firstTwo = reader.EnumerateTo<Customer>().Take(2).ToList();
```

For `DbDataReader` there is an async version that honours a `CancellationToken`:

```c#
await foreach (var customer in reader.EnumerateToAsync<Customer>(options, cancellationToken))
    Handle(customer);

var customers = await reader.MapToAsync<Customer>(options, cancellationToken);
```

## The other direction

```c#
var table = customers.ToDataTable("Customer");
var reader = customers.ToDataReader();

using (var bulkCopy = new SqlBulkCopy(connection))
{
    bulkCopy.DestinationTableName = "Customer";
    await bulkCopy.WriteToServerAsync(customers.ToDataReader());
}
```

Column names come from the member names or from a `[Column]` attribute, nullable members produce
columns of the underlying type, and members marked with `[IgnoreMember]` are left out.

## Parameters

`AddParameters` fills a `DbCommand` from an object, an anonymous type or a dictionary. Names come
from the member names or from a `[Column]` attribute, which is the same rule the mapper uses, and
`[IgnoreMember]` members are left out.

```c#
using var connection = new SqlConnection(connectionString);
using var command = connection.CreateCommand();
command.CommandText = "select * from accounts where account_id = @account_id and status = @status";
command.AddParameters(new { account_id = id, status = Status.Active });
```

A null value becomes `DBNull.Value`. Enums are boxed as their underlying value, because that is
what a provider can store. `DbType` is set for the types a provider can be told about; `DateTime`
and `TimeSpan` are left at `DbType.Object` so the provider infers them from the value.

Read-only members are included here. This is the one place where the mapper's rule of skipping
members with no setter does not apply, and it matters: an anonymous type is entirely read-only, so
the usual parameter object would produce nothing under the mapping rule.

```c#
command.AddParameters(new Dictionary<string, object> { ["id"] = 42 });  // keys become parameter names
var parameter = command.AddParameter(42, "id");                        // one at a time
```

Only `MapOptions.Converter` is used by the parameter methods. `NullHandling` is a mapping policy,
and a parameter has no POCO to apply it to, so null is always `DBNull.Value`. When the converter
returns a different type, `DbType` is set from the value it produced rather than from the declared
member type, so the two always agree.

Dictionaries are read as name and value pairs, not as a POCO. `Dictionary<string, int>` binds its
entries, not its properties, and a key that is not a string becomes a name through `ToString()`.

Two members that resolve to the same name are a model error the library does not try to repair:
parameter providers reject duplicate parameter names, and `ToDataTable` throws
`DuplicateNameException` because a `DataTable` cannot hold two columns with the same name.

When a reader has two fields with the same name, or two names that normalize to the same value with
`MatchNormalizedNames`, the last one wins. That rule is the same for the `DataTable` and the reader
paths.

This is the parameter half of a query layer only. Nothing here executes a command: the command
text, the connection lifetime and transactions remain the caller's job.

## Example

`examples/DataMapper.Extensions.Example` is a console app that runs against an in-memory SQLite
database. It is the only project in the repo with a package dependency (`Microsoft.Data.Sqlite`);
the library itself still has none.

```bash
dotnet run --project examples/DataMapper.Extensions.Example
```

It inserts 1,000 rows in one transaction, streams them back with `EnumerateTo`, and shows the null
policy and the default POCO across the whole result set rather than on a single row. Output from a
run on the machine this was developed on. The timings vary by machine and by run, so the numbers
below are illustrative:

```
Parameters built from the model:
  account_id = 1 (Int64)
  display_name = "account-1" (String)
  status = 1 (Int32)
  account_key = 00000000-0000-0000-0000-000000000001 (Guid)
  balance = 10 (Int32)
  duration = 00:01:00 (String)
  created_on = 1/1/2026 12:01:00 AM (String)
  is_active = False (Boolean)
Inserted 1000 rows in 35.7 ms, commit included.

Parameters bound for the query:
  status = 0 (Int32)

First five rows, streamed with EnumerateTo:
  3 account-3 Active 00000000-0000-0000-0000-000000000003 30 00:03:00 2026-01-01T00:03:00.0000000 False
  6 account-6 Active 00000000-0000-0000-0000-000000000006 60 00:06:00 2026-01-01T00:06:00.0000000 True
  9 account-9 Active 00000000-0000-0000-0000-000000000009 90 00:09:00 2026-01-01T00:09:00.0000000 False
  12 account-12 Active 00000000-0000-0000-0000-000000000012 120 00:12:00 2026-01-01T00:12:00.0000000 True
  15 account-15 Active 00000000-0000-0000-0000-000000000015 150 00:15:00 2026-01-01T00:15:00.0000000 False
  333 rows mapped in 14.7 ms, balance sum 1431450

Null policy across the whole result set:
  default (EmptyString): 100 empty, 0 null
  NullHandling.Null:     0 empty, 100 null

Default POCO on the rows that come back null:
  70 "unknown" balance 0
  140 "unknown" balance 0
  210 "unknown" balance 0
```

The `String` shown for `duration` and `created_on` is Microsoft.Data.Sqlite inferring the type from
the value, which is how it stores them; the library leaves those at `DbType.Object`. SQLite has no
real column types, so a text column reads back as a string, which is what exercises the `Guid`,
`TimeSpan` and `DateTime` conversions from strings.

## ConvertTo

```c#
var customerId = row.ConvertTo<long>("CustomerId");
var zip = row.ConvertTo("Zip", 0);            // default when the column is DBNull
var amount = row.ConvertTo<decimal>(options, "Amount");
```

`DBNull` becomes `null` for a nullable target type. For a non-nullable target it converts as it
always has, which for a string is an empty string. `NullHandling` is a mapping policy and does not
apply to `ConvertTo`; use the `defaultValue` overload for a fallback.

`object` is not a nullable target type in the reflection sense, so `ConvertTo<object>` on a `DBNull`
column returns `DBNull.Value` rather than `null`.

## Changes from the original

- `CultureInfo.InvariantCulture` is the default culture, because `CurrentCulture` can silently
  corrupt numeric data.
- The `nullStringsToEmpty` flag is replaced by the `NullHandling` policy.
- `ConvertTo<T>` on `DBNull` returns `null` for a nullable target type instead of throwing
  `InvalidCastException`. Non-nullable targets are unchanged.
- Indexers on a POCO are skipped. They used to throw when the accessor built its delegates.
- `ConvertTo<T>(row, columnName, MapOptions)` takes the options first, so it cannot be confused
  with the default-value overload.

## Performance

Member access uses compiled expression-tree delegates generated once per type and cached. The
member-to-column map is built once per table or reader rather than per row, only members that
differ from a new instance are copied when cloning the default POCO, and values that already match
the target type are assigned without converting.

50,000 rows, warm, on the machine this was developed on: **23 ms**, against a plain
reflection + `Convert.ChangeType` baseline of **30 ms**.

## Tests

```bash
dotnet test

# with coverage
dotnet test -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

152 tests, using xUnit.net v3 with the Microsoft.Testing.Platform runner. `TestData` is a class
fixture, so each test class reads its own copy of `TestData.xml`.

The test data (`TestData.xml`) holds one table per scenario: a fully populated row with
every data type the mapper can produce, a sparse row, empty strings, boundary values,
columns that are not members of the POCO, database-style column names, snake_case names,
and a table of deliberately bad values for the failure paths.

The tests cover every data type, the three null policies, member shapes (properties, fields,
read-only and write-only members, types without a parameterless constructor, records), the
`DataRow`, `DataTable` and `IDataReader` paths, streaming, async cancellation, the reverse
direction, parameter binding against a minimal `DbCommand` fake, null options, duplicate and
null names in a reader, and the `DateOnly` and `TimeOnly` conversions.

Coverage of the library is **100% of lines** and **97.4% of branches**. The branches that
remain are defensive ones that cannot be reached from a public call: a public property
with neither a getter nor a setter, `New<T>` for a value type (the generic methods are
constrained to reference types), the unreachable default case of the null-policy
switch, and the null-key guard on the dictionary path, which no standard dictionary can
trigger because they all reject null keys. The last one is the await-continuation check the
compiler generates for the async state machine; it is only taken when a reader completes
`ReadAsync` asynchronously, which the in-memory reader in the tests never does.
