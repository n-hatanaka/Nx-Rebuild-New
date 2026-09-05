---

# **SQL 方言（Dialect）の主要な違いまとめ**

SQL は ANSI/ISO 標準があるけど、  
**各 DBMS が勝手に拡張した結果 “方言” が生まれている**。  
検索結果でも「SQL は英語の方言みたいなもの」と説明されている。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/)

以下、主要 DB の違いを “Nx の世界線モデル的に重要な部分” に絞ってまとめる。

---

# 1. **識別子のクォート規則（最重要）**

| DB | 識別子の書き方 | 備考 |
|----|----------------|------|
| **PostgreSQL** | `"column"` | 標準 SQL に最も準拠。大文字小文字保持。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/) |
| **MySQL** | `` `column` `` | バッククォートが正統派。ANSI モードで `"column"` も可。  [Emergent Mind](https://www.emergentmind.com/topics/sql-dialect-documentation) |
| **SQL Server** | `[column]` | T‑SQL の文化。ANSI モードで `"column"` も可。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/) |
| **Oracle** | `"column"` | ただし **大文字小文字を厳密に区別**する。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/) |
| **SQLite** | `"column"` | ANSI SQL に近い。  [Emergent Mind](https://www.emergentmind.com/topics/sql-dialect-documentation) |

**Nx の正本は PostgreSQL なので `"..."` が正しい。**

---

# 2. **文字列結合の方言**

検索結果でも明確に差があると記載されている。  [sqlpedia.org](https://sqlpedia.org/comparisons/)

- PostgreSQL：||

- Oracle：||

- SQLite：||

- MySQL：CONCAT()

- SQL Server：+

---

# 3. **LIMIT / TOP / FETCH の違い（ページング）**

検索結果の比較表より。  [sqlpedia.org](https://sqlpedia.org/comparisons/)

| DB | 書き方 |
|----|---------|
| PostgreSQL | `LIMIT n OFFSET m` |
| MySQL | `LIMIT n OFFSET m` |
| SQLite | `LIMIT n OFFSET m` |
| SQL Server | `TOP n` または `OFFSET m ROWS FETCH NEXT n ROWS ONLY` |
| Oracle | `FETCH FIRST n ROWS ONLY` または `ROWNUM` |

---

# 4. **型キャストの方言**

検索結果の一次情報より。  [Emergent Mind](https://www.emergentmind.com/topics/sql-dialect-documentation)

| DB | キャスト例 |
|----|-------------|
| PostgreSQL | `'123'::INTEGER` |
| MySQL | `CAST('123' AS SIGNED)` |
| SQL Server | `CAST('123' AS INT)` |
| Oracle | `CAST('123' AS NUMBER)` |
| SQLite | 型ゆるい（ストレージ型が固定されない） |

---

# 5. **日付関数の方言**

検索結果の比較表より。  [sqlpedia.org](https://sqlpedia.org/comparisons/)

| 操作 | PostgreSQL | MySQL | SQL Server |
|------|------------|--------|------------|
| 現在日時 | `NOW()` | `NOW()` | `GETDATE()` |
| 日付加算 | `date + INTERVAL '1 day'` | `DATE_ADD(date, INTERVAL 1 DAY)` | `DATEADD(day, 1, date)` |
| 日付差 | `date1 - date2` | `DATEDIFF(date1, date2)` | `DATEDIFF(day, date1, date2)` |

---

# 6. **UPSERT（方言の象徴）**

検索結果の比較表より。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/)

| DB | 書き方 |
|----|---------|
| PostgreSQL | `INSERT ... ON CONFLICT (id) DO UPDATE` |
| SQL Server | `MERGE INTO ... WHEN MATCHED THEN UPDATE` |
| MySQL | `INSERT ... ON DUPLICATE KEY UPDATE` |
| Oracle | `MERGE` |

---

# 7. **AUTO_INCREMENT / SERIAL の違い**

検索結果の DDL 比較より。  [sql-designer.com](https://sql-designer.com/blog/database-ddl-comparison)

| DB | 自動採番 |
|----|-----------|
| PostgreSQL | `SERIAL` / `IDENTITY` |
| MySQL | `AUTO_INCREMENT` |
| SQL Server | `IDENTITY (1,1)` |
| Oracle | `SEQUENCE` + `TRIGGER` |
| SQLite | `INTEGER PRIMARY KEY` |

---

# **まとめ：Nx が PostgreSQL を正本にする理由**

検索結果でも PostgreSQL は  
**「最も標準準拠で、方言が少ない」** と明言されている。  [courses.technogic.org](https://courses.technogic.org/sql/specialized-topics/dialects/)

だから Nx の世界線モデルは PostgreSQL を正本にしている。

- `"識別子クォート"` が標準 SQL と一致  
- MVCC が強力で世界線の整合性が保ちやすい  
- 拡張性が高く抽象構造の写像に向いている  
- 方言が少なく、他 DB への折り畳みが容易  

---
