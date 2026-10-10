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
```

53 tests, using xUnit.net v3 with the Microsoft.Testing.Platform runner.
