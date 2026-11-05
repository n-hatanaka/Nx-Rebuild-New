using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {

    [NxObjsMetaData(
        PrimaryTable = "Ry_lib",
        PriTblPKColList = new[] { "tenant_code", "Ry_Code" },

        DetailTableList = new[] { "Ry_lib_s" },
        DetailTblPKJson = @"{
            ""Ry_lib_s"": [""id"", ""tenant_code"", ""Ry_Code""]
        }",

        NameCol = "Name",
        IdCol = "Ry_Code",
        UpdateAtCol = "Update_at",
        LockedByCol = "locked_by",
        LockedAtCol = "locked_at",
        LockTimeOutSec = 600,
        TenantCodeCol = "tenant_code",
        ParentIDCol = "Parent",
        WorkPriTable = "w_Ry_Lib",
        WorkDtlTblList = new[] { "w_Ry_Lib_s" },
        InfoTable = "",
        DataType = NxDataType.Ryouri,
        RootName = "料理ライブラリ"
    )]
    public class RyLibEntity : BaseDataObj<Guid> {
        public RyLibEntity() : base() { }

        // サブテーブル（Ry_lib_s：料理内材料など）へのアクセサ
        public List<Dictionary<string, object?>> RyLibSList {
            get {
                if (SubTables.Count == 0)
                    SubTables.Add(("Ry_lib_s", new List<Dictionary<string, object?>>()));

                return SubTables[0].Rows;
            }
        }

        // フォルダか献立/料理かの判定プロパティ
        public int Type {
            get {
                if (_rawData.TryGetValue("Type", out var v) && v != null)
                    return Convert.ToInt32(v);
                return 0; // 0: 献立/料理, 1: フォルダ
            }
            set => _SetProperties(new Dictionary<string, object> { ["Type"] = value });
        }

        public decimal Ninzu {
            get {
                if (_rawData.TryGetValue("Ninzu", out var v) && v != null)
                    return Convert.ToDecimal(v);
                return 1.0m;
            }
            set => _SetProperties(new Dictionary<string, object> { ["Ninzu"] = value });
        }

        public string? RyNote {
            get => _rawData.TryGetValue("Ry_note", out var v) ? v?.ToString() : null;
            set => _SetProperties(new Dictionary<string, object> { ["Ry_note"] = value ?? string.Empty });
        }

        // JSON復元時にサブテーブルも再読み込みする
        public override async Task<bool> JsonToTblAsync(string json, IDbTransaction tran) {
            var result = await base.JsonToTblAsync(json, tran);
            
            if (!result)
                return result;

            await _LoadDetailTablesFromRawAsync();
            return result;
        }

        // サブテーブル行の親キー補完
        protected override void CompleteDetailRow(
            string tableName, Dictionary<string, object?> row, Dictionary<string, object?> mainWorkingRaw) {
            if (tableName == "Ry_lib_s") {
                row["tenant_code"] = mainWorkingRaw["tenant_code"];
                row["Ry_Code"] = mainWorkingRaw["Ry_Code"];
            }
        }

    }
}