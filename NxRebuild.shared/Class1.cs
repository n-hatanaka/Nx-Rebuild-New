
using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    /// <summary>
    /// データオブジェクトのインターフェースです。
    /// </summary>
    public interface IDataObj<Key> {

        IDbConnection DBCon { get; }

        string LoadDataAsJson();
        Task<bool> SaveAsync();
        TKey DataID { get; }
        string DataName { get; set; }
        NxDataType DataType { get; }
        DateTime Update_at { get; set; }
        Guid LockerID { get; set; }
        DateTime LockedAt { get; set; }
        string TenantCode { get; set; }

        string NameColName { get; }
        string IdColName { get; }
        string TblName { get; }
        string S_TblName { get; }
        string InfoTbl { get; }
        string W_TblName { get; }
        string Ws_TblName { get; }

        Task<LockStatus> DataOpen();
        Task<bool> ReName(string newName);
        static Task<bool> ReNameQueryExec(string newName);
        Task<LockStatus> SetLockAsync(LockStatus lockStatus);

        protected virtual async Task<bool> ValidateNewName(string newName);
        private protected virtual async Task CheckLockStatusAndExecuteRename(string newName);
        protected virtual async Task<(bool, string)> TryExecuteRenameQuery(string newName);
        protected virtual async Task UpdateDataPropertiesAfterRenaming();

        protected virtual LockResult CheckExistingLockAndReturnStatus(LockStatus lockStatus);
        private protected virtual bool IsNewRecordForLocking(TenantCode tenantCode, DataID dataID);

        protected virtual async Task<LockResult> AttemptToWriteLockInfo(LockStatus lockStatus);
        protected virtual async Task<(bool, string)> ExecuteReNameQuery(string newName);

        protected virtual LockResult CheckAndUpdateLockStatusIfExpired();
    }

    /// <summary>
    /// サーバーとの同期機能を追加したIDataObjから継承したインターフェースです。
    /// </summary>
    public interface ISyncDataObj<Key> : IDataObj<Key> {
        void Sync();
    }

    /// <summary>
    /// データオブジェクト管理のためのインターフェースです。
    /// </summary>
    public interface IDataObjMgr<Key> {

        protected string _tblName { get; }
        protected string _s_tblName { get; }
        protected string _infoTbl { get; }
        protected string _w_tblName { get; }
        protected string _ws_tblName { get; }

        protected List<Object> _dataList { get; }
        protected NxDataType _datatype { get; }
        protected DateTime _refreshed_at { get; }

        protected HttpClient _http { get; }

        // Virtual methods that can be implemented by the deriving class

        protected virtual T CreateInstans(InMemoryDatabaseState db, CustomAuthStateProvider auth);

        protected virtual Task Initialize(string strWhere = "") { }

        // Public properties (can be accessed outside this interface via an implementation of IDataObjMgr)

        public IEnumerable<T> DataList { get; }

        protected IDbConnection DBcon { get; }

        protected CustomAuthStateProvider AuthProv { get; }

        // Additional methods

        public virtual async Task<string> GetGroupCodeAsync();

        public virtual async Task Initialize(string strWhere = "");
    }

    /// <summary>
    /// サーバーとの同期機能を追加したIDataObjMgrから継承したインターフェースです。
    /// </summary>
    public interface ISyncDataObjMgr<Key> : IDataObjMgr<Key> {
    }

    /// <summary>
    /// IDataObj<Key>の実装クラスです。
    /// </summary>
    public class DataObj<Key> : IDataObj<Key> {
        protected Key _dataID;

        public DataObj(Key dataId) {
            _dataID = dataId;
        }

        Key IDataObj<Key>.Id {
            get { return _dataID; }
        }

        // 他のデータオブジェクトのプロパティやメソッドをここに追加してください。
    }

    /// <summary>
    /// DataObjのラッパークラスで、サーバーとの同期機能を提供します。
    /// </summary>
    public class SyncDataObj<Key> : ISyncDataObj<Key>, IDataObj<Key> {
        private readonly IDataObj<Key> _dataObj;

        public SyncDataObj(IDataObj<Key> dataObj) {
            _dataObj = dataObj ?? throw new ArgumentNullException(nameof(dataObj));
        }

        Key IDataObj<Key>.Id => _dataObj.Id;

        void ISyncDataObj<Key>.Sync() {
            // サーバーとの同期を実行するためのロジックをここに実装してください。
            throw new NotSupportedException("このメソッドは派生クラスで実装されるべきです。");
        }
    }

    /// <summary>
    /// DataObjMgrのラッパークラスで、サーバーとの同期管理機能を提供します。
    /// </summary>
    public class SyncDataObjMgr<Key> : ISyncDataObjMgr<Key> {
        private readonly DataObjMgr<Key> _dataObjMgr;

        public SyncDataObjMgr(DataObjMgr<Key> dataObjMgr) {
            if (dataObjMgr == null)
                throw new ArgumentNullException(nameof(dataObjMgr));

            _dataObjMgr = dataObjMgr;
        }
    }

    /// <summary>
    /// DataObjの管理クラスです。
    /// </summary>
    public class DataObjMgr<Key> : IDataObjMgr<Key> {
        private readonly List<IDataObj<Key>> _dataObjects = new List<IDataObj<Key>>();

        public void Add(IDataObj<Key> dataObj) {
            if (dataObj == null)
                throw new ArgumentNullException(nameof(dataObj));

            _dataObjects.Add(dataObj);
        }

    }

}
