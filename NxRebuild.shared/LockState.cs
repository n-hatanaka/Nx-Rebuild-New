using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    public class LockStatus {
        public bool IsLocked { get; set; } = false;
        public string? LockedByUserId { get; set; }
        public string? LockedByUserName { get; set; }
        public DateTime? Update_at { get; set; } = null;
    }
}
