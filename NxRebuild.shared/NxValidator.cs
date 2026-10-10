using System.Data;

namespace NxRebuild.shared {
    public enum NxValidationErrorCode
    {
        None = 0,

        // Save
        EmptyName,
        InvalidParent,
        DuplicateName,

        // Delete
        CannotDeleteRoot,

        // Rename
        RenameEmpty,
        RenameTooLong,

        // Sync
        LockedByOther,

        // Import
        TooOldData,

        // LocalEdit
        LocalEditDenied,

        // その他
        Unknown
      }

    public struct NxValidationResult
    {
        public NxValidationErrorCode ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }

        public bool IsValid => ErrorCode == NxValidationErrorCode.None;

        public static NxValidationResult Ok()
            => new NxValidationResult { ErrorCode = NxValidationErrorCode.None };

        public static NxValidationResult Fail(NxValidationErrorCode code, string msg)
            => new NxValidationResult { ErrorCode = code, ErrorMessage = msg };
    }

    public class NxValidator<TKey>
    {
        public TKey DataID { get; }
        public string DataName { get; }
        public NxDataType DataType { get; }
        public TKey ParentID { get; }
        public Guid CurrUsrID { get; }
        public Guid LockerID { get; }
        public DateTime Update_at { get; }
        public DateTime LockedAt { get; }
        public NxLocationKind LocationKind { get; }
        public Dictionary<string, object> RawData { get; }
        public IDbConnection DBcon { get; }

        public NxValidator(IBaseDataObj<TKey> obj, Dictionary<string,object> ValidData)
        {
            DataID = obj.DataID;
            DataName = obj.DataName;
            DataType = obj.DataType;
            ParentID = obj.ParentID;
            CurrUsrID = obj.CurrUsrID;
            LockerID = obj.LockerID;
            Update_at = obj.Update_at;
            LockedAt = obj.LockedAt;
            LocationKind = obj.LocationKind;
            RawData = ValidData;
            DBcon = obj.DBcon;
        }

        // ---------------------------------------------------------
        // Save
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateSave()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DataName))
                    return NxValidationResult.Fail(NxValidationErrorCode.EmptyName, "名前が空です");

                if (ParentID == null)
                    return NxValidationResult.Fail(NxValidationErrorCode.InvalidParent, "親IDが不正です");

                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // Delete
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateDelete()
        {
            try
            {
                if (DataType == NxDataType.root)
                    return NxValidationResult.Fail(NxValidationErrorCode.CannotDeleteRoot, "ルートは削除できません");

                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // Rename
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateRename(string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return NxValidationResult.Fail(NxValidationErrorCode.RenameEmpty, "名前が空です");

                if (newName.Length > 20)
                    return NxValidationResult.Fail(NxValidationErrorCode.RenameTooLong, "名前が長すぎます");

                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // Sync
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateSync()
        {
            try
            {
                if (LockerID != CurrUsrID)
                    return NxValidationResult.Fail(NxValidationErrorCode.LockedByOther, "他人がロック中です");

                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // Import
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateImport()
        {
            try
            {
                if (Update_at < DateTime.UtcNow.AddYears(-10))
                    return NxValidationResult.Fail(NxValidationErrorCode.TooOldData, "古すぎるデータです");

                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // LocalEdit
        // ---------------------------------------------------------
        public virtual NxValidationResult ValidateLocalEdit()
        {
            try
            {
                return NxValidationResult.Ok();
            }
            catch (Exception ex)
            {
                return NxValidationResult.Fail(NxValidationErrorCode.Unknown, $"例外: {ex.Message}");
            }
        }
    }
}