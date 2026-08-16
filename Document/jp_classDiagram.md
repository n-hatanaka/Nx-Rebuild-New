```mermaid
classDiagram
    %% インターフェース
    class IBaseDataObj
    class ISyncBaseDataObj
    class IBaseDataObjMgr
    class IsrvBaseDataObjMgr
    class ISyncBaseDataObjMgr

    %% インターフェース継承
    IBaseDataObj <|-- ISyncBaseDataObj : 継承
    IBaseDataObjMgr <|-- ISyncBaseDataObjMgr : 継承

    %% 実装クラス
    class BaseDataObj
    class SyncBaseDataObj
    class SyncBaseDataObjMgr
    class BaseDataObjMgr
    class NxDataController

    %% 実装関係
    IBaseDataObj <|.. BaseDataObj : 実装
    ISyncBaseDataObj <|.. SyncBaseDataObj : 実装
    IBaseDataObjMgr <|.. BaseDataObjMgr : 実装
    IsrvBaseDataObjMgr <|.. BaseDataObjMgr : 実装
    ISyncBaseDataObjMgr <|.. SyncBaseDataObjMgr : 実装

    %% コンポジション関係（右側はクラス名にする）
    SyncBaseDataObj o-- BaseDataObj : 保持
    SyncBaseDataObjMgr o-- BaseDataObjMgr : 保持

    %% 管理関係（右側もクラス名にする）
    BaseDataObjMgr o-- BaseDataObj : 管理
    SyncBaseDataObjMgr o-- SyncBaseDataObj : 管理

    %% --- 追加した循環構造 ---
    %% Sync系 → HttpController
    SyncBaseDataObj --> NxDataController : 同期要求
    SyncBaseDataObjMgr --> NxDataController : 同期要求

    %% HttpController → DataObjMgr
    NxDataController o-- BaseDataObjMgr : 保持
    NxDataController --> IsrvBaseDataObjMgr : データ操作/API処理
    NxDataController --> BaseDataObj : データ操作/API処理
    
    %% 注釈

    %%note for IBaseDataObj "BaseDataObjとSyncBaseDataObjMgrを<br/>同型として扱うインターフェース。<br/>これを経由してオブジェクトへアクセスすることで<br/>UIの抽象化が可能になる。"
    %%note for IBaseDataObjMgr "BaseDataObjとSyncBaseDataObjMgrを<br/>同型として扱うインターフェース。<br/>これを経由してオブジェクトへアクセスすることで<br/>UIの抽象化が可能になる。"
    %%note for SyncBaseDataObj "BaseDataObjのラッパークラス。<br/>サーバーとの同期機能を付与する。"
    %%note for SyncBaseDataObjMgr "BaseDataObjMgrのラッパークラス。<br/>SyncBaseDataObjのメソッドを呼び出し<br/>サーバーとの同期機能を担う。"
    %%note for ISyncBaseDataObj "IBaseDataObjを継承することで、<br/>SyncBaseDataObjを同一の型として扱える。"
    %%note for ISyncBaseDataObjMgr "IBaseDataObjMgrを継承することで、<br/>SyncBaseDataObjMgrを同一の型として扱える。"
    %%note for NxDataController "Sync系からの要求を受け取り<br/>IsrvBaseDataObjMgr及びBaseDataObjを通してサーバーのDBへアクセスする。"
```

## Base 系（正本 CRUD ライン）

### BaseDataObj
- エンティティ単位の CRUD を行う。

### BaseDataObjMgr
- BaseDataObj のライフサイクルを管理する。
- BaseDataObj を介して複数データの CRUD を行う。


## Sync 系（同期 CRUD ライン）

### SyncBaseDataObj
- BaseDataObj のラッパークラス。
- サーバーとの同期機能を付与する。

### SyncBaseDataObjMgr
- BaseDataObjMgr のラッパークラス。
- SyncBaseDataObj を用いて同期処理を担う。


## UI 抽象化インターフェース（Sync → Base の同型性を保証）

### ISyncBaseDataObj
- IBaseDataObj を継承することで SyncBaseDataObj を同一型として扱える。

### ISyncBaseDataObjMgr
- IBaseDataObjMgr を継承することで SyncBaseDataObjMgr を同一型として扱える。

### IBaseDataObj
- クライアント向けの実装を提供する。
- BaseDataObj と SyncBaseDataObj を同型として扱うためのインターフェース。
- UI 抽象化を可能にする。

### IBaseDataObjMgr
- クライアント向けの実装を UI へ提供する。
- BaseDataObjMgr と SyncBaseDataObjMgr を同型として扱うためのインターフェース。
- UI 抽象化を可能にする。


## サーバー側インターフェース

### IsrvBaseDataObjMgr
- サーバー向け BaseDataObjMgr アクセス用インターフェース。
- サーバー向けの実装を API へ提供する。


## Controller（CRUD ライン接続点）

### NxDataController
- Sync 系からの要求を受け取る。
- IsrvBaseDataObjMgr および BaseDataObjMgr を通してサーバー DB へアクセスする。
