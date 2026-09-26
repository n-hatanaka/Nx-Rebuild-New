namespace NxRebuild.shared
{
    // 外部キー情報を表すクラス
    public class ForeignKeyInfo {
        public List<string> FromColumns { get; set; } = new();
        public string ToTable { get; set; } = string.Empty;
        public List<string> ToColumns { get; set; } = new();
    }

    // 変換後のテーブル情報クラス
    public class ConvertedTableSchema {
        public string TableName { get; set; } = string.Empty;
        public List<ConvertedColumnInfo> Columns { get; set; } = new();
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    }


    // 変換後のカラム情報クラス
    public class ConvertedColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string PostgresType { get; set; } = string.Empty;
        public string SqliteType { get; set; } = string.Empty;

        public bool IsPrimaryKey { get; set; } = false;
    }
}
