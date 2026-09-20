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
    // ---------------------------------------------------------
    // ZmstEntityMgr
    // カラム数80弱+小さいサブテーブル（単位マスタ）を持つ
    // ツリー構造を作るためのスキーマ設計でないため、本来の想定外の実装を行う
    // 
    // ---------------------------------------------------------
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
            RootName = "食品分類";
            DataType = NxDataType.Zairyou;
        }
        protected override int GenerateDataID() {
            throw new InvalidOperationException(
                "ZmstEntityMgr は採番をしないので ZmstentityでGenerateDataID を使用してください。"
            );
        }

        public override ZmstEntity? Get(int id) {
            // _dataList が null の場合は何も返さない
            if (_dataList == null)
                return null;

            // DataList を手動で走査
            foreach (var obj in _dataList) {
                // ID が一致しないならスキップ
                if (obj.DataID != id)
                    continue;

                // CategoryEntity（フォルダ）は除外
                if (obj is CategoryEntity)
                    continue;

                // ZmstEntity だけ返す
                if (obj is ZmstEntity zmst)
                    return zmst;
            }

            // 見つからなかった
            return null;
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

            var tc = TenantCode.ToString(); // Guid → string

            // ---------------------------------------------------------
            // ★ Zmst を正本化して取得
            // ---------------------------------------------------------
            var rawZmstRows = await DBcon.QueryAsync<dynamic>(
                sqlMain,
                new { tc }
            );

            var zmstRows = rawZmstRows
                .Select(r => NxTypeMapper.ConvertRow(_tblName, (IDictionary<string, object>)r))
                .ToList();


            // ---------------------------------------------------------
            // ★ tan_m を正本化して取得
            // ---------------------------------------------------------
            string sqlSub = $@"
                                SELECT *
                                FROM ""{_s_tblName}""
                                WHERE ""tenant_code"" = @tc;
                            ";

            var rawTanRows = await DBcon.QueryAsync<dynamic>(
                sqlSub,
                new { tc }
            );
            //なぜかDapperがDapperRowとして返してくる場合があるので、Dictionary<string, object>に変換する。
            var tanRows = rawTanRows
                .Select(r => NxTypeMapper.ConvertRow(_s_tblName, (IDictionary<string, object>)r))
                .ToList();


            // ---------------------------------------------------------
            // ★ LocalCode ごとにグループ化（正本化後なので安全）
            // ---------------------------------------------------------
            var tanGroups = tanRows
                .GroupBy(r => (int)r["LocalCode"])
                .ToDictionary(g => g.Key, g => g.ToList());


            // ---------------------------------------------------------
            // ★ Zmst + tan_m を合成して返す
            // ---------------------------------------------------------
            var result = new List<Dictionary<string, object>>();

            foreach (var zmst in zmstRows) {
                // LocalCode は正本化済みなので安全
                int localCode = (int)zmst["LocalCode"];

                // Zmst の行をそのまま返す（Initialize → Setproperties で使う）
                result.Add(zmst);

                // tan_m の行を ZmstEntity 内で保持するために追加
                if (tanGroups.TryGetValue(localCode, out var tanList)) {
                    zmst["_tan_m_rows"] = tanList;
                } else {
                    zmst["_tan_m_rows"] = new List<Dictionary<string, object>>();
                }
            }

            return result;
        }


        // ---------------------------------------------------------
        // Initialize（gun_m + Zmst + tan_m を DataList に突っ込む）
        // ---------------------------------------------------------
        public override async Task Initialize() {

            CreateRoot();

            // ---------------------------------------------------------
            // 大分類 gun_m（分類マスタ）をロードして CategoryEntity を追加
            // ---------------------------------------------------------
            string sqlCat = @"
                                SELECT
                                    tenant_code,
                                    0 AS jun,
                                    0 As dai_cd,
                                    dai_name,
                                    dai_cd AS syou_cd,
                                    dai_name AS syou_name
                                FROM gun_m
                                WHERE tenant_code = @tc
                                GROUP BY tenant_code, dai_cd, dai_name
                                ORDER BY dai_cd;
                            ";

            var tc = TenantCode.ToString();   // ← Guid → string に変換

            var rawCatRows = await DBcon.QueryAsync<dynamic>(
                                        sqlCat,
                                        new { tc }
                                    );

            var catRows = rawCatRows
                            .Select(r => NxTypeMapper.ConvertRow("gun_m", (IDictionary<string, object>)r))
                            .ToList();


            foreach (var row in catRows) {
                var dict = (IDictionary<string, object>)row;

                var cat = new CategoryEntity();
                cat.DBcon = DBcon;
                cat.TenantCode = TenantCode;
                cat.CurrUsrID = CurrentUserID;
                cat.Setproperties(dict);

                _dataList.Add(cat);
            }
            // ---------------------------------------------------------
            // 小分類 gun_m（分類マスタ）をロードして CategoryEntity を追加
            // ---------------------------------------------------------
            sqlCat = @"
                        SELECT *
                        FROM ""gun_m""
                        WHERE ""tenant_code"" = @tc;
                    ";

            tc = TenantCode.ToString();   // ← Guid → string に変換

            rawCatRows = await DBcon.QueryAsync<dynamic>(
                                        sqlCat,
                                        new { tc }
                                    );

            catRows = rawCatRows
                            .Select(r => NxTypeMapper.ConvertRow("gun_m", (IDictionary<string, object>)r))
                            .ToList();


            foreach (var row in catRows) {
                var dict = (IDictionary<string, object>)row;

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
