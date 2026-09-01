# NxTypeMapper README (A Universal Type‑Normalization Engine for Traditional Applications)

---

# NxTypeMapper — A Universal Engine That Eliminates Type Mismatches

## Purpose  
In traditional applications, the majority of runtime issues stem from **type mismatches**, such as:

- JSON numbers becoming `double` and failing to fit into `int`  
- SQLite returning `INTEGER` as `long`, causing mismatches with `int`  
- Timestamps returned as strings that cannot be parsed into `DateTime`  
- Boolean values represented as `0/1` instead of `true/false`  
- Inconsistent types between API and client  
- Database schema changes breaking application logic  

**NxTypeMapper absorbs all of these mismatches and ensures that your application consistently uses the correct C# types.**

Nx internally uses WASM + SQLite, so a SQLite‑oriented conversion step exists.  
However, **applications that do not use SQLite can still use NxTypeMapper without any modification.**  
(SQLiteType is used internally as an intermediate representation; SQLite itself is not required.)

---

# Architecture Overview

NxTypeMapper normalizes types through the following three‑stage pipeline:

```
PostgreSQLType (true DB type)
    ↓ PgTypeToSqliteType (intermediate SQLiteType)
SQLiteType (WASM projection type)
    ↓ SqliteType → CsType (canonical C# type)
CsType (canonical worldline)
    ↓ NxTypeMapper (final normalization)
The entire application operates on C# canonical types.
```

This structure ensures:

- JSON → C#  
- SQLite → C#  
- API → C#  

All type mismatches are **fully absorbed by NxTypeMapper**.

Even without SQLite,  
**PgType → SQLiteType → CsType → NxTypeMapper**  
remains valid internally.

SQLiteType is **not** a type for SQLite itself.  
It is an **intermediate representation used to determine the correct C# type**,  
so NxTypeMapper works perfectly even in applications that do not use SQLite.

---

# How to Integrate (Works in Traditional Applications)

You will use the following modules:

1. `NxRebuild.Api\Schema\DBSchemaProvider.cs`  
2. `NxRebuild.shared\ConvertedDBScms.cs`  
3. `NxRebuild.shared\NxTypeMapper.cs`  

Adjust namespaces as needed for your environment.

---

## 1. Retrieve DB Schema (Using DBSchemaProvider)

Nx provides a built‑in mechanism to automatically extract database schema information.

```csharp
var provider = new DBSchemaProvider(connectionString);
var schemas = provider.GetConvertedSchemas();
```

`GetConvertedSchemas()` reads the database table definitions and returns a list of `ConvertedTableSchema`.

This means **you do not need to manually write schema JSON**.

If you use a database other than PostgreSQL,  
extend DBSchemaProvider or implement your own provider.

---

## 2. Build the Type Map with NxTypeMapBuilder

```csharp
var typeMap = NxTypeMapBuilder.FromSchemas(schemas);
NxTypeMapper.Set(typeMap);
```

This defines the **canonical C# types** used throughout the application.

---

## 3. Convert JSON or SQLite Values into C# Types

```csharp
var converted = NxTypeMapper.ConvertRow("users", row);
```

This automatically normalizes:

- number → int / long / double  
- string → DateTime  
- "1"/"0" → bool  
- SQLite INTEGER → long/int  
- TEXT → string  

All values become correct C# types.

Even without SQLite,  
**JSON → C# normalization alone provides significant benefits.**

---

## 4. Save to the Database via Dapper

```csharp
connection.Execute(sql, converted);
```

Dapper handles C# → DB type conversion,  
so **type mismatches are completely eliminated.**

---

# Where NxTypeMapper Can Be Used

- Traditional Web APIs  
- Blazor / WASM  
- WPF / WinForms  
- Unity  
- MAUI  
- Mobile applications  
- Electron  
- Applications using local SQLite  
- JSON‑heavy applications  
- Legacy systems suffering from type inconsistencies  

**Any application that handles typed data can benefit from NxTypeMapper.  
The effect is especially strong in JSON, DB, and ORM‑based systems.**

---

# Key Points About NxTypeMapper

## 1. SQLiteType is *not* a SQLite‑specific type  
Internally, NxTypeMapper performs:

**PgType → SQLiteType → CsType**

SQLiteType is an **intermediate representation** used to determine the correct C# type.

This conversion occurs even when the application does not use SQLite.

---

## 2. SQLite’s “loose” type system makes it ideal as an intermediate layer  
SQLite’s type system is simpler than PostgreSQL or SQLServer:

- INTEGER  
- REAL  
- TEXT  
- BLOB  

This simplicity makes it a safe and stable intermediate layer:

- PgType → SQLiteType  
- SQLiteType → CsType  

This two‑step conversion is **more robust** than converting PgType → CsType directly.

---

## 3. NxTypeMapper works even without SQLite  
SQLiteType is purely an **internal intermediate representation**.  
SQLite does not need to exist in your application.

Traditional applications follow:

```
PgType → SQLiteType → CsType → NxTypeMapper
```

This ensures:

**JSON → C# → DB type mismatches are fully absorbed.**

---

## 4. NxTypeMapper is an engine designed to absorb type mismatches  
It unifies:

- JSON type inconsistencies  
- API type mismatches  
- DB schema changes  
- ORM auto‑conversion issues  
- SQLite type inconsistencies  

All into a single **canonical C# type system (CsType)**,  
eliminating many common sources of runtime errors.

---

# Benefits (Major Reduction in Type‑Related Issues)

- JSON type inconsistencies disappear  
- SQLite type mismatches disappear  
- API type mismatches disappear  
- DB schema changes become safer  
- UI type mismatches disappear  
- ORM auto‑conversion issues disappear  
- A single canonical C# type is used across all layers  
- Type causality becomes fully closed  

In traditional applications, **type‑related issues decrease significantly**.

---

# Conclusion  
NxTypeMapper is based on the Nx worldline model,  
yet it remains a **general‑purpose type‑normalization engine**  
that can be used directly in traditional applications.

By unifying type handling,  
it enables **more stable and predictable application behavior across all layers.**