# Diagrams

## 1. Client / Server Structural Symmetry  
*Basic structure of the Nexus UI–DB Transformation Model*

```
+-------------------------------+            +----------------------+
|            Client             |            |        Server        |
| (WASM / Browser / InMemoryDB) |            |    (API / Database)  |
+-------------------------------+            +----------------------+
|  DataObj                      | <--------> |  DataObj             |
|  DataObjMgr                   | <--------> |  DataObjMgr          |
+-------------------------------+            +----------------------+

Because both sides share the same object structure,
CRUD logic becomes nearly identical on client and server.
```

---

## 2. Worldline Divergence (Occurs when editing begins)

```
                Client (Local DB)
                --------------------------------
                Pre‑edit State (Synchronized)
                       |
                       |  Divergence occurs the moment the user begins editing
                       v
                Editing State (Unsynchronized)
                       |
                       |  Merges back through synchronization
                       v
                Synchronized State
                --------------------------------
                Server (Authoritative Data)
```

The worldline divergence point is **“the moment the user begins editing.”**  
Synchronization later merges the client state back into the server’s authoritative state.

---

## 3. Worldline Processing Flow

```
+------------------+
| User Editing     |
+------------------+
          |
          v
+------------------+
| Local Save       |
| (Unsynchronized) |
+------------------+
          |
          v
+------------------+
| Synchronization  |
| (Local DB + API) |
| *If editing is canceled or sync fails,   |
|   the local DB can be discarded to reset |
+------------------+
          |
          v
+------------------+
| Server Merge     |
| (Authoritative)  |
+------------------+
```

---

## 4. Worldline Operation in a WASM UI

```
+---------------------------+
| WASM UI                   |
| Local SQLite / WASM FS   |
+---------------------------+
          |
          v
+---------------------------+
| DataObj / DataObjMgr     |
| (Local CRUD)             |
+---------------------------+
          |
          v
+---------------------------+
| SyncBaseDataObj / Mgr    |
| (API Synchronization)    |
+---------------------------+
```

---

## 5. Integration into an Existing System (Post‑WASM Modernization)

```
+---------------------------+------------------------------+
|     Existing Browser UI   |     New Entity CRUD UI       |
|   (Paging + Read‑only)    |   (Nx Architecture / WASM)   |
+---------------------------+------------------------------+
          |                               |
          v                               v
+---------------------------+------------------------------+
|     Existing API          |     API (NxDataController)   |
+---------------------------+------------------------------+
          |                               |
          v                               v
+----------------------------------------------------------+
|                         Database                         |
+----------------------------------------------------------+
```

The existing UI and the Nx‑based UI operate side‑by‑side,  
sharing the same database — a true **bolt‑on architecture**.

---