\# NxTypeMapper README（従来型アプリでも使える汎用型変換エンジン）



\---



\# NxTypeMapper — 型ズレを根絶する汎用型変換エンジン



\## 目的  

従来型アプリでは、次のような「型ズレ」が障害の大半を占めます：



\- JSON の number が double になって int に入らない  

\- SQLite が INTEGER を long で返して int に入らない  

\- timestamp が string で返って DateTime に変換できない  

\- bool が 0/1 で返って true/false とズレる  

\- API とクライアントの型がズレる  

\- DB の型変更でアプリが壊れる  



\*\*NxTypeMapper はこれらの型ズレをすべて吸収し、  

アプリ全体で「正しい型」を一貫して使えるようにする仕組みです。\*\*



なお Nx では WASM + SQLite を使用するため途中に SQLite 用の変換が入っていますが、  

\*\*SQLite を使わない設計のアプリでもそのまま利用可能です。\*\*  

（内部処理で SQLiteType を経由しますが、SQLite が存在する必要はありません）



\---



\# 仕組みの概要



NxTypeMapper は次の三段階で型を正本化します：



```

PostgreSQLType（DBの真実の型）

&#x20;   ↓ PgTypeToSqliteType（中間表現としての SQLiteType）

SQLiteType（WASM 用の投影型）

&#x20;   ↓ SqliteType → CsType（C# の正本型）

CsType（正本世界線）

&#x20;   ↓ NxTypeMapper（正本型の適用）

C#型でアプリ全体が動きます。

```



この構造により：



\- JSON → C#  

\- SQLite → C#  

\- API → C#  



すべての型ズレが \*\*NxTypeMapper で吸収されます\*\*。



SQLite を使わない場合でも  

\*\*PgType → SQLiteType → CsType → NxTypeMapper\*\* の流れはそのまま使えます。



なお、ここでの SQLiteType は「SQLite のための型」ではなく、  

\*\*C# の型（CsType）を決めるための中間表現\*\*として扱われます。  

そのため SQLite を使わないアプリでも問題なく利用できます。



\---



\# 導入方法（従来型アプリでも使用可能）



使用するモジュールは次の 3 つです：



1\. `NxRebuild.Api\\Schema\\DBSchemaProvider.cs`  

2\. `NxRebuild.shared\\ConvertedDBScms.cs`  

3\. `NxRebuild.shared\\NxTypeMapper.cs`  



namespace はご自分の環境に合わせてください。



\---



\## ① DB スキーマを取得する（DBSchemaProvider を使用）



Nx では DB スキーマを自動で吸い上げる仕組みがあります。



```csharp

var provider = new DBSchemaProvider(connectionString);

var schemas = provider.GetConvertedSchemas(); 

```



`GetConvertedSchemas()` は DB のテーブル定義を読み取り、  

`ConvertedTableSchema` のリストとして返します。



このため \*\*スキーマ JSON を手書きする必要はありません。\*\*



PostgreSQL 以外の DB を使う場合は、  

DBSchemaProvider を拡張するか、独自の Provider を作成してください。



\---



\## ② NxTypeMapBuilder で型マップを構築する



```csharp

var typeMap = NxTypeMapBuilder.FromSchemas(schemas);

NxTypeMapper.Set(typeMap);

```



これで \*\*アプリ全体が使用する C# の正本型が決まります\*\*。



\---



\## ③ JSON や SQLite の値を C# 型に変換する



```csharp

var converted = NxTypeMapper.ConvertRow("users", row);

```



これだけで：



\- number → int / long / double  

\- string → DateTime  

\- "1"/"0" → bool  

\- SQLite INTEGER → long/int  

\- TEXT → string  



すべて正しい C# 型になります。



SQLite を使わない場合でも  

\*\*JSON → C# の型変換だけで十分効果があります。\*\*



\---



\## ④ Dapper に渡すだけで DB に正しく保存される



```csharp

connection.Execute(sql, converted);

```



Dapper が C# 型 → DB 型に自動変換するため、  

\*\*型ズレは完全に消えます。\*\*



\---



\# どんなアプリに使える？



\- 従来型 Web API  

\- Blazor / WASM  

\- WPF / WinForms  

\- Unity  

\- MAUI  

\- モバイルアプリ  

\- Electron  

\- ローカル SQLite を使うアプリ  

\- JSON を大量に扱うアプリ  

\- 型ズレで苦しんでいるレガシーアプリ  



\*\*「型を扱うアプリ」ならすべて利用できます。  

特に JSON・DB・ORM を使うアプリでは効果が最大化されます。\*\*



\---



\# NxTypeMapper の重要なポイント



\## 1. SQLiteType は “SQLite のための型” ではない  

NxTypeMapper 内部では  

\*\*PgType → SQLiteType → CsType\*\*  

という三段階変換を行いますが、  

ここでの SQLiteType は SQLite のためではなく、  

\*\*CsType を決めるための中間表現\*\*です。



SQLite を使わないアプリでもこの変換は内部で行われます。



\---



\## 2. SQLite の型体系は「ゆるい」ため、中間表現として最適  

SQLite の型体系は PostgreSQL や SQLServer よりも抽象度が低く、  

\*\*INTEGER / REAL / TEXT / BLOB\*\* の 4 種類にほぼ集約されます。



このため：



\- PgType → SQLiteType  

\- SQLiteType → CsType  



という二段階に分けることで、  

\*\*PgType → CsType を直接変換するより安全で壊れにくい構造\*\*になります。



\---



\## 3. SQLite を使わないアプリでも NxTypeMapper はそのまま使える  

SQLiteType はあくまで \*\*内部処理の中間表現\*\*であり、  

SQLite が存在する必要はありません。



従来型アプリでは：



```

PgType → SQLiteType → CsType → NxTypeMapper

```



の流れだけが使われ、  

\*\*JSON → C# → DB の型ズレが完全に吸収されます。\*\*



\---



\## 4. NxTypeMapper は「型ズレを吸収する」ためのエンジン  

\- JSON の型崩壊  

\- API の型ズレ  

\- DB の型変更  

\- ORM の勝手な型変換  

\- SQLite の型ズレ  



これらを \*\*C# の正本型（CsType）で統一する\*\*ため、  

従来アプリの障害原因の多くが解消されます。



\---



\# 導入メリット（従来アプリの障害が激減）



\- JSON の型崩壊が消える  

\- SQLite の型ズレが消える  

\- API の型ズレが消える  

\- DB の型変更に強くなる  

\- UI の型ズレが消える  

\- ORM の勝手な型変換が消える  

\- 全層で「C#型」が使える  

\- 型の因果が完全に閉じる  



従来アプリで頻発する型ズレ由来の障害が大幅に減少します。

\---



\# 最後に  

NxTypeMapper は Nx の世界線モデルを基盤としていますが、

\*\* 従来型アプリでもそのまま利用できる汎用的な型変換エンジンです。\*\*

型を扱うあらゆるアプリケーションで、より安定した運用が期待できます。



\---

