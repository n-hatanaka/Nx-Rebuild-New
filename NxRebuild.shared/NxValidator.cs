using System.Data;

namespace NxRebuild.shared {
    public enum NxValidationErrorCode {
        None = 0,

        // Save
        EmptyName,
        CannotRename,
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

    public class NxValidator<TKey> {
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

        public NxValidator(IBaseDataObj<TKey> obj, Dictionary<string, object> ValidData) {
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
        public virtual NxOperationResult<TKey> ValidateSave() {
            try {
                var result = ValidateRename(DataName);
                if (!result.Success)
                    return result;


                return new NxOperationResult<TKey>(); // Success
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------
        // Delete
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateDelete() {
            try {
                if (DataType == NxDataType.root)
                    return new NxOperationResult<TKey> {
                        ValidationErrorCode = NxValidationErrorCode.CannotDeleteRoot,
                        Message = "ルートは削除できません"
                    };

                return new NxOperationResult<TKey>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------
        // Rename
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateRename(string newName) {
            try {
                if (string.IsNullOrWhiteSpace(newName))
                    return new NxOperationResult<TKey> {
                        ValidationErrorCode = NxValidationErrorCode.RenameEmpty,
                        Message = "名前が空です"
                    };

                return new NxOperationResult<TKey>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------
        // Sync
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateSync() {
            try {

                return new NxOperationResult<TKey>();

            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------
        // Import
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateImport() {
            try {

                return new NxOperationResult<TKey>();

            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------
        // LocalEdit
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateLocalEdit() {
            try {
                return new NxOperationResult<TKey>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }
    }

}