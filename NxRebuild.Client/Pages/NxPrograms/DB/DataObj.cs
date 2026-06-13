using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    [Flags] public enum NxDataType {
        root = 0,
        Folder  = 1,
        Zairyou = 2, 
        Ryouri  = 4,
        Meal    = 8,
        Kondate = 16,
        Calendar = 32,
        Person   = 64,
        InstMeals = 128 //給食'Institutional meals'

    }

    public class TFileInfo {
        public DataobjMgr thisMgr { get; init; }
        public Guid Data_ID { get; init; }
        public string DataName { get; init; }
        public DateTime Date { get; init; }
        public NxDataType DataType { get; init; }
        public DataObj<Guid> ObjParent { get; init; }
        public Guid ParentCode { get; init; }
        public DateTime Update_at { get; init; }
        public Guid Rocked_by { get; init; }
        public DateTime Locked_at { get; init; }

        public int SortNo { get; init; }
        public Dictionary<string, float> ExtData { get; init; } // 拡張情報
    }


    public class DataObj<TKey> {
        //データのID　Guidの場合とIntの場合があるので継承先で指定しなおす事
        protected TKey _dataID;
        protected string _dataName = "";

        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名


        protected string _tblName;　//データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        protected string _w_tblName;
        protected string _ws_tblName;

        protected NxDataType _datatype;
        protected DateTime _update_at;
        protected Guid _locker_ID;
        protected DateTime _locked_at;



        public TKey DataID { get => _dataID; }
        public string DataName { get; set; }
        public NxDataType DataType { get => _datatype; }

        public DateTime Update_at { get => _update_at; }
        public Guid LockerID { get => _locker_ID; set { _locker_ID = value; }}
        public DateTime LockedAt { get=>_locked_at; set { _locked_at = value; }}

        private InMemoryDatabaseState _dbstate;

        private AuthenticationStateProvider _authProv;
        protected InMemoryDatabaseState dbState { get => _dbstate; }
        protected AuthenticationStateProvider authProv { get => _authProv; }

        public DataObj(InMemoryDatabaseState db, AuthenticationStateProvider auth) {
            _dbstate = db;
            _authProv = auth;
            Initialize();
        }

        protected virtual void Initialize() {

        }

        //渡されたレコードの内容をプロパティに反映
        //一応書いたけど継承先で書き直す事
        public virtual void SetPropertys(IDictionary<string, object> record) {
            _dataID = (TKey?)record["ID"];
            _dataName = (string)record[_nameColName];
            _update_at = (DateTime)record["update_at"];
            _locker_ID = (Guid)record["edit_by"];
            _locked_at = (DateTime)record["locked_at"];
        }
    }
}
