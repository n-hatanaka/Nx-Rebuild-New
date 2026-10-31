using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;

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
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        public string _parentIDColName;//テーブルの親IDカラムのカラム名
        public string _primaryTableName; //データ名等基本データが格納されるテーブル名

        public string[] _primaryTblPKList;
        public string[] _detailTableList;//材料など詳細データが格納されるテーブル名
        public Dictionary<string, string[]> _detailTblPKList;//詳細テーブルのPKカラム名リスト>
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        //ワークテーブル名、ワークテーブルを持たない場合は
        //_primaryTableName,_detailTableList
        //と同じ値を設定すること
        protected string _w_tblName;
        protected string[] _w_detailTblList;

        public TKey DataID { get; }
        public string DataName { get; }
        public NxDataType DataType { get; }
        public TKey ParentID { get; }
        public Guid CurrUsrID { get; }
        public Guid LockerID { get; }
        public DateTime Update_at { get; }
        public DateTime LockedAt { get; }
        public NxLocationKind LocationKind { get; }
        public IDbConnection DBcon { get; }

        // 検証対象JSON
        public string? RawJson { get; }

        // パース後キャッシュ
        protected List<TableJson>? _workingRaw;
        protected List<TableJson>? WorkingRaw {
            get {
                if (_workingRaw == null)
                    ParseJson();

                return _workingRaw;
            }
        }

        public NxValidator(
            IBaseDataObj<TKey> obj,
            string? rawJson = null) {


            DataID = obj.DataID;
            DataName = obj.DataName;
            DataType = obj.DataType;
            ParentID = obj.ParentID;
            CurrUsrID = obj.CurrUsrID;
            LockerID = obj.LockerID;
            Update_at = obj.Update_at;
            LockedAt = obj.LockedAt;
            LocationKind = obj.LocationKind;
            RawJson = rawJson;
            DBcon = obj.DBcon;


            var meta = NxObjsMetaReader.Read(obj.GetType());

            _primaryTableName = meta.PrimaryTable;

            _primaryTblPKList = meta.PriTblPKColList;

            _detailTableList = meta.DetailTableList;
            //DetailテーブルのPKカラム名リストをDictionaryに変換
            _detailTblPKList = new Dictionary<string, string[]>();

            // ★ JSON → Dictionary<string, string[]>
            _detailTblPKList = JsonSerializer.Deserialize<Dictionary<string, string[]>>(meta.DetailTblPKJson)
                                ?? new Dictionary<string, string[]>();

            _nameColName = meta.NameCol;
            _idColName = meta.IdCol;
            _parentIDColName = meta.ParentIDCol;
            _infoTbl = meta.InfoTable;
            _w_tblName = meta.WorkPriTable;
            _w_detailTblList = meta.WorkDtlTblList;
        }

        protected List<TableJson>? ParseJson() {
            if (_workingRaw != null)
                return _workingRaw;

            if (string.IsNullOrWhiteSpace(RawJson))
                return null;

            _workingRaw =
                JsonSerializer.Deserialize<List<TableJson>>(RawJson);

            return _workingRaw;
        }

        // ---------------------------------------------------------
        // Save
        // ---------------------------------------------------------
        public virtual NxOperationResult<TKey> ValidateSave() {
            try {
                // JSON未設定
                if (WorkingRaw == null) {
                    return new NxOperationResult<TKey> {
                        ValidationErrorCode = NxValidationErrorCode.Unknown,
                        Message = "保存データが存在しません"
                    };
                }

                // メインテーブル確認
                var mst = WorkingRaw
                    .FirstOrDefault(x => x.Table == _primaryTableName);

                if (mst == null) {
                    return new NxOperationResult<TKey> {
                        ValidationErrorCode = NxValidationErrorCode.Unknown,
                        Message = $"メインテーブル[{_primaryTableName}]が存在しません"
                    };
                }

                //データ名の確認
                var row = mst.Rows[0];
                var result = ValidateRename(
                    row[_nameColName]?.ToString() ?? string.Empty);

                if (!result.Success)
                    return result;

                // 明細テーブル確認
                foreach (var tblName in _detailTableList) {
                    var detail = WorkingRaw
                        .FirstOrDefault(x => x.Table == tblName);

                    if (detail == null) {
                        return new NxOperationResult<TKey> {
                            ValidationErrorCode = NxValidationErrorCode.Unknown,
                            Message = $"明細テーブル[{tblName}]が存在しません"
                        };
                    }
                }

                return new NxOperationResult<TKey>();
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