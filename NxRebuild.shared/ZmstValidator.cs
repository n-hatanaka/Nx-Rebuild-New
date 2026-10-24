using System.Data;
using System.Security.Cryptography.X509Certificates;

namespace NxRebuild.shared {
    public class ZmstValidator : NxValidator<int> {
        public ZmstValidator(IBaseDataObj<int> dataObj, Dictionary<string, object> rawData)
            : base(dataObj, rawData)
        {
          
        }
        public override NxOperationResult<int> ValidateDelete() {
            var result = base.ValidateDelete();
            if (!result.Success)
                return result;

            try {
                if (DataType == NxDataType.Folder)
                    return new NxOperationResult<int> {
                        ValidationErrorCode = NxValidationErrorCode.CannotDeleteRoot,
                        Message = "分類は削除できません"
                    };

                return new NxOperationResult<int>();
            } catch (Exception ex) {
                return new NxOperationResult<int> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }
        public override NxOperationResult<int> ValidateRename(string newName) {
            try {
                if (string.IsNullOrWhiteSpace(newName))
                    return new NxOperationResult<int> {
                        ValidationErrorCode = NxValidationErrorCode.RenameEmpty,
                        Message = "名前が空です"
                    };

                if (newName.Length > 56)
                    return new NxOperationResult<int> {
                        ValidationErrorCode = NxValidationErrorCode.RenameTooLong,
                        Message = "名前が長すぎます"
                    };

                return new NxOperationResult<int>();
            } catch (Exception ex) {
                return new NxOperationResult<int> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"例外: {ex.Message}"
                };
            }
        }
    }
}
