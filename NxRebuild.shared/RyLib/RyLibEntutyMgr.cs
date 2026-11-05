using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {
    public class RyLibEntityMgr : BaseDataObjMgr<RyLibEntity, Guid> {
        
        public RyLibEntityMgr(IDbConnection db, Guid tenantCode, Guid currUserID) 
            : base(db, tenantCode, currUserID) {
            RootName = "料理ライブラリ";
            DataType = NxDataType.Ryouri;
        }

        // マネージャー側での UUIDv7 採番実装
        public override Guid _GenerateDataID() {
            return RyLibEntity.GenerateUuidV7();
        }
    }
}