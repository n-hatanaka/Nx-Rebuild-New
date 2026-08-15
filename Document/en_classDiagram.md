```mermaid

classDiagram

&#x20;   %% Interfaces

&#x20;   class IBaseDataObj

&#x20;   class ISyncBaseDataObj

&#x20;   class IBaseDataObjMgr

&#x20;   class IsrvBaseDataObjMgr

&#x20;   class ISyncBaseDataObjMgr



&#x20;   %% Interface inheritance

&#x20;   IBaseDataObj <|-- ISyncBaseDataObj : inherits

&#x20;   IBaseDataObjMgr <|-- ISyncBaseDataObjMgr : inherits



&#x20;   %% Concrete classes

&#x20;   class BaseDataObj

&#x20;   class SyncBaseDataObj

&#x20;   class SyncBaseDataObjMgr

&#x20;   class BaseDataObjMgr

&#x20;   class NxDataController



&#x20;   %% Implementation relationships

&#x20;   IBaseDataObj <|.. BaseDataObj : implements

&#x20;   ISyncBaseDataObj <|.. SyncBaseDataObj : implements

&#x20;   IBaseDataObjMgr <|.. BaseDataObjMgr : implements

&#x20;   IsrvBaseDataObjMgr <|.. BaseDataObjMgr : implements

&#x20;   ISyncBaseDataObjMgr <|.. SyncBaseDataObjMgr : implements



&#x20;   %% Composition relationships

&#x20;   SyncBaseDataObj o-- BaseDataObj : holds

&#x20;   SyncBaseDataObjMgr o-- BaseDataObjMgr : holds



&#x20;   %% Management relationships

&#x20;   BaseDataObjMgr o-- BaseDataObj : manages

&#x20;   SyncBaseDataObjMgr o-- SyncBaseDataObj : manages



&#x20;   %% --- Worldline flow (synchronization cycle) ---

&#x20;   %% Sync layer → HttpController

&#x20;   SyncBaseDataObj --> NxDataController : sync request

&#x20;   SyncBaseDataObjMgr --> NxDataController : sync request



&#x20;   %% HttpController → DataObjMgr

&#x20;   NxDataController --> IsrvBaseDataObjMgr : data operations / API handling

&#x20;   NxDataController --> BaseDataObjMgr : data operations / API handling

&#x20;   

&#x20;   %% Notes

&#x20;   note for SyncBaseDataObj "Wrapper class for BaseDataObj.<br/>Provides synchronization features with the server."

&#x20;   note for SyncBaseDataObjMgr "Wrapper class for BaseDataObjMgr.<br/>Invokes SyncBaseDataObj methods and handles server synchronization."

&#x20;   note for ISyncBaseDataObj "Extends IBaseDataObj so SyncBaseDataObj can be treated as the same type."

&#x20;   note for ISyncBaseDataObjMgr "Extends IBaseDataObjMgr so SyncBaseDataObjMgr can be treated as the same type."

&#x20;   note for NxDataController "Receives sync requests from Sync layer.<br/>Accesses server DB through IsrvBaseDataObjMgr and BaseDataObjMgr."

```

