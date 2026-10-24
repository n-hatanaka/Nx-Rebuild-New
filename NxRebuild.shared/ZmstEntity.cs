using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {

    // ---------------------------------------------------------
    // CategoryEntity
    // 食品群マスタ（gun_m）を表すエンティティ
    // ZmstEntityMgr のデータリストに混在して格納されるため、ZmstEntity を継承する
    // ---------------------------------------------------------
    public class CategoryEntity : ZmstEntity {
        public CategoryEntity() {
            _primaryTableName = "gun_m";
            _idColName = "syou_cd";
            _nameColName = "syou_name";
            _parentIDColName = "dai_cd";
            _datatype = NxDataType.Folder;   // UIでフォルダ扱い
        }

        public int jun {
            get {
                if (_rawData.TryGetValue("jun", out var v) && v != null)
                    return Convert.ToInt32(v);
                return 0;
            }
        }


        // ---------------------------------------------------------
        // ★ 名前変更禁止：常に false
        // ---------------------------------------------------------
        public override async Task<NxOperationResult<int>> ReNameAsync(string newName) {
            return new NxOperationResult<int> {
                OperationErrorCode = NxOperationErrorCode.None,
                ValidationErrorCode = NxValidationErrorCode.None
            };
        }

        // ---------------------------------------------------------
        // ★ 保存禁止：常に false
        // ---------------------------------------------------------
        public override async Task<NxOperationResult<int>> SaveAsync(
            Dictionary<string, object?> workingRaw,
            List<(string, List<Dictionary<string, object?>>)>? subTables = null) {
            return new NxOperationResult<int> {
                OperationErrorCode = NxOperationErrorCode.None,
                ValidationErrorCode = NxValidationErrorCode.None
            };
        }


        // ---------------------------------------------------------
        // ★ 削除禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> _DeleteQueryExecAsync(IDbTransaction tran, bool softDelete = false) {          
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // ★ 論理削除禁止：常に false
        // ---------------------------------------------------------
        //protected override Task<bool> _SoftDeleteQueryExecAsync(IDbTransaction tran) {
        //    return Task.FromResult(false);
        //}

        // ---------------------------------------------------------
        // JSON生成SQLは使わないので空でOK
        // ---------------------------------------------------------
        protected override IEnumerable<(string tableName, string sql)> CreateJSONsql() {
            yield return ("","");                
        }
    }



    //IZmstEntity インターフェース
    public interface IZmstEntity : IBaseDataObj<int> {
        List<Dictionary<string, object?>> TanList { get; }
        decimal? GetNutritionValue(string col);
        void SetNutritionValue(string col, decimal? value);


    }


    [NxObjsMetaData(
        PrimaryTable = "Zmst",
        PriTblPKColList = new[] { "tenant_code", "LocalCode" },

        DetailTableList = new[] { "tan_m" },
        DetailTblPKJson = @"{
            ""tan_m"": [""tenant_code"", ""LocalCode"", ""tan_cd""]
        }",

        NameCol = "Z_name",
        IdCol = "LocalCode",
        ParentIDCol = "gun_cd",
        WorkPriTable = "Zmst",
        WorkDtlTblList = new[] { "tan_m" },
        InfoTable = "",
        DataType = NxDataType.Zairyou,
        RootName = "食品分類"
    )]
    public class ZmstEntity : BaseDataObj<int>, IZmstEntity {
        public ZmstEntity() : base() { }



        public List<Dictionary<string, object?>> TanList {
            get {
                // サブテーブルがまだ無い場合は作る
                if (SubTables.Count == 0)
                    SubTables.Add(("tan_m", new List<Dictionary<string, object?>>()));

                return SubTables[0].Rows;
            }
        }


        public decimal? GetNutritionValue(string col) {
            if (_rawData.TryGetValue(col, out var v))
                return v == null ? null : Convert.ToDecimal(v);
            return null;
        }

        public void SetNutritionValue(string col, decimal? value) {
            _rawData[col] = value;
        }



        // ---------------------------------------------------------
        // Zmst固有
        // メモリ上にサブテーブル（単位リスト）を持つためロード後、再読み込みの実装にする
        // ---------------------------------------------------------
        public override async Task<bool> JsonToTblAsync(string json, IDbTransaction tran) {
            var result = await base.JsonToTblAsync(json, tran);
            
            if (!result)
                return result;

            await _LoadDetailTablesFromRawAsync();
            return result;
        }



        


        // ---------------------------------------------------------
        // ソフトデリート（Zmst のみ）単位マスタは消してはいけないので放置
        // 材料マスタはフラグで削除（無効化）を管理する
        // ---------------------------------------------------------
        protected override async Task<bool> _SoftDeletePrimaryQueryExecAsync(IDbTransaction transaction) {
            try {               

                string sql = $@"
                    UPDATE ""{_primaryTableName}""
                    SET ""deleted"" = 1,
                        ""Update_at"" = @UpdateAt
                    WHERE ""{_idColName}"" = @DataID
                      AND ""tenant_code"" = @TenantCode;
                ";

                await DBcon.ExecuteAsync(sql, new {
                    DataID = this.DataID,
                    TenantCode = this.TenantCode,
                    UpdateAt = DateTime.UtcNow
                }, transaction);

                return true;
            } catch {
                return false;
            }
        }

        // ---------------------------------------------------------
        // ID生成（LocalCode の新規採番）
        // 
        // ---------------------------------------------------------
        public int GenerateDataID(IDbTransaction tran) {
            bool isSQLite = DBcon.ConnectionString.Contains("mode=memory", StringComparison.OrdinalIgnoreCase);

            if (isSQLite) {
                // SQLite は FOR UPDATE が無いので MAX+1 でOK
                string sql = $@"
                                SELECT COALESCE(MAX(""LocalCode""), 0) + 1
                                FROM ""{_primaryTableName}""
                                WHERE ""tenant_code"" = @TenantCode;
                            ";
                return DBcon.ExecuteScalar<int>(sql, new { TenantCode }, tran);
            }

            // PostgreSQL / MySQL / SQLServer
            string sql2 = $@"
                            SELECT ""LocalCode""
                            FROM ""{_primaryTableName}""
                            WHERE ""tenant_code"" = @TenantCode
                            ORDER BY ""LocalCode"" DESC
                            LIMIT 1
                            FOR UPDATE;
                        ";

            int? last = DBcon.ExecuteScalar<int?>(sql2, new { TenantCode }, tran);
            return (last ?? 0) + 1;
        }



        public override int EnsureIDForSave(IDbTransaction tran){
            if (this.DataID == 0)
            {
                // Zmst は保存時採番（新規レコード）
                int newId = GenerateDataID(tran);
                return newId;
            }
            else
            {
                // 既存レコード（採番不要）
                return this.DataID;
            }
        }

        //材料マスタ専用のバリデータ
        protected override NxValidator<int> CreateValidator(IBaseDataObj<int> IBaseDataObj, Dictionary<string, object> ValidData) {
            return new ZmstValidator(this, _rawData);
        }

    }
}
