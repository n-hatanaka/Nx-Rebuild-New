using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NxRebuild.shared {

    public interface ISyncBaseDataObj<TKey>: IBaseDataObj<TKey> {

        Task<LockStatus> SetLockAsync(LockStatus lockStatus)
        Task<bool>       SaveJsonData(string json);
        string           LoadDataAsJson();
    }


    //DataObjのラッパークラス。<br/>サーバーとの同期機能を付与する。
    //UIからはインターフェース経由でaccessさせる。
    //オブジェクトの作成と削除はマネージャークラスから行う。
    public class SyncBaseDataObj<TKey> : ISyncBaseDataObj<TKey> {
        protected readonly BaseDataObj<TKey> _dataObj;

        public Guid CurrUsrID{ get => _dataObj.CurrUsrID; 
                                set => _dataObj.CurrUsrID = value; }
        public SyncBaseDataObj(BaseDataObj<TKey> dataObj) {
            _dataObj = dataObj ?? throw new ArgumentNullException(nameof(dataObj));
        }

        public IDbConnection DBcon {
            get => _dataObj.DBcon;
            set => _dataObj.DBcon = value;
        }

        public IBaseDataObjMgr SelfObjMgr { 
            get => _dataObj.SelfObjMgr;
            set => _dataObj.SelfObjMgr = value;
        }
        // DataObjのメソッドへのアクセスラッパー
        public Task<LockStatus> SetLockAsync(LockStatus lockStatus) => _dataObj.SetLockAsync(lockStatus);
        public string LoadDataAsJson() => _dataObj.LoadDataAsJson();
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


        public string TenantCode {
            get => _dataObj.TenantCode;
            set => _dataObj.TenantCode = value;
        }

        public string NameColName => _dataObj.NameColName;
        public string IdColName => _dataObj.IdColName;
        public string TblName => _dataObj.TblName;
        public string S_TblName => _dataObj.S_TblName;
        public string InfoTbl => _dataObj.InfoTbl;
        public string W_TblName => _dataObj.W_TblName;
        public string Ws_TblName => _dataObj.Ws_TblName;

        public async Task<LockStatus> DataOpen() => await _dataObj.DataOpen();

   
        public virtual async Task<bool> ReName(string newName) {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            IDbTransaction transaction = DBcon.BeginTransaction();
            // 2.インンメモリへの保存を実行してみる
            if (!await _dataObj.ReNameQueryExec(newName, transaction))
                transaction.Dispose();
                return false;

            // 4.書き込み成功したら
            //httpリクエストでリネーム
            //アップデートタイム食い違うがサーバー側が新しくなるので気にしない。
            //あとで勝手に更新される

            //失敗したらロールバックする
            if (!false) {
                transaction.Rollback();
                return false;
            }

            transaction.Commit();
            return true;
        }


        public Task<bool> SaveJsonData(string json) {
            return _dataObj.SaveJsonData(json);
        }


        public void SetPropertys(Dictionary<string, object> record) {
            _dataObj).SetPropertys(record);
        }

    }
}
