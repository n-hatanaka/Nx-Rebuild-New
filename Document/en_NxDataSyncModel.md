## **Nx Data Synchronization Model (CRUD Processing Line Architecture)**

Nx manages data using a three‑layer architecture:

**Base (Primary CRUD Line)**

**Sync (Client‑side Synchronization CRUD Line)**

**Controller (CRUD Line Connection Point)**

This structure ensures consistent data between client and server and enables safe bidirectional synchronization.

---

## **1. BaseDataObj (Primary CRUD Line)**

BaseDataObj represents

**the actual data stored on the server and the actual data stored in the client‑side in‑memory database**,

and serves as the primary source for all CRUD operations.

- Holds server‑side database records
- Holds client‑side in‑memory database records
- Converts between JSON and table formats (TblToJson / JsonToTbl)
- Handles locking, updating, saving, and other primary CRUD operations
- Implements `IBaseDataObj`

BaseDataObj is the **core of the primary CRUD line** on both server and client.

---

## **2. SyncBaseDataObj (Synchronization CRUD Line)**n

SyncBaseDataObj is a **wrapper** around BaseDataObj and handles client‑side CRUD operations and server synchronization.

- Holds BaseDataObj
- Sends CRUD requests to the server via API
- Applies returned JSON to the local database
- Implements `ISyncBaseDataObj`

SyncBaseDataObj represents the **client‑side synchronization CRUD line**.

---

## **3. BaseDataObjMgr (Manager of the Primary CRUD Line)**

BaseDataObjMgr manages collections of BaseDataObj and coordinates server‑side CRUD operations.

- Loads data from the database
- Creates and manages BaseDataObj instances
- Performs delete, update, and other primary CRUD operations
- Implements `IBaseDataObjMgr` and `IsrvBaseDataObjMgr`

BaseDataObjMgr is the **manager of the primary CRUD line**.

---

## **4. SyncBaseDataObjMgr (Manager of the Synchronization CRUD Line)**

SyncBaseDataObjMgr contains BaseDataObjMgr and manages collections of SyncBaseDataObj.

- Holds BaseDataObjMgr
- Creates and manages SyncBaseDataObj instances
- Handles API‑based synchronization
- Implements `ISyncBaseDataObjMgr`

SyncBaseDataObjMgr is the **manager of the synchronization CRUD line**.

---

## **5. NxDataController (CRUD Line Connection Point)**

NxDataController receives CRUD requests from the Sync layer and accesses the server database through BaseDataObjMgr.

- Receives requests from SyncObj / SyncMgr
- Uses BaseDataObjMgr to operate on the database
- Returns JSON to update the synchronization line

The controller acts as the **gateway connecting the primary CRUD line and the synchronization CRUD line**.

---

## **CRUD Processing Line Flow (Synchronization Cycle)**

1. **UI → SyncObj**  
   Client‑side CRUD operation occurs

2. **SyncObj → Controller**  
   CRUD request is sent via API

3. **Controller → BaseMgr / BaseObj**  
   Server‑side primary CRUD line processes the request

4. **Controller → SyncObj (JSON Response)**  
   Result is returned to the synchronization line

5. **SyncObj → Local DB**  
   JSON is applied to the client‑side in‑memory database

This cycle ensures that **the server’s primary CRUD line and the client’s synchronization CRUD line remain consistent at all times**.

---

## **Characteristics of This Architecture**

- Base (primary) and Sync (synchronization) layers are fully separated
- CRUD processing cycles never break
- UI interacts only with the synchronization line, ensuring safety
- Server handles only the primary line, ensuring robustness
- Interfaces guarantee type consistency
- Easily extensible to other data types
