using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    public enum NxOperationErrorCode {
        None = 0,
        // 通信
        NetworkError,
        Timeout,
        HttpError,
        ResponseParseError,

        // DB
        DbError,
        TransactionFailed,

        // API
        ServerException,

        // その他
        Unknown
    }

    // 同期1件分: DataId と、そのデータの TblToJson の結果
    public class SyncItem<TKey> {
        public TKey DataId { get; set; } = default!;
        public string Data { get; set; } = "";
    }

    public class NxOperationResult<TKey> {
        // Validation
        public NxValidationErrorCode ValidationErrorCode { get; set; }
            = NxValidationErrorCode.None;

        // Operation
        public NxOperationErrorCode OperationErrorCode { get; set; }
            = NxOperationErrorCode.None;

        // 成功判定
        public bool Success =>
            ValidationErrorCode == NxValidationErrorCode.None &&
            OperationErrorCode == NxOperationErrorCode.None;

        // UI表示用
        public string Message { get; set; } = string.Empty;

        // Save / Rename
        public TKey? NewID { get; set; }
        public string UpdateAt { get; set; } = string.Empty;

        // Delete
        public List<TKey> FailedIds { get; set; } = new();

        // Sync / Lock
        public LockStatus? LockStatus { get; set; }

        // 正本データ
        public Dictionary<string, object> RawData { get; set; } = new();

        // SyncAll / DataOpenLatest
        public DateTime RefreshedAt { get; set; }
        public List<SyncItem<TKey>> SyncItems { get; set; } = new();
    }
}