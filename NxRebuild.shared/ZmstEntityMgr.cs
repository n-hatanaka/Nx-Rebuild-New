using System;
using System.Collections.Generic;
using System.Text;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {
    public interface IZmstEntityMgr : IBaseDataObjMgr<ZmstEntity, int> {
        List<CategoryEntity> GunList { get; }
    }

    public class ZmstEntityMgr : BaseDataObjMgr<ZmstEntity, int>, IZmstEntityMgr {
        // ---------------------------------------------------------
        // 食品群リスト
        // ---------------------------------------------------------
        public List<CategoryEntity> GunList {
            get {
                return _dataList
                    .OfType<CategoryEntity>()
                    .OrderBy(x => Convert.ToInt32(x.jun))
                    .ToList();
            }
        }


        public ZmstEntityMgr(IDbConnection db, Guid tenantCode, Guid currUserID)
            : base(db, tenantCode, currUserID) {
            _tblName = "Zmst";
            _s_tblName = "tan_m";
            _infoTbl = "";
            _w_tblName = "";
            _ws_tblName = "";

            DataType = NxDataType.Zairyou;
        }
        protected override int GenerateDataID() {
            throw new InvalidOperationException(
                "ZmstEntityMgr は採番をしないので ZmstentityでGenerateDataID を使用してください。"
            );
        }

        public override ZmstEntity CreateNewDataObj() {

            var obj = new ZmstEntity();
            obj.DBcon = DBcon;
            obj.SelfObjMgr = (IBaseDataObjMgr<ZmstEntity, int>)this;
            obj.TenantCode = TenantCode;
            obj.CurrUsrID = CurrentUserID;

            obj.DataID = 0; //IDは保存時に取得する。
            obj.Setproperties(GetEmptySchema());

            _dataList.Add(obj);
            return obj;
        }



        // ---------------------------------------------------------
        // Zmst + tan_m をまとめてロードする
        // ---------------------------------------------------------
        public override async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            // --- Zmst ---
            string sqlMain = $@"
                                SELECT *
                                FROM ""{_tblName}""
                                WHERE ""tenant_code"" = @tc;
                            ";

            var tc = TenantCode.ToString(); // Guid → string に変換

            var zmstRows = await DBcon.QueryAsync<Dictionary<string, object>>(
                sqlMain,
                new { tc }
            );

            // --- tan_m ---
            string sqlSub = $@"
                                SELECT *
                                FROM ""{_s_tblName}""
                                WHERE ""tenant_code"" = @tc;
                            ";

            var tanRows = await DBcon.QueryAsync<Dictionary<string, object>>(
                sqlSub,
                new { tc }
            );

            // --- LocalCode ごとにグループ化 ---
            var tanGroups = tanRows.GroupBy(r => Convert.ToInt32(r["LocalCode"]))
                                   .ToDictionary(g => g.Key, g => g.ToList());

            // --- Zmst + tan_m を合成して返す ---
            var result = new List<Dictionary<string, object>>();

            foreach (var zmst in zmstRows) {
                int localCode = Convert.ToInt32(zmst["LocalCode"]);

                // Zmst の行をそのまま返す（Initialize → Setproperties で使う）
                result.Add(zmst);

                // tan_m の行は ZmstEntity 内で保持するため、
                // Initialize 後に ZmstEntityMgr が TanList を埋める
                if (tanGroups.TryGetValue(localCode, out var tanList)) {
                    // tan_m の行を ZmstEntity に渡すために
                    // DataList に追加後に ZmstEntity.TanList を埋める
                    zmst["_tan_m_rows"] = tanList;
                } else {
                    zmst["_tan_m_rows"] = new List<Dictionary<string, object>>();
                }
            }

            return result;
        }


        // ---------------------------------------------------------
        // Initialize（Zmst + tan_m を DataList に突っ込む）
        // ---------------------------------------------------------
        public override async Task Initialize() {

            var root = CreateRoot();
            _dataList.Add(root);

            // ---------------------------------------------------------
            // ★ gun_m（分類マスタ）をロードして CategoryEntity を追加
            // ---------------------------------------------------------
            string sqlCat = @"
                                SELECT *
                                FROM ""gun_m""
                                WHERE ""tenant_code"" = @tc;
                            ";

            var tc = TenantCode.ToString();   // ← Guid → string に変換

            var catRows = await DBcon.QueryAsync<dynamic>(
                sqlCat,
                new { tc }                    // ← パラメータ名一致
            );

            foreach (var row in catRows) {
                var dict = (IDictionary<string, object>)row;

                Console.WriteLine("Keys: " + string.Join(", ", dict.Keys));
                Console.WriteLine($"App TenantCode: {TenantCode}");

                if (dict.TryGetValue("tenant_code", out var tnc))
                    Console.WriteLine($"DB TenantCode: {tnc}");
                else
                    Console.WriteLine("DB TenantCode: <none>");

                Console.WriteLine($"Equal? {TenantCode.ToString() == dict["tenant_code"]?.ToString()}");

                var cat = new CategoryEntity();
                cat.DBcon = DBcon;
                cat.TenantCode = TenantCode;
                cat.CurrUsrID = CurrentUserID;
                cat.Setproperties(dict);

                _dataList.Add(cat);
            }

            // ---------------------------------------------------------
            // ★ Zmst + tan_m をロード
            // ---------------------------------------------------------
            var records = await LoadRecordsAsync();

            foreach (var record in records) {
                var obj = new ZmstEntity();
                obj.DBcon = DBcon;
                obj.TenantCode = TenantCode;
                obj.CurrUsrID = CurrentUserID;

                obj.Setproperties((IDictionary<string, object>)record);

                if (record["_tan_m_rows"] is List<Dictionary<string, object>> subRows) {
                    foreach (var row in subRows) {
                        obj.TanList.Add(new TanMEntity(row));
                    }
                } else {
                    obj.TanList.Clear();
                }

                _dataList.Add(obj);
            }

            foreach (var obj in _dataList) {
                SetParent((ZmstEntity)obj);
            }
        }

    }
}
