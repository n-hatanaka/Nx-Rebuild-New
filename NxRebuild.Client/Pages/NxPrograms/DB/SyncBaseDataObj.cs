using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {

    public interface ISyncBaseDataObj<TKey>: IBaseDataObj<TKey> {
        HttpClient Http { get; set; }
        CustomAuthStateProvider Auth { get; set; }
        Task<LockStatus> SetLockAsync(LockStatus lockStatus);
        Task<bool>       JsonToTbl(string json);
        string           TblToJson();
    }


    //DataObjのラッパークラス。<br/>サーバーとの同期機能を付与する。
    //UIからはインターフェース経由でaccessさせる。
    //オブジェクトの作成と削除はマネージャークラスから行う。
    public abstract class SyncBaseDataObj<TKey> : ISyncBaseDataObj<TKey> {
        protected readonly BaseDataObj<TKey> _dataObj;

        public HttpClient Http { get; set; }
        public CustomAuthStateProvider Auth {  get; set; }
        public abstract string ApiRoute { get; } //←派生先で設定する事。
        public Guid CurrUsrID{ get => _dataObj.CurrUsrID; 
                                set => _dataObj.CurrUsrID = value; }
        public SyncBaseDataObj(BaseDataObj<TKey> dataObj) {
            _dataObj = dataObj ?? throw new ArgumentNullException(nameof(dataObj));
        }

        public IDbConnection DBcon {
            get => _dataObj.DBcon;
            set => _dataObj.DBcon = value;
        }

        public IBaseDataObjMgr<BaseDataObj<TKey>,TKey> SelfObjMgr { 
            get => _dataObj.SelfObjMgr;
            set => _dataObj.SelfObjMgr = value;
        }
        // DataObjのメソッドへのアクセスラッパー
        public Task<LockStatus> SetLockAsync(LockStatus lockStatus) => _dataObj.SetLockAsync(lockStatus);
        public string TblToJson() => _dataObj.TblToJson();

        public Task<bool> JsonToTbl(string json) => _dataObj.JsonToTbl(json);


        public Task<bool> SaveAsync() => _dataObj.SaveAsync();

        public TKey DataID {
            get => _dataObj.DataID;
            set => _dataObj.DataID = value;
        }

        public string DataName {

            get => _dataObj.DataName;
        }
        public NxDataType DataType => _dataObj.DataType;
        public DateTime Update_at {
            get => _dataObj.Update_at;
        }
        public Guid LockerID {
            get => _dataObj.LockerID;
        }
        public DateTime LockedAt {
            get => _dataObj.LockedAt;
        }


        public Guid TenantCode {
            get => _dataObj.TenantCode;
            set => _dataObj.TenantCode = value;
        }

        public Dictionary<string, object> _rawData {
            get => _dataObj._rawData;
            set => _dataObj._rawData = value;
        }

        public string NameColName => _dataObj.NameColName;
        public string IdColName => _dataObj.IdColName;
        public string TblName => _dataObj.TblName;
        public string S_TblName => _dataObj.S_TblName;
        public string InfoTbl => _dataObj.InfoTbl;
        public string W_TblName => _dataObj.W_TblName;
        public string Ws_TblName => _dataObj.Ws_TblName;

        public virtual async Task UpdatePropertys() => await _dataObj.UpdatePropertys();

        public async Task<LockStatus> DataOpen() => await _dataObj.DataOpen();


        public virtual async Task<bool> ReName(string newName) {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            // 2. トランザクション開始（Base世界線）
            using IDbTransaction transaction = DBcon.BeginTransaction();

            // 3. ローカル（Base世界線）で名前変更を試みる
            var localUpdated = await _dataObj.ReNameQueryExec(newName, transaction);
            if (!localUpdated) {
                transaction.Rollback();
                return false;
            }

            // 4. Sync世界線 → API呼び出し
            var url = $"{ApiRoute}/ReName/{DataID}/{newName}";
            HttpResponseMessage response;

            try {
                response = await Http.PostAsync(url, null);
            } catch {
                // API通信失敗 → Base世界線をロールバック
                transaction.Rollback();
                return false;
            }

            if (!response.IsSuccessStatusCode) {
                // API側で失敗 → Base世界線をロールバック
                transaction.Rollback();
                return false;
            }

            // 5. APIが返す JSON（正本世界線）を取得
            var json = await response.Content.ReadAsStringAsync();

            // 6. JSONをローカルDBに反映（Base世界線の更新）
            //    ※ _rawData は BaseDataObj の生データ
            try {
                var updatedRaw = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                // サーバーからメタデータを受け取りクライアントのテーブルをを更新
                UpdateRawData(updatedRaw, transaction);
                //保持しているプロパティも更新
                UpdatePropertys();
            } catch {
                transaction.Rollback();
                return false;
            }

            // 7. 世界線を閉じる（Base世界線を確定）
            transaction.Commit();

            return true;
        }

        //何かしらの更新をした時にサーバーから返ってきたメタデータ（TblName）を更新する際に使用する。
        //例えばRenameとか
        public bool UpdateRawData(Dictionary<string, object> raw, IDbTransaction transaction) {
            try {
                // 1. カラム名と値を準備（IDColNameとtenant_code は SET に含めない）
                //raw（Dictionary）に入っている全カラムのうち、
                //主キー（IdColName）とテナントキー（tenant_code）を除外して
                //UPDATE の SET に使うカラムだけを抽出する。
                //要はこれは「SET に入れていいカラムだけを抽出している」
                var columns = raw.Keys.Where(k => k != IdColName && k != "tenant_code");
                var setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));

                // 2. UPDATE 実行（世界線の一意性を保証）
                var sql =
                        $"UPDATE {TblName} SET {setClause} " +
                        $"WHERE {IdColName} = @DataID AND tenant_code = @TenantCode";


                DBcon.Execute(sql, raw, transaction);

                // 3. インメモリの RawData を更新（正本世界線の吸収）
                _rawData = raw;

                return true;
            } catch {
                return false;
            }
        }





        public void SetPropertys(Dictionary<string, object> record) {
            _dataObj.SetPropertys(record);
        }

    }
}
