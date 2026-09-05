---

# # Nx DataObj CRUD 移植性ガイド  
**対象：BaseDataObj を継承して具象エンティティを書く開発者向け**  
**目的：Nx 標準 SQL（PostgreSQL ベース）が他 DB でどう扱われるかの比較と注意点**

---

# # 1. Nx 標準 CRUD（PostgreSQL ベース）

## ## SELECT（標準）
```sql
SELECT *
FROM "{TableName}"
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

## ## INSERT（標準）
```sql
INSERT INTO "{TableName}" ("col1", "col2", "tenant_code")
VALUES (@Col1, @Col2, @TenantCode);
```

## ## UPDATE（標準）
```sql
UPDATE "{TableName}"
SET "Visible" = @Visible
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

## ## DELETE（標準）
```sql
DELETE FROM "{TableName}"
WHERE "{IdColumn}" = @DataID
  AND "tenant_code" = @TenantCode;
```

---

# # 2. DB ごとの互換性一覧（CRUD 全体）

| DB | SELECT | INSERT | UPDATE | DELETE | 修正必要度 | 備考 |
|----|--------|--------|--------|--------|-------------|------|
| PostgreSQL | ◎ | ◎ | ◎ | ◎ | 0% | Nx 正本 |
| SQLite | ◎ | ◎ | ◎ | ◎ | 0% | ANSI SQL に近い |
| MySQL | ○ | ○ | ○ | ○ | 20% | 識別子は `` `col` `` 推奨 |
| SQL Server | ○ | ○ | ○ | ○ | 20% | `[col]` が文化的 |
| Oracle | △ | △ | △ | △ | 50% | パラメータ記法が `:Param` |

---

# # 3. DB 別の注意点（CRUD まとめ）

---

## ## PostgreSQL（Nx 正本）
**互換性：◎（完全対応）**

- `"column"` → 識別子として正しく扱われる  
- `@Param` → Dapper でそのまま使える  
- CRUD 全て完全互換  

**Nx 標準のままで問題なし。**

---

## ## SQLite
**互換性：◎（完全対応）**

- `"column"` → 識別子として扱われる  
- `@Param` → Dapper でそのまま使える  
- CRUD 全て完全互換  

**PostgreSQL とほぼ同じ感覚で使える。**

---

## ## MySQL / MariaDB
**互換性：○（ほぼ動くが識別子に注意）**

### ● 注意点（CRUD 共通）
- `"column"` は **文字列扱い**になる  
- 識別子は **バッククォート `` `column` ``** が正統派  
- ANSI モードを ON にすると `"column"` も識別子になるが、  
  **本番環境で ANSI モード前提は危険**

### ● パラメータ
- `@Param` → Dapper 経由ならそのまま使える

### ● 推奨書き方（MySQL）
```sql
UPDATE `TableName`
SET `Visible` = @Visible
WHERE `IdColumn` = @DataID
  AND `tenant_code` = @TenantCode;
```

---

## ## SQL Server（MSSQL）
**互換性：○（動くが文化が違う）**

### ● 注意点（CRUD 共通）
- `"column"` は ANSI モードなら識別子扱い  
- SQL Server の文化では **角括弧 `[column]`** が正統派  
- Dapper の `@Param` はそのまま使える

### ● 推奨書き方（SQL Server）
```sql
SELECT *
FROM [TableName]
WHERE [IdColumn] = @DataID
  AND [tenant_code] = @TenantCode;
```

---

## ## Oracle
**互換性：△（CRUD 全体で癖が強い）**

### ● 注意点（CRUD 共通）
- `"column"` は **大文字小文字を区別する識別子**になる  
- パラメータは `:Param` 形式  
- Dapper の Oracle Provider を使う場合は記法変更が必須

### ● 推奨書き方（Oracle）
```sql
UPDATE "TableName"
SET "Visible" = :Visible
WHERE "IdColumn" = :DataID
  AND "tenant_code" = :TenantCode;
```

---

# # 4. Nx 開発者向け総括（具象を書く人への注意）

- Nx の BaseDataObj は **PostgreSQL を正本**として設計  
- 他 DB を使う場合は **識別子とパラメータ記法を自分で調整すること**  
- 世界線モデルは DB 非依存だが、SQL の文化は DB ごとに違う  
- 特に MySQL / SQL Server / Oracle は識別子の扱いが異なるため注意  
- Nx の抽象構造は折り畳めるが、DB の物理法則は折り畳めない  

---