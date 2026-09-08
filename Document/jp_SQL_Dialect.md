# Nx における SQL 方言と DB 互換性ドキュメント

## ● 1. 概要
Nx は SQLite と PostgreSQL を併用する構造を持つが、互換性問題はほぼ発生しない。  
理由は Nx が SQL の最小共通部分（Common Subset）のみを使用し、  
DB 方言が発生する領域を意図的に踏まないためである。

Nx の世界線モデルは縦方向の因果で閉じており、  
SQL の複雑性が抽象核に影響しない設計になっている。

---

## ● 2. Nx が使用する SQL の範囲（Common Subset）

Nx が採用する SQL は以下の領域に限定される：

- 基本的な CRUD  
- WHERE 条件は単独（複合条件なし）  
- JOIN を使用しない  
- 関数を使用しない  
- 型キャストを使用しない  
- 日付関数を使用しない  
- UPSERT を使用しない（INSERT/UPDATE 分離）

これらは SQLite と PostgreSQL の完全互換領域であり、  
ANSI SQL の最も安全な部分に相当する。

---

## ● 3. 互換性が保たれる理由

### ■ 3.1 WHERE 条件が単独
単独条件は全 DB で完全互換であり、  
関数・複合条件・サブクエリによる方言差が発生しない。

```
WHERE id = @id
WHERE name = @name
WHERE flag = 1
```

---

### ■ 3.2 JOIN を使用しない
JOIN は DB 方言の最大の差分領域であるが、  
Nx は縦方向の因果でデータを扱うため JOIN を必要としない。

これにより、SQLite の JOIN 弱さや Oracle のヒント句などの差分を完全に回避できる。

---

### ■ 3.3 基本 CRUD の完全互換

```
INSERT INTO table (a, b) VALUES (@a, @b)
UPDATE table SET a = @a WHERE id = @id
DELETE FROM table WHERE id = @id
```

SQLite と PostgreSQL の基本 CRUD は完全互換である。

---

### ■ 3.4 型差をモデル側で吸収
SQLite は型がゆるく、PostgreSQL は厳密だが、  
Nx はモデルが正本（canonical model）であるため、  
型差はモデル側で吸収される。

---

### ■ 3.5 UPSERT を使用しない
UPSERT は DB 方言の象徴であるが、  
Nx は INSERT と UPDATE を分離するため、  
UPSERT 方言の差分が発生しない。

---

## ● 4. ページングのみ方言差が発生する

巨大テーブルに対してページングが必要な場合のみ、  
DB 方言の差分が発生する。

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

Nx が採用する SQLite と PostgreSQL は LIMIT/OFFSET で完全互換である。

---

## ● 5. Dapper による DB 判別と SQL 切り替え

Dapper 自体は方言判別機能を持たないが、  
接続している ADO.NET プロバイダの型から DB 種類を判別できる。

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

これにより、ページング専用メソッドのみ SQL を切り替える設計が可能となる。

---

## ● 6. ページング SQL 切り替えの最小構成例

### ■ DB 種類の判別
```
var dbType = connection.GetType().Name;
```

### ■ SQL の切り替え
```
string sql = dbType switch
{
    "NpgsqlConnection" => "SELECT ... LIMIT @limit OFFSET @offset",
    "SqliteConnection" => "SELECT ... LIMIT @limit OFFSET @offset",
    "SqlConnection"    => "SELECT ... OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY",
    _ => throw new NotSupportedException()
};
```

### ■ 実行
```
var rows = connection.Query<Entity>(sql, new { limit, offset });
```

---

## ● 7. 結論：Nx の DB 互換性は「ページングのみ切り替えれば完全」

領域 / 互換性  
CRUD / 完全互換  
WHERE 単独条件 / 完全互換  
JOIN / 使用しない  
型 / モデル側で吸収  
UPSERT / 使用しない  
日付関数 / 使用しない  
ページング / ● ここだけ切り替えればよい

Nx は SQLite＋PostgreSQL の共通部分だけで動作する基礎技術であり、  
縦方向の因果を保ったまま DB 互換性を維持できる。