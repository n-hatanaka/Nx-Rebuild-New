using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NxRebuild.shared {
    // ---------------------------------------------------------
    // エンティティ毎の具象クラスに付与するメタデータ属性
    // BaseDataObjの具象クラスの先頭に記述することで、
    //   BaseDataObjMgr
    //   NxDataController
    //  へ自動的に設定される。
    //
    //記述例(ZmstEntity.cs）
    // [NxObjsMetaData(
    // PrimaryTable = "Zmst",
    // NameCol = "Z_name",
    // IdCol = "LocalCode",
    // ParentIDCol = "gun_cd",
    // DetailTables = "tan_m",
    // InfoTable = "",
    // WorkPriTable = "Zmst",
    // WorkDtlTable = "tan_m",
    // DataType = NxDataType.Zairyou,
    // RootName = "食品分類")]

    [AttributeUsage(AttributeTargets.Class)]
    public class NxObjsMetaDataAttribute : Attribute {
        // Primary（正本）テーブル
        public string PrimaryTable { get; set; }

        // Primary の PK（複合キー対応）
        public string[] PriTblPKColList { get; set; } = Array.Empty<string>();

        // Detail（サブ）テーブルは複数あり得る
        public string[] DetailTableList { get; set; } = Array.Empty<string>();

        // Detail の PK（複数テーブル → 複数 PK セット）
        // ★ JSON 文字列で PK を渡す
        // 例: { "tan_m": ["tenant_code","LocalCode","tan_cd"] }
        public string DetailTblPKJson { get; set; } = "{}";

        // その他メタ情報
        public string NameCol { get; set; }
        public string IdCol { get; set; }
        public string ParentIDCol { get; set; }
        public string RootName { get; set; }
        public string WorkPriTable { get; set; }
        public string[] WorkDtlTblList { get; set; } = Array.Empty<string>();
        public string InfoTable { get; set; }
        public NxDataType DataType { get; set; }

    }


    public static class NxObjsMetaReader {
        public static NxObjsMetaDataAttribute Read(Type type) {
            return type.GetCustomAttribute<NxObjsMetaDataAttribute>();
        }
    }

}
