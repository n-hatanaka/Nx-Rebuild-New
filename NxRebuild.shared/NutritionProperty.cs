using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Threading.Tasks;

namespace NxRebuild.shared
{
    // 非ジェネリックにして BaseDataObj<int> を継承し、抽象メンバーをオーバーライドする
    public class NutritionProperty : BaseDataObj<int>, IBaseDataObj<int>
    {
        protected override void Initialize()
        {
            // 初期化ロジックをここに実装
            _tblName = "ColName";
            _nameColName = "Name";
            _idColName = "No";

        }

        protected override string CreateJSONsql()
        {
            // JSON 作成 SQL を返す実装をここに
            return $@"SELECT t.* FROM {_tblName} t
                        WHERE t.{_idColName} = @dataID AND t.tenant_code = @tenantCode";
        }

        public override Task<LockStatus> DataOpen()
        {
            // ロックなどは行わないので無効化
            throw new NotImplementedException();
        }

        public override Task<bool> DeleteQueryExec(IDbTransaction transaction)
        {
            // 削除は行わないので無効化
            throw new NotImplementedException();
        }

        public override Task<bool> SoftDeleteQueryExec(IDbTransaction transaction)
        {
            // 削除は行わないので無効化
            throw new NotImplementedException();
        }

        public override Task<bool> SaveAsync()
        {
            // 
            throw new NotImplementedException();
        }

        public override Task<bool> SaveQueryExec(IDbTransaction transaction)
        {
            // 保存トランザクション処理をここに
            throw new NotImplementedException();
        }
    }
}
