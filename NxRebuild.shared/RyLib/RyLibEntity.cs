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
        DataTypeCol = "Type",
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

    }
}