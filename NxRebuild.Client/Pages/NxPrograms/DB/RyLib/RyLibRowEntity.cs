using System.Data;
using Dapper;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.DB.RyLib {

    public interface IRyLibRowEntity : IBaseDataObj<int> {
        Dictionary<string, object?> RawData { get; }
        int SortNo { get; set; }
        string ZCode { get; set; }
        int LocalCode { get; set; }
        float Syoumi { get; set; }
        float Junbi { get; set; }
        int TanCd { get; set; }
    }

    // ※ NxObjsMetaData は AttributeUsage が Class 限定のため、インターフェースではなくクラスに付ける
    [NxObjsMetaData(
        PrimaryTable = "w_Ry_Lib_s",
        PriTblPKColList = new[] { "id", "tenant_code", "Ry_Code" },
        DetailTblPKJson = "{}",

        NameCol = "Z_code",
        IdCol = "id",

        UpdateAtCol = "",
        LockedByCol = "",
        LockedAtCol = "",

        TenantCodeCol = "tenant_code",
        ParentIDCol = "",

        // 正本テーブルと同じ = ワークテーブルを介さず w_Ry_Lib_s を直接CRUDする
        WorkPriTable = "w_Ry_Lib_s",

        InfoTable = "",

        DataType = NxDataType.Ryouri
    )]
    public class RyLibRowEntity : BaseDataObj<int>, IRyLibRowEntity {

        private static readonly string[] SaveCols = {
            "id", "tenant_code", "Ry_Code", "sort_no", "Z_code", "LocalCode", "Syoumi", "Junbi", "tan_cd"
        };
        private static readonly string[] PkCols = { "id", "tenant_code", "Ry_Code" };

        public Dictionary<string, object?> RawData => _rawData!;

        public int SortNo {
            get => _rawData.TryGetValue("sort_no", out var v) ? Convert.ToInt32(v) : 0;
            set => _SetProperties(new Dictionary<string, object?> { ["sort_no"] = value });
        }

        public string ZCode {
            get => _rawData.TryGetValue("Z_code", out var v) ? v?.ToString() ?? "" : "";
            set => _SetProperties(new Dictionary<string, object?> { ["Z_code"] = value });
        }

        public int LocalCode {
            get => _rawData.TryGetValue("LocalCode", out var v) ? Convert.ToInt32(v) : 0;
            set => _SetProperties(new Dictionary<string, object?> { ["LocalCode"] = value });
        }

        public float Syoumi {
            get => _rawData.TryGetValue("Syoumi", out var v) ? Convert.ToSingle(v) : 0;
            set => _SetProperties(new Dictionary<string, object?> { ["Syoumi"] = value });
        }

        public float Junbi {
            get => _rawData.TryGetValue("Junbi", out var v) ? Convert.ToSingle(v) : 0;
            set => _SetProperties(new Dictionary<string, object?> { ["Junbi"] = value });
        }

        public int TanCd {
            get => _rawData.TryGetValue("tan_cd", out var v) ? Convert.ToInt32(v) : 0;
            set => _SetProperties(new Dictionary<string, object?> { ["tan_cd"] = value });
        }


        // ---------------------------------------------------------
        // 保存: workingRaw を w_Ry_Lib_s へ直接 Upsert する
        // 基底の SaveAsync は UpdateAtCol(空)へ書き込もうとして失敗するため、この行用に置き換える。
        // id は新規(0以下)のとき、保存時に MAX(id)+1 で採番する。
        // ---------------------------------------------------------
        public override async Task<NxOperationResult<int>> SaveAsync(
            Dictionary<string, object?> workingRaw,
            List<(string, List<Dictionary<string, object?>>)>? subTables = null) {

            using var tran = DBcon.BeginTransaction();
            try {
                if (!workingRaw.TryGetValue("Ry_Code", out var ry) || ry == null)
                    throw new Exception("Ry_Code が未設定です。");

                if (DataID <= 0) {
                    DataID = await DBcon.ExecuteScalarAsync<int>(
                        $@"SELECT COALESCE(MAX(""id""), 0) + 1 FROM ""{_primaryTableName}"" WHERE ""tenant_code"" = @tc;",
                        new { tc = TenantCode }, tran);
                }

                workingRaw["id"] = DataID;
                workingRaw["tenant_code"] = TenantCode;

                var param = new Dictionary<string, object?>();
                foreach (var c in SaveCols)
                    param[c] = workingRaw.TryGetValue(c, out var v) ? v : null;

                var cols = string.Join(", ", SaveCols.Select(c => $@"""{c}"""));
                var vals = string.Join(", ", SaveCols.Select(c => "@" + c));
                var updates = string.Join(", ",
                    SaveCols.Where(c => !PkCols.Contains(c)).Select(c => $@"""{c}"" = EXCLUDED.""{c}"""));
                var pk = string.Join(", ", PkCols.Select(c => $@"""{c}"""));

                string sql = $@"
                    INSERT INTO ""{_primaryTableName}"" ({cols})
                    VALUES ({vals})
                    ON CONFLICT ({pk})
                    DO UPDATE SET {updates};";

                var affected = await DBcon.ExecuteAsync(sql, param, tran);
                if (affected != 1)
                    throw new Exception($"UPSERT affected {affected} rows");

                tran.Commit();

                // 正本(_rawData)を保存内容で更新
                _SetProperties(workingRaw);

                return new NxOperationResult<int> {
                    RawData = _rawData,
                    Message = "Save completed"
                };
            } catch (Exception ex) {
                tran.Rollback();
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"SaveAsync failed: {ex.Message}"
                };
            }
        }
    }
}