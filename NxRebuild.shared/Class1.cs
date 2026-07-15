using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    /// <summary>
    /// データオブジェクトのインターフェースです。
    /// </summary>
    public interface IDataObj<Key> {
        Key Id { get; }
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
