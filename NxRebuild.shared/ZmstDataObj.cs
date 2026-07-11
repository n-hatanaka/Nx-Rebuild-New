using Dapper;
using Npgsql.Replication.PgOutput.Messages;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Transactions;

namespace NxRebuild.shared {

    public class ColNameRecord {
        public Guid Group_ID { get; set; }
        public int No { get; set; }
        public int SortNo { get; set; }
        public string Col { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public int Digit { get; set; }
        public bool Visible { get; set; }
        public bool Nutrition { get; set; }
        public string PName1 { get; set; }
        public string PName2 { get; set; }
        public string PName3 { get; set; }
        public string PTanni { get; set; }
    }

    public class ZmstDataObj:BaseDataObj<int> {
            
        public string Z_code 
        { 
            get => (string)_rawData["Z_code"]; 
            set => _rawData["Z_code"] = value; 
        }
    
     // 書き込み・読み込みを禁止するカラムのリスト
         protected HashSet<string> _restrictedColumns = new();
     
         // 禁止リストへの追加（Initializeやコンストラクタで呼ぶ想定）
         protected void AddRestrictedColumn(string colName) => _restrictedColumns.Add(colName);
     
         public float GetNutrient(string colName)
         {
             // 禁止されているキーなら例外を投げる
             if (_restrictedColumns.Contains(colName))
                 throw new UnauthorizedAccessException($"Invalid: カラム '{colName}' への読み込みは許可されていません。");
     
             if (!_rawData.ContainsKey(colName))
                 return 0.0f; // または例外
     
             return Convert.ToSingle(_rawData[colName]);
         }
     
         public void SetNutrient(string colName, float value)
         {
             // 禁止されているキーなら例外を投げる
             if (_restrictedColumns.Contains(colName))
                 throw new UnauthorizedAccessException($"Invalid: カラム '{colName}' への書き込みは許可されていません。");
     
             _rawData[colName] = value;
         }

        protected override void Initialize() {
            _idColName = "LocalCode";
            _nameColName = "Z_name";
            _tblName = "Zmst";
            _datatype = NxDataType.Zairyou;
            AddRestrictedColumn(_idColName);
            AddRestrictedColumn(_nameColName);
            
            AddRestrictedColumn("Z_code");
            AddRestrictedColumn("LocalCode");
            AddRestrictedColumn("update_at");
            AddRestrictedColumn("locked_by");
            AddRestrictedColumn("locked_at");
        }


        public override async Task<LockStatus> DataOpen() {

            // データを開くためのSQLを実行
            string sql = "SELECT * FROM Zmst WHERE id = @id AND group_code = @tenantCode";
            var result = await DBcon.QueryFirstOrDefaultAsync(sql, new { id = DataID, tenantCode = TenantCode });
            SetPropertys(result);

            return LockStatus;
        }

        public override async Task<bool> DeleteQueryExec(IDbTransaction transaction) {

            // 削除用のSQLを実行
            string sql = "DELETE FROM Zmst WHERE id = @id AND group_code = @tenantCode";
            return await DBcon.ExecuteAsync(sql, new { id = DataID , tenantCode = TenantCode },transaction) > 0;
        }


        //注）クライアントの処理でのみ使うメソッド
        public override async Task<bool> SaveAsync() {

            IDbTransaction transaction = DBcon.BeginTransaction();
            try {
                bool result = await DeleteQueryExec(transaction);
                if (!result)
                    result = await SaveQueryExec(transaction);
                if (!result) {
                    transaction.Commit();
                    return true;
                } else
                    return false;
            } catch (Exception ex) {
                transaction.Rollback();
                return false;
            }
        }

        public override async Task<bool> SaveQueryExec(IDbTransaction transaction) {

            // データ保存用のSQLを実行
            var columns = this._rawData.Keys.Select(key => $"\"{key}\"");
            var columnList = string.Join(", ", columns);
            var values = string.Join(", ", _rawData.Keys.Select(key => "@" + key));
            string sql = $"INSERT INTO Zmst ({columnList}) VALUES ({values})";
            return await DBcon.ExecuteAsync(sql, _rawData, transaction) > 0;
        }

        public override async Task<string> TbltoJson() {

            // テーブルデータをJSON形式で取得
            string sql = "SELECT * FROM Zmst WHERE id = @id AND group_code = @tenantCode";
            var result = await DBcon.QueryFirstOrDefaultAsync(sql, new { id = DataID, tenantCode = TenantCode });
            return JsonSerializer.Serialize(result);
        }

   }

}
