```mermaid
classDiagram
    %% Interfaces
    class IBaseDataObj
    class ISyncBaseDataObj
    class IBaseDataObjMgr
    class IsrvBaseDataObjMgr
    class ISyncBaseDataObjMgr

    %% Interface inheritance
    IBaseDataObj <|-- ISyncBaseDataObj : inherits
    IBaseDataObjMgr <|-- ISyncBaseDataObjMgr : inherits

    %% Implementation classes
    class BaseDataObj
    class SyncBaseDataObj
    class SyncBaseDataObjMgr
    class BaseDataObjMgr
    class NxDataController

    %% Implementation relations
    IBaseDataObj <|.. BaseDataObj : implements
    ISyncBaseDataObj <|.. SyncBaseDataObj : implements
    IBaseDataObjMgr <|.. BaseDataObjMgr : implements
    IsrvBaseDataObjMgr <|.. BaseDataObjMgr : implements
    ISyncBaseDataObjMgr <|.. SyncBaseDataObjMgr : implements

    %% Composition relations
    SyncBaseDataObj o-- BaseDataObj : holds
    SyncBaseDataObjMgr o-- BaseDataObjMgr : holds

    %% Management relations
    BaseDataObjMgr o-- BaseDataObj : manages
    SyncBaseDataObjMgr o-- SyncBaseDataObj : manages

    %% --- Added cyclic structure ---
    %% Sync layer → HttpController
    SyncBaseDataObj --> NxDataController : sync request
    SyncBaseDataObjMgr --> NxDataController : sync request

    %% HttpController → DataObjMgr
    NxDataController o-- BaseDataObjMgr : holds
    NxDataController --> IsrvBaseDataObjMgr : data operation / API
    NxDataController --> BaseDataObj : data operation / API
    
    %% Notes (commented out)

    %%note for IBaseDataObj "Interface to treat BaseDataObj and SyncBaseDataObjMgr as the same type.<br/>Provides UI abstraction by accessing objects through this interface."
    %%note for IBaseDataObjMgr "Interface to treat BaseDataObjMgr and SyncBaseDataObjMgr as the same type.<br/>Provides UI abstraction by accessing objects through this interface."
    %%note for SyncBaseDataObj "Wrapper class of BaseDataObj.<br/>Provides synchronization functionality with the server."
    %%note for SyncBaseDataObjMgr "Wrapper class of BaseDataObjMgr.<br/>Calls SyncBaseDataObj methods and handles synchronization with the server."
    %%note for ISyncBaseDataObj "By inheriting IBaseDataObj, SyncBaseDataObj can be treated as the same type."
    %%note for ISyncBaseDataObjMgr "By inheriting IBaseDataObjMgr, SyncBaseDataObjMgr can be treated as the same type."
    %%note for NxDataController "Receives sync requests and accesses the server DB through IsrvBaseDataObjMgr and BaseDataObjMgr."
```

## Base layer (primary CRUD line)

### BaseDataObj
- Performs CRUD operations on individual entities.

### BaseDataObjMgr
- Manages the lifecycle of BaseDataObj.
- Performs CRUD operations on multiple data entries through BaseDataObj.


## Sync layer (synchronized CRUD line)

### SyncBaseDataObj
- Wrapper class of BaseDataObj.
- Provides synchronization functionality with the server.

### SyncBaseDataObjMgr
- Wrapper class of BaseDataObjMgr.
- Uses SyncBaseDataObj to handle synchronization.


## UI abstraction interfaces (ensuring Sync → Base type equivalence)

### ISyncBaseDataObj
- By inheriting IBaseDataObj, SyncBaseDataObj can be treated as the same type.

### ISyncBaseDataObjMgr
- By inheriting IBaseDataObjMgr, SyncBaseDataObjMgr can be treated as the same type.

### IBaseDataObj
- Provides client-side implementation.
- Interface that allows BaseDataObj and SyncBaseDataObj to be treated as the same type.
- Enables UI abstraction by accessing objects through this interface.

### IBaseDataObjMgr
- Provides client-side implementation to the UI.
- Interface that allows BaseDataObjMgr and SyncBaseDataObjMgr to be treated as the same type.
- Enables UI abstraction by accessing objects through this interface.


## Server-side interface

### IsrvBaseDataObjMgr
- Interface for server-side access to BaseDataObjMgr.
- Provides server-side implementation to the API.


## Controller (CRUD line connection point)

### NxDataController
- Receives sync requests.
- Accesses the server DB through IsrvBaseDataObjMgr and BaseDataObjMgr.
