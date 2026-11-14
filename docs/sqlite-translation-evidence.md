# SQLite translation evidence

The [integration project](../tests/AlephMapper.IntegrationTests/AlephMapper.IntegrationTests.csproj) pins `Microsoft.EntityFrameworkCore.Sqlite` to **10.0.14** and runs against an in-memory SQLite database. Its [SQL contract tests](../tests/AlephMapper.IntegrationTests/SqlTranslationContractTests.cs) use `ToQueryString()` and execute each query. These checks establish the listed shapes for this provider/version; they do not certify every projection or another provider.

| Generated expression | SQL evidence | Runtime evidence |
| --- | --- | --- |
| `FirstName + " " + LastName` | SQL contains the SQLite concatenation operator and both input columns. A control top-level client projection selects the two columns without SQL concatenation. | The generated query returns the expected combined name. |
| `IsActive && FirstName == "John"` | Both columns and the name value occur in the `WHERE` clause. | Only the expected employee is returned. |
| `Department?.Name ?? "Unassigned"` with `Rewrite` | SQL uses `LEFT JOIN` and `COALESCE`. | Rows with and without a department return the expected text. |
| Nested order/customer/line projection | The [order test](../tests/AlephMapper.IntegrationTests/ArticleMappingTests.cs) checks a join plus SQL `ef_sum` and `ef_multiply` over price and quantity. | Projected customer names and totals match seeded orders and an in-memory mapping. |

The tests assert operations and columns rather than full SQL snapshots or incidental aliases. Top-level client evaluation can make a query succeed without doing the desired computation in SQL; the scalar control case makes that difference visible. A generated tree can still fail translation for another shape. Inspect provider exceptions and SQL for the specific query you intend to ship.
