using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared
{
    //栄養素のプロパティを表すクラス（更新対象のカラムはVisibleだけ。排他制御は行わないので少し特殊）最初の具象実装にうってつけ
    // 非ジェネリックにして BaseDataObj<int> を継承し、抽象メンバーをオーバーライドする
    public class NutritionProperty : BaseDataObj<int>, IBaseDataObj<int>
    {
        public bool Visible
        {
            get => _rawData.TryGetValue("Visible", out var v) && v is bool b ? b : false;
            set
            {
                _rawData["Visible"] = value;
                // プロパティ側も同期したいならここで SetProperties 呼んでもよい
            }
        }
       public bool Nutrition
       {
           get => _rawData.ContainsKey("Nutrition") && Convert.ToBoolean(_rawData["Nutrition"]);
       }

        public NutritionProperty() {
            _tblName = "ColName";
            _nameColName = "Name";
            _idColName = "No";
        }
    
        protected override string CreateJSONsql()
        {
            return $@"SELECT t.* FROM ""{_tblName}"" t
                      WHERE t.""{_idColName}"" = @dataID AND t.""tenant_code"" = @tenantCode";
        }
    
        public override async Task<LockStatus> DataOpen()
        {
            throw new NotImplementedException();
        }
    
        public override async Task<bool> DeleteQueryExec(IDbTransaction transaction)
        {
            // No-op: 同期時にクライアント側で削除処理を行わない設計のため、何もしないで成功を返す
            await Task.CompletedTask;
            return true;
        }
    
        public override async Task<bool> SoftDeleteQueryExec(IDbTransaction transaction)
        {
            // No-op: ソフトデリートを使わないので何もしないで成功を返す
            await Task.CompletedTask;
            return true;
        }
    
        public override async Task<bool> SaveAsync()
        {
            using var tran = DBcon.BeginTransaction();
            try
            {
                var ok = await SaveQueryExec(tran);
                if (!ok)
                {
                    tran.Rollback();
                    return false;
                }
    
                tran.Commit();
                return true;
            }
            catch
            {
                tran.Rollback();
                return false;
            }
        }

        public override async Task<bool> SaveQueryExec(IDbTransaction transaction) {
            try {
                var sql = $@"
                                UPDATE ""{_tblName}""
                                SET ""Visible"" = @Visible
                                WHERE ""{_idColName}"" = @DataID
                                  AND ""tenant_code"" = @TenantCode;
                            ";

                var rows = await DBcon.ExecuteAsync(sql, new {
                                                                Visible = this.Visible,
                                                                DataID = this.DataID,
                                                                TenantCode = this.TenantCode
                                                            }, transaction);
                //正常に更新された件数が 0 または 1 件であれば正常終了とみなす
                //0件は、既に同じ値が設定されていた場合に発生する可能性があるため、正常とみなす
                //0件は、このオブジェクトが存在する時点で対象レコードの存在は確定しているため単なる更新被り
                //1件は、通常の更新処理で発生する件数であるため、正常とみなす
                //2件以上更新されることは想定されないため、異常とみなす
                if (rows <= 1) {
                    Console.WriteLine($"[NutritionProperty] SaveQueryExec OK: DataID={DataID}, Visible={Visible}");
                    return true;
                }

                // まさかの 2 件以上更新 → 異常事態
                Console.WriteLine($"[NutritionProperty] SaveQueryExec ERROR: {rows} rows updated! WORLDLINE BROKEN (DataID={DataID})");
                return false;

            } catch (Exception ex) {
                Console.WriteLine($"[NutritionProperty] SaveQueryExec Exception: {ex.Message}");
                return false;
            }
        }



    }
}
