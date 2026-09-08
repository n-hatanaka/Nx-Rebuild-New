# Nx SQL Dialects and Database Compatibility Documentation

## ● 1. Overview
Nx uses both SQLite and PostgreSQL, yet compatibility issues are almost nonexistent.  
This is because Nx relies only on the minimal common subset of SQL, intentionally avoiding areas where dialect differences occur.

Nx’s worldline model is vertically closed, meaning SQL complexity does not affect the abstract core.

---

## ● 2. SQL Scope Used by Nx (Common Subset)

Nx restricts SQL usage to the following areas:

- Basic CRUD  
- Single-condition WHERE clauses (no compound conditions)  
- No JOIN  
- No SQL functions  
- No type casting  
- No date functions  
- No UPSERT (INSERT/UPDATE are separated)

These areas are fully compatible between SQLite and PostgreSQL and correspond to the safest portion of ANSI SQL.

---

## ● 3. Why Compatibility Is Preserved

### ■ 3.1 Single-condition WHERE clauses
Single conditions are fully compatible across all databases.  
No functions, compound conditions, or subqueries means no dialect differences.

```
WHERE id = @id
WHERE name = @name
WHERE flag = 1
```

---

### ■ 3.2 No JOIN usage
JOIN is the largest source of SQL dialect differences.  
Nx does not require JOIN because data is handled through vertical causality.

This avoids SQLite’s weak JOIN optimization, Oracle’s hint requirements, and other dialect-specific behaviors.

---

### ■ 3.3 Full compatibility in basic CRUD

```
INSERT INTO table (a, b) VALUES (@a, @b)
UPDATE table SET a = @a WHERE id = @id
DELETE FROM table WHERE id = @id
```

SQLite and PostgreSQL are fully compatible in basic CRUD operations.

---

### ■ 3.4 Type differences absorbed by the model
SQLite uses dynamic typing, while PostgreSQL is strict.  
Nx’s canonical model absorbs these differences, ensuring consistent behavior.

---

### ■ 3.5 No UPSERT usage
UPSERT is highly dialect-dependent.  
Nx avoids this by separating INSERT and UPDATE, eliminating dialect issues entirely.

---

## ● 4. Paging is the Only Area with Dialect Differences

Paging is required only for extremely large tables.  
This is the only area where SQL dialect differences appear.

### SQLite / PostgreSQL / MySQL
```
LIMIT @limit OFFSET @offset
```

### SQL Server
```
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
```

### Oracle
```
FETCH FIRST @limit ROWS ONLY
```

Nx uses SQLite and PostgreSQL, which share full compatibility via LIMIT/OFFSET.

---

## ● 5. Detecting Database Type with Dapper

Dapper does not detect dialects directly,  
but the underlying ADO.NET provider type reveals the database in use.

```
switch (connection)
{
    case NpgsqlConnection:
        // PostgreSQL
        break;

    case SqliteConnection:
        // SQLite
        break;

    case SqlConnection:
        // SQL Server
        break;

    case MySqlConnection:
        // MySQL
        break;
}
```

This allows Nx to switch SQL only in the paging method.

---

## ● 6. Minimal Paging SQL Switching Example

### ■ Detect database type
```
var dbType = connection.GetType().Name;
```

### ■ Switch SQL
```
string sql = dbType switch
{
    "NpgsqlConnection" => "SELECT ... LIMIT @limit OFFSET @offset",
    "SqliteConnection" => "SELECT ... LIMIT @limit OFFSET @offset",
    "SqlConnection"    => "SELECT ... OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY",
    _ => throw new NotSupportedException()
};
```

### ■ Execute
```
var rows = connection.Query<Entity>(sql, new { limit, offset });
```

---

## ● 7. Conclusion: Nx Database Compatibility Is Complete Except for Paging

Area / Compatibility  
CRUD / Fully compatible  
Single-condition WHERE / Fully compatible  
JOIN / Not used  
Types / Absorbed by the model  
UPSERT / Not used  
Date functions / Not used  
Paging / ● Only this area requires switching

Nx operates entirely within the shared subset of SQLite and PostgreSQL,  
maintaining full compatibility while preserving vertical causality in its abstract core.