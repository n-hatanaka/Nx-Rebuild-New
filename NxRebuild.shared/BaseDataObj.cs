using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

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

    public enum LockResult {
        Success,       // ロック確保成功
        LockedByOther, // 他人がロック中
        RecordNone,
        DbError        // システムエラー
    }

    public abstract class BaseDataObj<TKey>
    {
        // --- 既存のメンバ（必要に応じてコメントアウト） ---
        // protected TKey _dataID;
        // protected string _dataName = "";
        // protected DateTime _update_at;
        // protected Guid _locker_ID;
        // protected DateTime _locked_at;
        
        // 【変更】レコード内容をJSON（辞書）として保持するメンバを追加
        protected Dictionary<string, object> _rawData = new();
    
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
    
        protected string _tblName; //データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル
    
        protected string _w_tblName;
        protected string _ws_tblName;


        public string NameColName => _nameColName;
        public string IdColName => _idColName;
        public string TblName => _tblName;
        public string S_TblName => _s_tblName;
        public string InfoTbl => _infoTbl;
        public string W_TblName => _w_tblName;
        public string Ws_TblName => _ws_tblName;

        protected NxDataType _datatype;
        protected DateTime _update_at; 
        protected Guid _locker_ID; 
        protected DateTime _locked_at;

    
        // --- 既存のメンバ ---
        public IBaseDataObjMgr SelfObjMgr { get; set; }
        public string TenantCode { get; set; }
        public IDbConnection DBcon { get; set; }
    
        // --- 【変更】プロパティ実装：変数からJSON（_rawData）への参照へ切り替え ---
    
        public TKey DataID 
        { 
            get => (TKey)_rawData[_idColName]; 
            set => _rawData[_idColName] = value!; 
        }
    
        public string DataName 
        { 
            get => (string)_rawData[_nameColName]; 
            set => _rawData[_nameColName] = value; 
        }
    
        public NxDataType DataType => _datatype; // ※_datatypeはメタデータ側管理ならそのままでOK
    
        public DateTime Update_at 
        { 
            get => (DateTime)_rawData["update_at"]; 
            set => _rawData["update_at"] = value; 
        }
        
        public Guid LockerID 
        { 
            get => (Guid)_rawData["locked_by"]; 
            set => _rawData["locked_by"] = value; 
        }
        
        public DateTime LockedAt 
        { 
            get => (DateTime)_rawData["locked_at"]; 
            set => _rawData["locked_at"] = value; 
        }
    
        //参照しているユーザーのID
        //インスタンス作成後に必ずセットする事
        public Guid CurrUsrID { get; set;  }


        // この中は派生先で実装する事。
        //ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        protected abstract void Initialize();
    
        public virtual void SetPropertys(Dictionary<string, object> record)
        {
            // 【変更】recordをそのまま _rawData として保持する設計に移行
            // ※KeyedListとDictionaryの互換性がある前提ですが、
            // 必要に応じてここでコピーまたは変換を行ってください。
            _rawData = record;
    
            // 既存のフィールド個別セットは不要になるため、実質上記の一行で完結します。
            // 個別にプロパティへセットしていた古い実装はここで終了します。
            
            // --- コメントアウトした元実装の意図 ---
            // _dataID = (TKey?)record[_idColName];
            // _dataName = (string)record[_nameColName];
            // _update_at = (DateTime)record["update_at"];
            // _locker_ID = (Guid)record["locked_by"];
            // _locked_at = (DateTime)record["locked_at"];
        }
    // テーブルからデータを取得してJSON文字列にする
        public string LoadDataAsJson()
        {
            string sql = CreateJSONsql();
            var result = DBcon.Query<dynamic>(sql, new { dataID = DataID, tenantCode = TenantCode });
            return JsonSerializer.Serialize(result);
        }
        protected abstract string CreateJSONsql();
        //{ JSON生成用のビュー。以下実装例
        // 各レコードに自動的に "_table_type" というキーが追加される
        //return $@"SELECT t.*, '{_tblName}' as _table_type FROM {_tblName} t
        //        WHERE t.id = @dataID AND t.tenant_code = @tenantCode
        //        UNION ALL
        //        SELECT s.*, '{_s_tblName}' as _table_type FROM {_s_tblName} s
        //        WHERE s.parent_id = @dataID";
        //}

        public async Task<bool> SaveJsonData(string json)
        {
            var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            // JSONを受け取ってテーブルに保存（Delete & Insert）
            var transaction = DBcon.BeginTransaction();
            // 1. Delete: 対象テーブル削除
            var result = await DeleteQueryExec(transaction);
            if (!result) {
                transaction.Rollback();
                return false;
            }

            try {
                foreach (var record in records) {
                    // どのテーブルに属するかを判定するプロパティ(例: "_table_type")があると仮定
                    string targetTable = record.ContainsKey("_table_type") ? record["_table_type"].ToString() : _tblName;

                    // 2. Insert: targetTable に対して動的Insert
                    var columns = string.Join(", ", record.Keys);
                    var values = string.Join(", ", record.Keys.Select(k => "@" + k));

                    DBcon.Execute($"INSERT INTO {targetTable} ({columns}) VALUES ({values})", record, transaction);
                    
                }
                transaction.Commit();
                return true;

            } catch (Exception ex) {
                transaction.Rollback();
                return false;
            }
        }

        public abstract Task<LockStatus> DataOpen();




        public abstract Task<bool> DeleteQueryExec(IDbTransaction transaction);

        // 名前変更の検証メソッド
        // 必要に応じて派生クラスでオーバーライドできるように virtual にしておくと
        public virtual async Task<bool> ReName(string newName) {
            if (await ReNameQueryExec(newName)) {
                this.DataName = newName;
                return true;
            }
            return false;
        }
        protected virtual async Task<bool> ReNameQueryExec(string newName) {
            // ここでSQLを構築して実行
            string sql = $"UPDATE {_tblName} SET {_nameColName} = @name WHERE {_idColName} = @id AND group_code = {TenantCode}";

            // 成功したら true が返る
            return await DBcon.ExecuteAsync(sql, new { name = newName, id = DataID, TenantCode }) > 0;

        }

        public abstract Task<bool> SaveAsync();
        public abstract Task<bool> JsonToTable(string Json);

        //インメモリDB内での処理でのみ使用
        public abstract Task<bool> SaveQueryExec(IDbTransaction transaction);

        public abstract Task<string> TbltoJson();


        


    }
}
