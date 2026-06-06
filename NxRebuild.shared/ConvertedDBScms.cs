namespace NxRebuild.shared
{
    // 外部キー情報を表すクラス
    public class ForeignKeyInfo
    {
        public string FromColumn { get; set; } = string.Empty; // 自分のテーブルのカラム名 (例: user_id)
        public string ToTable { get; set; } = string.Empty;    // 相手（参照先）のテーブル名 (例: users)
        public string ToColumn { get; set; } = string.Empty;   // 相手（参照先）のカラム名 (例: id)
    }

    // 変換後のテーブル情報クラス
    public class ConvertedTableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public List<ConvertedColumnInfo> Columns { get; set; } = new();

        // ★ 追加：このテーブルが持つ外部キーリスト
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
