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

    public class NxOperationResult<TKey> {
        // ★ Validation（業務ロジック）エラー
        public NxValidationErrorCode ValidationErrorCode { get; set; } = NxValidationErrorCode.None;

        // ★ Operation（通常処理）エラー
        public NxOperationErrorCode OperationErrorCode { get; set; } = NxOperationErrorCode.None;

        // ★ 成功判定（計算）
        public bool Success =>
            ValidationErrorCode == NxValidationErrorCode.None &&
            OperationErrorCode == NxOperationErrorCode.None;

        // ★ 共通メッセージ（UI が表示する）
        public string? Message { get; set; }

        // Save / Rename 共通
        public TKey? NewID { get; set; }
        public string? UpdateAt { get; set; }

        // Delete 用
        public List<TKey>? FailedIds { get; set; }

        // Sync / Lock 用
        public LockStatus? LockStatus { get; set; }

        // ★ 正本の全カラム（成功時のみ）
        public Dictionary<string, object>? RawData { get; set; }

        // ★ SyncAll 専用の Items
        public List<object>? SyncItems { get; set; }
    }

}
