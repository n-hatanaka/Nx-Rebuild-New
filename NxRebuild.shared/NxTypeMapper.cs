// NxTypeMapper.cs
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace NxRebuild.Shared
{
    /// <summary>
    /// 世界の「型の正本」を保持し、全データを正しい C# 型へ矯正するための静的マッパー。
    /// BaseDataObj / SQLite / API の全世界線で同じ型を使うための中核。
    /// </summary>
    public static class NxTypeMapper
    {
        /// <summary>
        /// 現在の世界線で使う「型マップ正本」。
        /// 起動時に 1 回だけセットされる。
        /// </summary>
        public static NxTypeMap? Current { get; private set; }

        /// <summary>
        /// 世界線の正本 NxTypeMap を差し替える。
        /// クライアント起動時・サーバー API 内で使用。
        /// </summary>
        public static void Set(NxTypeMap map)
        {
            Current = map;
        }

        /// <summary>
        /// 単一値を正しい C# 型へ変換する。
        /// Current が null の場合は変換せずそのまま返す。
        /// </summary>
        public static object? Convert(string table, string column, object? value)
        {
            if (Current == null)
                return value;

            return Current.Convert(table, column, value);
        }

        /// <summary>
        /// 1行分の辞書を一括で型変換する。
        /// Dapper の INSERT / API の返却値などで使用。
        /// </summary>
        public static Dictionary<string, object?> ConvertRow(string table, Dictionary<string, object> row)
        {
            if (Current == null)
                return new Dictionary<string, object?>(row);

            return Current.ConvertRow(table, row);
        }
    }

    /// <summary>
    /// 実際の型マップ本体。
    /// [テーブル名][カラム名] = CsType の辞書を持ち、
    /// Convert() で値を正しい型へ矯正する。
    /// </summary>
    public class NxTypeMap
    {
        public Dictionary<string, Dictionary<string, string>> ColumnTypeMap { get; set; }
            = new();
     
        /// <summary>
        /// テーブル名を渡すと、型マップに基づいて
        /// 「初期値が入った1行分の辞書」を返す。
        /// 数値=0、bool=false、datetime=MinValue、string=""、その他=null。
        /// </summary>
        public Dictionary<string, object?> CreateEmptyRow(string table)
        {
            var result = new Dictionary<string, object?>();
        
            if (!ColumnTypeMap.TryGetValue(table, out var cols))
                throw new Exception($"型マップにテーブル {table} が存在しません。");
        
            foreach (var kv in cols)
            {
                var column = kv.Key;
                var type = kv.Value;
        
                result[column] = GetDefaultValue(type);
            }
        
            return result;
        }
        
        /// <summary>
        /// CsType に応じた初期値を返す。
        /// </summary>
        private object? GetDefaultValue(string csType)
        {
            switch (csType)
            {
                case "int":
                case "long":
                case "double":
                    return 0;
        
                case "bool":
                    return false;
        
                case "datetime":
                    return DateTime.MinValue; // マイクロ秒対応の最小値
        
                case "string":
                    return ""; // 空文字。null にしたいならここを null にする
        
                default:
                    return null;
            }
        }
        /// <summary>
        /// 単一値を「型マップで定義された正本型」に変換する。
        /// JSON → C#、SQLite → C# の型ズレを吸収する。
        /// </summary>
        public object? Convert(string table, string column, object? value)
        {
            // テーブル・カラムが型マップに存在しない場合は変換しない
            if (!ColumnTypeMap.TryGetValue(table, out var cols))
                return value;

            if (!cols.TryGetValue(column, out var type))
                return value;

            // -----------------------------
            // JSON Element の場合（API から来る値）
            // -----------------------------
            if (value is JsonElement el)
            {
                switch (type)
                {
                    case "int":
                        // JSON Number → int
                        if (el.TryGetInt64(out var l)) return (int)l;
                        return 0;

                    case "long":
                        // JSON Number → long
                        if (el.TryGetInt64(out var ll)) return ll;
                        return 0L;

                    case "double":
                        // JSON Number → double
                        if (el.TryGetDouble(out var d)) return d;
                        return 0.0;

                    case "string":
                        // JSON String → string
                        return el.GetString();

                    case "bool":
                        // JSON true/false → bool
                        if (el.ValueKind == JsonValueKind.True) return true;
                        if (el.ValueKind == JsonValueKind.False) return false;

                        // JSON Number → bool（0/1）
                        if (el.TryGetInt32(out var bi)) return bi != 0;
                        return false;

                    case "datetime":
                        // JSON String → DateTime（マイクロ秒対応）
                        if (el.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(el.GetString(), out var dt))
                            return dt;

                        return DateTime.MinValue;
                }
            }

            // -----------------------------
            // C# の値として来ている場合（SQLite / Dapper）
            // -----------------------------
            try
            {
                switch (type)
                {
                    case "int":
                        // long/double/string → int に矯正
                        if (value is int) return value;
                        if (value is long ll) return (int)ll;
                        if (value is double dd) return (int)dd;
                        if (value is string s && int.TryParse(s, out var si)) return si;
                        return 0;

                    case "long":
                        // int/double/string → long に矯正
                        if (value is long) return value;
                        if (value is int i) return (long)i;
                        if (value is double d) return (long)d;
                        if (value is string s2 && long.TryParse(s2, out var sl)) return sl;
                        return 0L;

                    case "double":
                        // int/long/string → double に矯正
                        if (value is double) return value;
                        if (value is float f) return (double)f;
                        if (value is int i2) return (double)i2;
                        if (value is long l2) return (double)l2;
                        if (value is string sd && double.TryParse(sd, out var sdv)) return sdv;
                        return 0.0;

                    case "string":
                        // 何でも string に変換
                        return value?.ToString();

                    case "bool":
                        // 数値・文字列 → bool に矯正
                        if (value is bool) return value;
                        if (value is int bi) return bi != 0;
                        if (value is long bl) return bl != 0;
                        if (value is string bs &&
                            (bs == "1" || bs.Equals("true", StringComparison.OrdinalIgnoreCase)))
                            return true;
                        return false;

                    case "datetime":
                        // string → DateTime（マイクロ秒対応）
                        if (value is DateTime) return value;
                        if (value is string ds && DateTime.TryParse(ds, out var dt2)) return dt2;
                        return DateTime.MinValue;
                }
            }
            catch
            {
                // 変換失敗時はそのまま返す
                return value;
            }

            return value;
        }

        /// <summary>
        /// 1行分の辞書を一括で型変換する。
        /// Dapper の INSERT / API の返却値などで使用。
        /// </summary>
        public Dictionary<string, object?> ConvertRow(string table, Dictionary<string, object> row)
        {
            var result = new Dictionary<string, object?>();

            foreach (var kvp in row)
            {
                // 各カラムを正しい型へ変換
                result[kvp.Key] = Convert(table, kvp.Key, kvp.Value);
            }

            return result;
        }
    }

    /// <summary>
    /// スキーマ情報から NxTypeMap を構築するビルダー。
    /// SQLite / RDB の型文字列を CsType に変換する。
    /// </summary>
    public static class NxTypeMapBuilder
    {
        /// <summary>
        /// スキーマ一覧から型マップ正本を生成する。
        /// </summary>
        public static NxTypeMap FromSchemas(List<ConvertedTableSchema> schemas)
        {
            var map = new NxTypeMap();

            foreach (var table in schemas)
            {
                map.ColumnTypeMap[table.TableName] = new Dictionary<string, string>();

                foreach (var col in table.Columns)
                {
                    // サーバー側で CsType が指定されていればそれを優先
                    var csType =
                        !string.IsNullOrEmpty(col.CsType)
                            ? col.CsType
                            : SqlTypeToCsType(col.SqliteType);

                    map.ColumnTypeMap[table.TableName][col.ColumnName] = csType;
                }
            }

            return map;
        }

        /// <summary>
        /// SQLite / RDB の型文字列を C# の型名へ変換する。
        /// </summary>
        public static string SqlTypeToCsType(string sqliteType)
        {
            var t = sqliteType.ToUpperInvariant();

            // BIGINT → long
            if (t.Contains("BIGINT")) return "long";

            // INT → int
            if (t.Contains("INT")) return "int";

            // REAL / DOUBLE / FLOAT → double
            if (t.Contains("REAL") || t.Contains("DOUBLE") || t.Contains("FLOAT"))
                return "double";

            // TEXT / CHAR / CLOB → string
            if (t.Contains("TEXT") || t.Contains("CHAR") || t.Contains("CLOB"))
                return "string";

            // BOOL → bool
            if (t.Contains("BOOL")) return "bool";

            // DATE / TIME → datetime（マイクロ秒対応）
            if (t.Contains("DATE") || t.Contains("TIME")) return "datetime";

            // その他は string として扱う
            return "string";
        }
    }
    /// <summary>
    /// 既存のスキーマ DTO を想定した簡易定義。
     /// 実際のプロジェクト側の定義に合わせて調整して。
    /// </summary>
    public class ConvertedTableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public List<ConvertedColumnSchema> Columns { get; set; } = new();
        public List<ConvertedForeignKeySchema> ForeignKeys { get; set; } = new();
    }

    public class ConvertedColumnSchema
    {
        public string ColumnName { get; set; } = string.Empty;
        public string SqliteType { get; set; } = string.Empty;

        // もしサーバー側で CsType を決めて渡すならここに入れる
        public string CsType { get; set; } = string.Empty;

        public bool IsPrimaryKey { get; set; }
    }

    public class ConvertedForeignKeySchema
    {
        public string FromColumn { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string ToColumn { get; set; } = string.Empty;
    }
}