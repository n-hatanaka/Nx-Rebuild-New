using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    public class LockStatus {
        public bool Exists { get; set; } = false; // レコードが存在するか
        public bool IsLocked { get; set; } = false;
        public string? LockedByUserId { get; set; }
        public string? LockedByUserName { get; set; }
        public DateTime? Locked_at { get; set; } = null;
        public bool HasError { get; set; } = false;
        public string? ErrorMessage { get; set; } = "";
    }
}
