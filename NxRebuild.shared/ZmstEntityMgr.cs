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

    public interface IsrvZmstEntityMgr : IZmstEntityMgr, IsrvBaseDataObjMgr<ZmstEntity, int> {

    }
    // ---------------------------------------------------------
    // ZmstEntityMgr
    // カラム数80弱+小さいサブテーブル（単位マスタ）を持つ
    // マスタデータのため、全体をロードしてキャッシュする方法をとる
    // ツリー構造を作るためのスキーマ設計でないため、本来の想定外の実装を行う
    // 
    // ---------------------------------------------------------
    public class ZmstEntityMgr : BaseDataObjMgr<ZmstEntity, int>, IZmstEntityMgr, IsrvZmstEntityMgr {
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
            //"ZmstEntityMgr は採番をしないので ZmstentityでGenerateDataID を使用してください。"
            return 0;
        }

        public override IZmstEntity? Get(int id) {
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
                if (obj is IZmstEntity zmst)
                    return zmst;
            }

            // 見つからなかった
            return null;
        }


        // ---------------------------------------------------------
        // Zmst をまとめてロードする
        // ---------------------------------------------------------
        public override async Task<IEnumerable<dynamic>> _LoadRecordsAsync(RecordQuery? q) {
            string sqlMain = $@"
                                    SELECT *
                                    FROM ""{_tblName}""
                                    WHERE ""tenant_code"" = @tc;
                                ";


            var rawZmstRows = await DBcon.QueryAsync<dynamic>(
                sqlMain,
                new { tc = TenantCode }
            );

            // Zmst の正本化
            var zmstRows = rawZmstRows
                .Select(r => NxTypeMapper.ConvertRow(_tblName, (IDictionary<string, object>)r))
                .ToList();

            return zmstRows;
        }



        // ---------------------------------------------------------
        // Initialize（gun_m + Zmst + tan_m を DataList に突っ込む）
        // ---------------------------------------------------------
        public override async Task Initialize(RecordQuery? q) {
            try {
                this.DataType = NxDataType.Zairyou;

                CreateRoot();

                // ---------------------------------------------------------
                // 大分類 gun_m（分類マスタ）
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

                var rawCatRows = await DBcon.QueryAsync<dynamic>(
                    sqlCat,
                    new { tc = TenantCode }   // ← パラメータ名修正
                );

                var catRows = rawCatRows
                    .Select(r => NxTypeMapper.ConvertRow("gun_m", (IDictionary<string, object>)r))
                    .ToList();

                foreach (var row in catRows) {
                    var cat = new CategoryEntity();
                    cat.DBcon = DBcon;
                    cat.TenantCode = TenantCode;
                    cat.CurrUsrID = CurrentUserID;
                    cat.SetProperties(row);

                    _dataList.Add(cat);
                }

                // ---------------------------------------------------------
                // 小分類 gun_m
                // ---------------------------------------------------------
                sqlCat = @"
                            SELECT *
                            FROM ""gun_m""
                            WHERE ""tenant_code"" = @tc;
                        ";

                rawCatRows = await DBcon.QueryAsync<dynamic>(
                    sqlCat,
                    new { tc = TenantCode }
                );

                catRows = rawCatRows
                    .Select(r => NxTypeMapper.ConvertRow("gun_m", (IDictionary<string, object>)r))
                    .ToList();

                foreach (var row in catRows) {
                    var cat = new CategoryEntity();
                    cat.DBcon = DBcon;
                    cat.TenantCode = TenantCode;
                    cat.CurrUsrID = CurrentUserID;
                    cat.SetProperties(row);

                    _dataList.Add(cat);
                }

                // ---------------------------------------------------------
                // Zmst + tan_m
                // ---------------------------------------------------------
                var records = await _LoadRecordsAsync(null);

                foreach (var record in records) {
                    var dict = (IDictionary<string, object>)record;

                    var obj = new ZmstEntity {
                        DBcon = DBcon,
                        SelfObjMgr = this,
                        TenantCode = TenantCode,
                        CurrUsrID = CurrentUserID,
                        DataType = this.DataType
                    };

                    obj.SetProperties(dict);

                    await obj.LoadSubTablesFromRaw();

                    _dataList.Add(obj);
                }

                foreach (var obj in _dataList) {
                    SetParent((ZmstEntity)obj);
                }
            } catch (Exception ex) {
                Console.WriteLine("=== Initialize Exception ===");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.GetType().FullName);

                if (ex.InnerException != null) {
                    Console.WriteLine("--- InnerException ---");
                    Console.WriteLine(ex.InnerException.Message);
                    Console.WriteLine(ex.InnerException.GetType().FullName);
                }

                Console.WriteLine("==============================");

                // 世界線を壊さないために「空の _dataList のまま返す」
                // ここで throw しないのが Nx の正しい世界線
                return;
            }
        }


    }
}
