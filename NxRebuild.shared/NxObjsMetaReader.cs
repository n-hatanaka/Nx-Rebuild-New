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
    // DetailTable = "tan_m",
    // InfoTable = "",
    // WorkPriTable = "Zmst",
    // WorkDtlTable = "tan_m",
    // DataType = NxDataType.Zairyou,
    // RootName = "食品分類")]

    [AttributeUsage(AttributeTargets.Class)]
    public class NxObjsMetaDataAttribute : Attribute {
        public string PrimaryTable { get; set; }
        public string NameCol { get; set; }
        public string IdCol { get; set; }
        public string ParentIDCol { get; set; }
        public string DetailTable { get; set; }
        public string RootName { get; set; }
        public string WorkPriTable { get; set; }
        public string WorkDtlTable { get; set; }
        public string InfoTable { get; set; }
        public NxDataType DataType { get; set; }
    }

    public static class NxObjsMetaReader {
        public static NxObjsMetaDataAttribute Read(Type type) {
            return type.GetCustomAttribute<NxObjsMetaDataAttribute>();
        }
    }

}
