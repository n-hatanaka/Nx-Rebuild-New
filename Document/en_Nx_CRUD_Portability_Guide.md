---

# # Nx DataObj CRUD Portability Guide  
**Audience:** Developers implementing concrete DataObj classes that inherit from `BaseDataObj`  
**Purpose:** Explain how Nx’s standard SQL (PostgreSQL‑based) behaves across major databases, and what adjustments are required.

---

# # 1. Nx Standard CRUD (PostgreSQL‑based)

## ## SELECT (standard)
```sql
SELECT *
FROM "{TableName}"
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

## ## INSERT (standard)
```sql
INSERT INTO "{TableName}" ("col1", "col2", "tenant_code")
VALUES (@Col1, @Col2, @TenantCode);
```

## ## UPDATE (standard)
```sql
UPDATE "{TableName}"
SET "Visible" = @Visible
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

## ## DELETE (standard)
```sql
DELETE FROM "{TableName}"
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

---

# # 2. CRUD Compatibility Across Databases

| Database | SELECT | INSERT | UPDATE | DELETE | Adjustment Needed | Notes |
|---------|--------|--------|--------|--------|-------------------|-------|
| PostgreSQL | ◎ | ◎ | ◎ | ◎ | 0% | Nx’s canonical SQL |
| SQLite | ◎ | ◎ | ◎ | ◎ | 0% | Very close to ANSI SQL |
| MySQL / MariaDB | ○ | ○ | ○ | ○ | ~20% | Identifiers use backticks |
| SQL Server | ○ | ○ | ○ | ○ | ~20% | Identifiers use brackets |
| Oracle | △ | △ | △ | △ | ~50% | Parameter syntax differs |

---

# # 3. Database‑Specific Notes (CRUD Summary)

---

## ## PostgreSQL (Nx canonical)
**Compatibility: ◎ (full)**

- `"column"` → valid identifier  
- `@Param` → works with Dapper  
- Full CRUD compatibility  

**No changes required.**

---

## ## SQLite
**Compatibility: ◎ (full)**

- `"column"` → valid identifier  
- `@Param` → works with Dapper  
- Full CRUD compatibility  

**Behaves almost identical to PostgreSQL.**

---

## ## MySQL / MariaDB
**Compatibility: ○ (minor adjustments)**

### Key points
- `"column"` is treated as a **string literal**, not an identifier  
- Identifiers should use **backticks**: `` `column` ``  
- ANSI mode allows `"column"` as an identifier, but relying on ANSI mode in production is risky

### Parameter syntax
- `@Param` works with Dapper

### Recommended MySQL style
```sql
UPDATE `TableName`
SET `Visible` = @Visible
WHERE `IdColumn` = @DataID
  AND `tenant_code` = @TenantCode;
```

---

## ## SQL Server (MSSQL)
**Compatibility: ○ (minor adjustments)**

### Key points
- `"column"` works only when ANSI mode is enabled  
- SQL Server’s cultural norm is **brackets**: `[column]`  
- `@Param` works with Dapper

### Recommended SQL Server style
```sql
SELECT *
FROM [TableName]
WHERE [IdColumn] = @DataID
  AND [tenant_code] = @TenantCode;
```

---

## ## Oracle
**Compatibility: △ (significant differences)**

### Key points
- `"column"` becomes a **case‑sensitive identifier**  
- Parameters use **colon syntax**: `:Param`  
- Dapper’s Oracle provider requires parameter syntax changes

### Recommended Oracle style
```sql
UPDATE "TableName"
SET "Visible" = :Visible
WHERE "IdColumn" = :DataID
  AND "tenant_code" = :TenantCode;
```

---

# # 4. Summary for Nx Developers (Concrete Implementers)

- Nx’s `BaseDataObj` is designed with **PostgreSQL as the canonical model**  
- When targeting other databases, developers must adjust:  
  - **Identifier quoting rules**  
  - **Parameter syntax**  
- Nx’s worldline model is database‑agnostic,  
  but **SQL dialects follow their own “physical laws”**  
- MySQL, SQL Server, and Oracle require special attention to identifier rules  
- Oracle additionally requires parameter syntax changes

---
