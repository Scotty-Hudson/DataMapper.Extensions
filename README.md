# DataMapper.Extensions

Extension methods that map `IDataReader`, `DataTable` and `DataRow` rows onto POCOs, and POCOs
back into a `DataTable` or `DbDataReader`.

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
- **Field initializers do not run on positional records.** A positional record has no
  parameterless constructor, so `New<T>` creates an uninitialized object. A record with a
  parameterless constructor runs its initializers as normal.
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
value the POCO already has.

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
extensions support: nullable target types, enums, `Guid` and `TimeSpan` from strings. A value that
is already the target type is assigned without converting.

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

## ConvertTo

```c#
var customerId = row.ConvertTo<long>("CustomerId");
var zip = row.ConvertTo("Zip", 0);            // default when the column is DBNull
var amount = row.ConvertTo<decimal>(options, "Amount");
```

`DBNull` becomes `null` for a nullable target type. For a non-nullable target it converts as it
always has, which for a string is an empty string. `NullHandling` is a mapping policy and does not
apply to `ConvertTo`; use the `defaultValue` overload for a fallback.

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

113 tests, using xUnit.net v3 with the Microsoft.Testing.Platform runner. `TestData` is a class
fixture, so each test class reads its own copy of `TestData.xml`.

The test data (`TestData.xml`) holds one table per scenario: a fully populated row with
every data type the mapper can produce, a sparse row, empty strings, boundary values,
columns that are not members of the POCO, database-style column names, snake_case names,
and a table of deliberately bad values for the failure paths.

Coverage of the library is **100% of lines** and **96% of branches**. The branches that
remain are defensive ones that cannot be reached from a public call: a public property
with neither a getter nor a setter, `New<T>` for a value type (the generic methods are
constrained to reference types), and the unreachable default case of the null-policy
switch.
