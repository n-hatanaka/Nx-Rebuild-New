using System;
using System.Collections.Generic;
using System.Text.Json;
using NxRebuild.shared;

namespace NxRebuild.shared {
    /// <summary>
    /// Nx 世界線の「型の正本」を保持する静的マッパー。
    /// - API / SQLite / JSON の型ズレを吸収し、正しい C# 型へ矯正する。
    /// 型揺れを吸収するため、NxTypeMapBuilder でスキーマから NxTypeMap を生成し、NxTypeMapper にセットする。
    /// - BaseDataObj / InsertMaster / API / jsonToTbl / tblTojson の全世界線で同じ型を使うための中核。
    /// JSON → C#型 → Dapper → [PostgreSQL あるいは SQLite] という流れで、型をC#型に直してかえす。
    /// </summary>
    public static class NxTypeMapper {
        /// <summary>
        /// 世界線の正本 NxTypeMap。
        /// NxTypeMapBuilder によって構築され、ここにセットされる。
        /// </summary>
        public static NxTypeMap? Current { get; private set; }

        /// <summary>
        /// 世界線の正本 NxTypeMap を差し替える。
        /// </summary>
        public static void Set(NxTypeMap map) {
            Current = map;
        }

        /// <summary>
        /// 単一値を正しい C# 型へ変換する。
        /// JSON / SQLite / Dapper の型ズレを吸収する。
        /// </summary>
        public static object? Convert(string table, string column, object? value) {
            if (Current == null)
                return value;

            return Current.Convert(table, column, value);
        }

        /// <summary>
        /// 1行分の辞書を一括で型変換する。
        /// BaseDataObj.Setproperties() や InsertMaster で使用。
        /// </summary>
        public static Dictionary<string, object?> ConvertRow(string table, Dictionary<string, object> row) {
            if (Current == null)
                return new Dictionary<string, object?>(row);

            return Current.ConvertRow(table, row);
        }
    }

    /// <summary>
    /// Nx 世界線の「型の正本」本体。
    /// - [テーブル名][カラム名] = CsType の辞書を保持する。
    /// - Convert() により値を正しい型へ矯正する。
    /// </summary>
    public class NxTypeMap {
        /// <summary>
        /// テーブル名 → (カラム名 → CsType) の辞書。
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ColumnTypeMap { get; set; }
            = new();

        /// <summary>
        /// テーブル名を渡すと、型マップに基づいて
        /// 「初期値が入った1行分の辞書」を返す。
        /// 新規レコード作成時に使用。
        /// </summary>
        public Dictionary<string, object?> CreateEmptyRow(string table) {
            var result = new Dictionary<string, object?>();

            if (!ColumnTypeMap.TryGetValue(table, out var cols))
                throw new Exception($"型マップにテーブル {table} が存在しません。");

            foreach (var kv in cols) {
                var column = kv.Key;
                var type = kv.Value;

                result[column] = GetDefaultValue(type);
            }

            return result;
        }

        /// <summary>
        /// CsType に応じた初期値を返す。
        /// </summary>
        private object? GetDefaultValue(string csType) {
            return csType switch {
                "int" => 0,
                "long" => 0L,
                "double" => 0.0,
                "bool" => false,
                "datetime" => DateTime.MinValue,
                "string" => "",
                _ => null
            };
        }

        /// <summary>
        /// 単一値を「型マップで定義された正本型」に変換する。
        /// JSON → C#、SQLite → C# の型ズレを吸収する。
        /// </summary>
        public object? Convert(string table, string column, object? value) {
            if (!ColumnTypeMap.TryGetValue(table, out var cols))
                return value;

            if (!cols.TryGetValue(column, out var type))
                return value;

            // -----------------------------
            // JSON Element の場合（API から来る値）
            // -----------------------------
            if (value is JsonElement el) {
                switch (type) {
                    case "int":
                        if (el.TryGetInt64(out var l)) return (int)l;
                        return 0;

                    case "long":
                        if (el.TryGetInt64(out var ll)) return ll;
                        return 0L;

                    case "double":
                        if (el.TryGetDouble(out var d)) return d;
                        return 0.0;

                    case "string":
                        return el.GetString();

                    case "bool":
                        if (el.ValueKind == JsonValueKind.True) return true;
                        if (el.ValueKind == JsonValueKind.False) return false;
                        if (el.TryGetInt32(out var bi)) return bi != 0;
                        return false;

                    case "datetime":
                        if (el.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(el.GetString(), out var dt))
                            return dt;
                        return DateTime.MinValue;
                }
            }

            // -----------------------------
            // C# の値として来ている場合（SQLite / Dapper）
            // -----------------------------
            try {
                switch (type) {
                    case "int":
                        if (value is int) return value;
                        if (value is long ll) return (int)ll;
                        if (value is double dd) return (int)dd;
                        if (value is string s && int.TryParse(s, out var si)) return si;
                        return 0;

                    case "long":
                        if (value is long) return value;
                        if (value is int i) return (long)i;
                        if (value is double d) return (long)d;
                        if (value is string s2 && long.TryParse(s2, out var sl)) return sl;
                        return 0L;

                    case "double":
                        if (value is double) return value;
                        if (value is float f) return (double)f;
                        if (value is int i2) return (double)i2;
                        if (value is long l2) return (double)l2;
                        if (value is string sd && double.TryParse(sd, out var sdv)) return sdv;
                        return 0.0;

                    case "string":
                        return value?.ToString();

                    case "bool":
                        if (value is bool) return value;
                        if (value is int bi) return bi != 0;
                        if (value is long bl) return bl != 0;
                        if (value is string bs &&
                            (bs == "1" || bs.Equals("true", StringComparison.OrdinalIgnoreCase)))
                            return true;
                        return false;

                    case "datetime":
                        if (value is DateTime) return value;
                        if (value is string ds && DateTime.TryParse(ds, out var dt2)) return dt2;
                        return DateTime.MinValue;
                }
            } catch {
                return value;
            }

            return value;
        }

        /// <summary>
        /// 1行分の辞書を一括で型変換する。
        /// BaseDataObj.Setproperties() や InsertMaster で使用。
        /// </summary>
        public Dictionary<string, object?> ConvertRow(string table, Dictionary<string, object> row) {
            var result = new Dictionary<string, object?>();

            foreach (var kvp in row) {
                result[kvp.Key] = Convert(table, kvp.Key, kvp.Value);
            }

            return result;
        }
    }

    /// <summary>
    /// Shared のスキーマ DTO から NxTypeMap を構築するビルダー。
    /// 世界線の型変換（Pg → SQLite → Cs）を一括で担当する。
    /// </summary>
    public static class NxTypeMapBuilder {
        /// <summary>
        /// スキーマ一覧から NxTypeMap（世界線の正本）を生成する。
        /// </summary>
        public static NxTypeMap FromSchemas(List<ConvertedTableSchema> schemas) {
            var map = new NxTypeMap();

            foreach (var table in schemas) {
                map.ColumnTypeMap[table.TableName] = new Dictionary<string, string>();

                foreach (var col in table.Columns) {
                    // ---------------------------------------------------------
                    // ★ PostgreSQL → SQLiteType（世界線の前段階）
                    // ---------------------------------------------------------
                    col.SqliteType = PgTypeToSqliteType(col.PostgresType);

                    // ---------------------------------------------------------
                    // ★ SQLiteType → CsType（既存ロジック）
                    // ---------------------------------------------------------
                    var csType = SqlTypeToCsType(col.SqliteType);

                    map.ColumnTypeMap[table.TableName][col.ColumnName] = csType;
                }
            }

            return map;
        }

        /// <summary>
        /// PostgreSQL の型文字列を SQLite の型文字列へ変換する。
        /// API は PostgresType を返すだけなので、WASM 側で変換する。
        /// </summary>
        public static string PgTypeToSqliteType(string pgType) {
            var t = pgType.ToLowerInvariant();

            if (t.Contains("bigint")) return "BIGINT";
            if (t.Contains("int")) return "INTEGER";
            if (t.Contains("double") || t.Contains("real") || t.Contains("float"))
                return "REAL";
            if (t.Contains("numeric") || t.Contains("decimal"))
                return "REAL";
            if (t.Contains("bool")) return "BOOLEAN";
            if (t.Contains("char") || t.Contains("text") || t.Contains("varchar"))
                return "TEXT";
            if (t.Contains("date") || t.Contains("time"))
                return "TEXT"; // SQLite は datetime を TEXT で扱う

            return "TEXT";
        }

        /// <summary>
        /// SQLite の型文字列を C# の型名へ変換する。
        /// </summary>
        public static string SqlTypeToCsType(string sqliteType) {
            var t = sqliteType.ToUpperInvariant();

            if (t.Contains("BIGINT")) return "long";
            if (t.Contains("INT")) return "int";
            if (t.Contains("REAL") || t.Contains("DOUBLE") || t.Contains("FLOAT"))
                return "double";
            if (t.Contains("TEXT") || t.Contains("CHAR") || t.Contains("CLOB"))
                return "string";
            if (t.Contains("BOOL")) return "bool";
            if (t.Contains("DATE") || t.Contains("TIME")) return "datetime";

            return "string";
        }
    }
}
