using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {

    public enum LockResult {
        Success,       // ロック確保成功
        LockedByOther, // 他人がロック中
        RecordNone,
        DbError        // システムエラー
    }

    public class LockStatus {
        public bool Exists { get; set; } = false; // レコードが存在するか
        public bool IsLocked { get; set; } = false;
        public Guid? LockedByUserId { get; set; } = Guid.Empty;
        public string? LockedByUserName { get; set; } = "";
        public DateTime? Locked_at { get; set; } = null;
        public bool HasError { get; set; } = false;
        public string? ErrorMessage { get; set; } = "";
    }
}
