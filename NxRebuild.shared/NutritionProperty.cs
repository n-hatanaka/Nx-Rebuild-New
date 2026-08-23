using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared
{
    // 非ジェネリックにして BaseDataObj<int> を継承し、抽象メンバーをオーバーライドする
    public class NutritionProperty : BaseDataObj<int>, IBaseDataObj<int>
    {
        public bool Visible
        {
            get => _rawData.TryGetValue("Visible", out var v) && v is bool b ? b : false;
            set
            {
                _rawData["Visible"] = value;
                // プロパティ側も同期したいならここで SetPropertys 呼んでもよい
            }
        }
       public bool Nutrition
       {
           get => _rawData.ContainsKey("Nutrition") && Convert.ToBoolean(_rawData["Nutrition"]);
       }

        protected override void Initialize()
        {
            _tblName = "ColName";
            _nameColName = "Name";
            _idColName = "No";
        }
    
        protected override string CreateJSONsql()
        {
            return $@"SELECT t.* FROM {_tblName} t
                      WHERE t.{_idColName} = @dataID AND t.tenant_code = @tenantCode";
        }
    
        public override Task<LockStatus> DataOpen()
        {
            throw new NotImplementedException();
        }
    
        public override Task<bool> DeleteQueryExec(IDbTransaction transaction)
        {
            throw new NotImplementedException();
        }
    
        public override Task<bool> SoftDeleteQueryExec(IDbTransaction transaction)
        {
            throw new NotImplementedException();
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
    
        public override async Task<bool> SaveQueryExec(IDbTransaction transaction)
        {
            // Visible だけ更新する世界線でもいいし、
            // _rawData 全体を使って UPDATE してもいい
            var sql = $@"
                UPDATE {_tblName}
                SET Visible = @Visible
                WHERE {_idColName} = @DataID
                  AND tenant_code = @TenantCode;
            ";
    
            var rows = await DBcon.ExecuteAsync(sql, new {
                Visible = this.Visible,
                DataID = this.DataID,
                TenantCode = this.TenantCode
            }, transaction);
    
            return rows == 1;
        }

    }
}
