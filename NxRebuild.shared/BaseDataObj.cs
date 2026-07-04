using System;
using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace NxRebuild.shared {
    [Flags]
    public enum NxDataType {
        root = 0,
        Folder = 1,
        Zairyou = 2,
        Ryouri = 4,
        Meal = 8,
        Kondate = 16,
        Calendar = 32,
        Person = 64,
        InstMeals = 128 //給食'Institutional meals'

    }

    public abstract class BaseDataObj<TKey> {
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

        //テナントコード取得用。サーバー側ではこれを使う
        //protected readonly UserManager<ApplicationUser> _userMgr;

        //テナントコード、認証JWT取得用。クライアント側ではこれを使う
        //private CustomAuthStateProvider _authProv;

        public TKey DataID { get => _dataID; }
        public string DataName { get; set; }
        public NxDataType DataType { get => _datatype; }

        public DateTime Update_at { get => _update_at; }
        public Guid LockerID { get => _locker_ID; set { _locker_ID = value; } }
        public DateTime LockedAt { get => _locked_at; set { _locked_at = value; } }

        //BaseDataObjMgr<BaseDataObj<TKey>, TKey> SelfObjMgr { get; set; }
        //↑の型指定でプロパティ定義できないのでオブジェクト型で持たせる。
        //使うときはTypedDataObjMgrを使う。
        public IBaseDataObjMgr SelfObjMgr { get; set; }
        public string TenantCode { get; set; }

        public IDbConnection DBcon { get; set; }

        //public BaseDataObj(IDbConnection dbcon) {
        //    _dbcon = dbcon;
        //}

        //この中は派生先で実装する。
        //サーバーサイド、クライアントサイドで
        protected abstract string GetGroupCode();

        //この中は派生先で実装する事。
        //ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        protected abstract void Initialize();


        public virtual void SetPropertys(KeyedList<string, object> record) {
            _dataID = (TKey?)record[_idColName];
            _dataName = (string)record[_nameColName];
            _update_at = (DateTime)record["update_at"];
            _locker_ID = (Guid)record["locked_by"];
            _locked_at = (DateTime)record["locked_at"];
        }

        public abstract Task<LockStatus> DataOpen();


    }
}
