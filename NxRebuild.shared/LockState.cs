using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {

public enum LockResult {
    Success,        // ロック確保成功
    LockedByOther,  // 他人がロック中
    RecordNone,     // レコード無し（新規扱い）
    DbError         // DBエラー
}

public class LockStatus
{
    // ---- DBの生データ（正本世界線） ----
    public bool Exists { get; set; } = false;
    public Guid? LockedByUserId { get; set; } = Guid.Empty;
    public string? LockedByUserName { get; set; } = "";
    public DateTime? Locked_at { get; set; } = null;
    public DateTime? Update_at { get; set; } = null;

    // ---- 判定に必要な外部情報 ----
    public Guid CurrUserId { get; set; } = Guid.Empty;

    // ---- 判定ロジック（計算プロパティ） ----

    // ロック時刻が有効か（10分以内）
    public bool IsTimeValid =>
        Locked_at != null &&
        (DateTime.UtcNow - Locked_at.Value).TotalMinutes < 10;

    // 自分がロックしたかどうか
    public bool IsMine =>
        LockedByUserId != null &&
        LockedByUserId == CurrUserId;

    // 編集可能判定（他人ロックなら不可）
    public bool CanEdit =>
        !IsLockedForEdit;   // 他人ロックでなければ編集可

    // 編集時ロック判定（他人ロック → 編集不可）
    public bool IsLockedForEdit =>
        IsTimeValid && !IsMine;

    // 保存可能判定（自分ロック or RecordNone）
    public bool CanSave =>
        Exists == false ||      // RecordNone → 保存可（新規作成）
        (IsTimeValid && IsMine); // 自分ロック → 保存可

    // 旧IsLocked互換（他人ロックを示す）
    public bool IsLocked =>
        IsLockedForEdit;

    // ---- LockResult をロジックで返す（計算プロパティ） ----
    public LockResult Result {
        get {
            if (HasError)
                return LockResult.DbError;

            if (!Exists)
                return LockResult.RecordNone;

            if (IsLockedForEdit)
                return LockResult.LockedByOther;

            return LockResult.Success;
        }
    }

    // ---- 例外系 ----
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; } = "";
}
}
