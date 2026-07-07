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
    
        protected NxDataType _datatype;
        protected DateTime _update_at; // 既存のメンバ
        protected Guid _locker_ID; // 既存のメンバ
        protected DateTime _locked_at; // 既存のメンバ
    
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
    
        // この中は派生先で実装する事。
        //ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        protected abstract void Initialize();
    
        public virtual void SetPropertys(KeyedList<string, object> record)
        {
            // 【変更】recordをそのまま _rawData として保持する設計に移行
            // ※KeyedListとDictionaryの互換性がある前提ですが、
            // 必要に応じてここでコピーまたは変換を行ってください。
            _rawData = record.ToDictionary(x => x.Key, x => x.Value);
    
            // 既存のフィールド個別セットは不要になるため、実質上記の一行で完結します。
            // 個別にプロパティへセットしていた古い実装はここで終了します。
            
            // --- コメントアウトした元実装の意図 ---
            // _dataID = (TKey?)record[_idColName];
            // _dataName = (string)record[_nameColName];
            // _update_at = (DateTime)record["update_at"];
            // _locker_ID = (Guid)record["locked_by"];
            // _locked_at = (DateTime)record["locked_at"];
        }
 

        public abstract Task<LockStatus> DataOpen();

        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで
        protected virtual async Task<LockStatus> LockedChkfromTbl() {
            // SQLでlocked_atとlocked_byの両方を取得
            var sql = $@"SELECT locked_at, locked_by as UserId, Update_at  
                 FROM {_tblName} 
                 WHERE group_code = @groupCode AND {_idColName} = @dataID";

            var result = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, new { TenantCode, _dataID });

            // デフォルト値を設定
            bool isLocked = false;
            string? userId = null;
            DateTime updateAt = result?.Update_at ?? DateTime.MinValue;

            // レコードが取れなかった場合
            if (result == null) {
                return new LockStatus { Exists = false };
            }

            // レコードがある場合
            DateTime? lockedAt = result.locked_at as DateTime?;
            bool locked = lockedAt != null && (DateTime.UtcNow - lockedAt.Value).TotalMinutes < 10;

            LockStatus lockSt = new LockStatus{
                                            Exists = true, // レコードあり！
                                            IsLocked = locked,
                                            LockedByUserId = locked ? (string)result.UserId : null,
                                            Locked_at = (DateTime?)result.Update_at
                                        };
            //自分のプロパティも更新
            Guid parsedGuid;
            _locked_at = (DateTime)lockSt.Locked_at;

            // 文字列をGuidに変換する
            if (Guid.TryParse(lockSt.LockedByUserId, out parsedGuid)) {
                _locker_ID = parsedGuid;
            } else {
                // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                _locker_ID = Guid.Empty;
            }
            return lockSt;

        }

        public virtual async Task<LockStatus>SetLockAsync(LockStatus lockStatus) {
            LockStatus Lockst = await LockedChkfromTbl();
            Guid parsedGuid;
            if (Lockst.IsLocked) {
                //すでにロック済みの場合その情報を返す。
                //自分のプロパティも更新
                _locked_at = (DateTime)Lockst.Locked_at;

                // 文字列をGuidに変換する
                if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                    _locker_ID = parsedGuid;
                } else {
                    // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                    _locker_ID = Guid.Empty;
                }
                return Lockst;
            } else {
                //ロック情報書き込み
                LockResult result = await WriteLockInfoAsync(lockStatus);
                switch (result) {
                    case LockResult.Success:
                        // 書き込み成功後、改めて最新の情報をDBから取得して返す
                        //自分のプロパティも更新
                        _locked_at = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            _locker_ID = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            _locker_ID = Guid.Empty;
                        }
                        return await LockedChkfromTbl();

                    case LockResult.RecordNone:
                        // ロックすべきレコードが無い（新規データ）の場合、
                        // ロックしたものとしてリクエスト内容を返す
                        //すでにロック済みの場合その情報を返す。
                        _locked_at = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            _locker_ID = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            _locker_ID = Guid.Empty;
                        }
                        return lockStatus;

                    case LockResult.DbError:
                    default:
                        // エラー（DbError）および想定外のケース（default）の処理
                        // ロック無し、かつHasErrorを立てて通知する
                        return new LockStatus {
                            Exists = false,
                            IsLocked = false,
                            LockedByUserId = null,
                            Locked_at = null,
                            HasError = true,
                            ErrorMessage = "DBエラーが発生しました"
                        };
                }
            }

            
        }

        protected virtual async Task<LockResult> WriteLockInfoAsync(LockStatus lockStatus) {
            // 10分経過したものは期限切れとみなす
            var expiryTime = DateTime.UtcNow.AddMinutes(-10);

            // 1. まず更新を試みる
            string sql = $@"
                        UPDATE {_tblName} 
                        SET locked_by = @userId, locked_at = @lockedAt 
                        WHERE {_idColName} = @dataID 
                          AND group_code = @tenantCode
                          AND (locked_at IS NULL OR locked_at < @expiryTime)";

            try {
                int affectedRows = await DBcon.ExecuteAsync(sql, new {
                    userId = lockStatus.LockedByUserId,
                    lockedAt = DateTime.UtcNow,
                    dataID = _dataID,
                    tenantCode = TenantCode,
                    expiryTime = expiryTime
                });

                if (affectedRows > 0) return LockResult.Success;

                // 2. 更新できなかった場合、理由を調べるために再確認
                // ここでレコードが存在するか確認する
                var currentStatus = await LockedChkfromTbl();

                // currentStatus.Update_at が MinValue ならレコード無しと判定
                if (currentStatus.Locked_at == DateTime.MinValue) {
                    return LockResult.RecordNone;
                }

                // レコードはあるがIsLockedがtrue＝他人がロック中
                return LockResult.LockedByOther;

            } catch (Exception ex) {
                return LockResult.DbError;
            }
        }

        public abstract Task<bool> DeleteQueryExec();

        // 名前変更の検証メソッド
        // 必要に応じて派生クラスでオーバーライドできるように virtual にしておくと
        public virtual async Task<bool> ReName(string newName) {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            // 2. ロック状態を確認
            LockStatus currentLock = await LockedChkfromTbl();

            // 3. エクスプローラー仕様のガード
            // 誰かがロックしていたら（IsLocked == true）、例えそれが自分でもリネーム不可
            if (currentLock.IsLocked) {
                return false;
            }

            // 4. ロックされていなければ実行
            if (await ReNameQueryExec(newName)) {
                this._dataName = newName;
                return true;
            }

            return false;
        }
        protected virtual async Task<bool> ReNameQueryExec(string newName) {
            // ここでSQLを構築して実行
            string sql = $"UPDATE {_tblName} SET {_nameColName} = @name WHERE {_idColName} = @id AND group_code = {TenantCode}";

            // 成功したら true が返る
            return await DBcon.ExecuteAsync(sql, new { name = newName, id = _dataID }) > 0;

        }

        //インメモリDB内での処理でのみ使用
        public abstract Task<bool> SaveQueryExec();

        public abstract Task<string> TbltoJson();

        public abstract Task<bool> JsonToTable(string Json);

        


    }
}
