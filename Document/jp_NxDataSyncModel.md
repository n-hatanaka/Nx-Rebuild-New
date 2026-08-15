#  Nx データ同期モデル（CRUD処理ライン構造）概要

Nx のデータ管理は、  
**「Base（正本CRUDライン）」「Sync（同期CRUDライン）」「Controller（ライン接続点）」**  
の三層構造で設計されている。

この構造により、  
クライアントとサーバー間でデータの整合性を保ちながら、  
双方向同期を安全に実現する。

---

##  1. BaseDataObj（正本CRUDライン）

BaseDataObj は  
**サーバー側の実体データおよびクライアント側インメモリDBの実体**  
を表し、CRUD 処理の正本となる。

- サーバーDBのレコードを保持  
- クライアント側インメモリDBのレコードも保持  
- JSON変換（TblToJson / JsonToTbl）  
- ロック・更新・保存などの正本CRUD処理  
- IBaseDataObj を実装

BaseDataObj は **サーバーとクライアント双方の“正本CRUDライン”の中心**。

---

##  2. SyncBaseDataObj（同期CRUDライン）

SyncBaseDataObj は BaseDataObj を内包する **ラッパー**であり、  
クライアント側での CRUD 操作とサーバー同期を担当する。

- BaseDataObj を保持  
- API 呼び出しで CRUD をサーバーへ伝達  
- サーバーから返却された JSON をローカルへ反映  
- ISyncBaseDataObj を実装

SyncBaseDataObj は **クライアント側の同期CRUDライン**。

---

##  3. BaseDataObjMgr（正本CRUDラインの管理者）

BaseDataObjMgr は BaseDataObj の集合を管理し、  
サーバー側の CRUD 処理を統括する。

- DBからデータを読み込む  
- BaseObj を生成・管理  
- 削除・更新などの正本CRUD処理  
- IBaseDataObjMgr / IsrvBaseDataObjMgr を実装

BaseDataObjMgr は **正本CRUDラインの管理者**。

---

##  4. SyncBaseDataObjMgr（同期CRUDラインの管理者）

SyncBaseDataObjMgr は BaseDataObjMgr を内包し、  
SyncBaseDataObj の集合を管理する。

- BaseMgr を保持  
- SyncObj を生成・管理  
- APIとの同期処理を担当  
- ISyncBaseDataObjMgr を実装

SyncBaseDataObjMgr は **同期CRUDラインの管理者**。

---

##  5. NxDataController（CRUDライン接続点）

NxDataController は Sync 系からの CRUD 要求を受け取り、  
BaseDataObjMgr を通してサーバーの DB にアクセスする。

- SyncObj / SyncMgr からの要求を受ける  
- BaseMgr を使って DB を操作  
- JSON を返して同期ラインへ反映

Controller は **正本ラインと同期ラインを接続するゲート**。

---

##  CRUD処理ラインの流れ（同期サイクル）

1. **UI → SyncObj**  
   クライアント側で CRUD 操作が発生

2. **SyncObj → Controller**  
   API 呼び出しで CRUD 要求を送信

3. **Controller → BaseMgr / BaseObj**  
   サーバー側の正本CRUDラインで処理

4. **Controller → SyncObj（JSON返却）**  
   処理結果を同期ラインへ返す

5. **SyncObj → ローカルDB**  
   JSON をローカルに反映し、同期ラインを更新

この循環により、  
**サーバー側の正本CRUDラインとクライアント側の同期CRUDラインが常に一致する構造**が成立する。

---

##  この構造の特徴

- Base（正本）と Sync（同期）が完全に分離  
- CRUD処理ラインの循環が破綻しない  
- UI は同期ラインを扱うため安全  
- サーバー側は正本ラインのみ扱うため堅牢  
- インターフェースにより型の整合性が保証される  
- 拡張が容易で、他のデータ型にもそのまま適用可能

