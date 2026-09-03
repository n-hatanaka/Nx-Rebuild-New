### NxTypeMapper Added
NxTypeMapper is now available in this repository.  
It is a **universal type‑conversion engine** that absorbs non‑isomorphic type differences  
between client, server, and database layers.  
Detailed documentation is available in the Document folder under “NxTypeMapper README”.

---

# Nx‑Rebuild‑New
A **Divergence Oscillation Model** based on the Nexus UI–DB Transformation Architecture

---

## Introduction
For more than 20 years, enterprise applications have been designed around  
multi‑layered structures such as controllers, services, repositories, DTOs,  
and validation layers.

Nx‑Rebuild‑New does not replace these layers;  
instead, it introduces a modular architecture—  
the Nexus UI–DB Transformation Architecture (hereafter “Nx Architecture”)—  
which enables the client and server to share the **exact same object structure**.

### With this architecture:

- CRUD becomes declarative  
- Schema changes propagate automatically  
- Large‑scale systems maintain structural stability  

---

## Purpose
- To establish a development foundation where individual developers,  
  AI‑assisted workflows, and large teams can all write CRUD  
  using **the same structural model**.
- To simplify distributed system construction and prevent system failures  
  caused by data inconsistencies from multi‑client CRUD operations.

Nx‑Rebuild‑New provides the technical foundation for this goal,  
building the Nx Architecture where DataObj becomes the smallest CRUD unit,  
and offering a working implementation as part of the repository.

---

## What This Architecture Provides

##### DataObj‑centric CRUD
UI, API, and DB share the same structure,  
eliminating the need for DTOs and validation layers.

##### Complete Isomorphism Between Client and Server
Because both sides operate on the same DataObj,  
CRUD structure stabilizes and schema changes propagate automatically.

##### Strong Resistance to Schema Changes
Shared structures are reflected directly on both sides,  
making the system extremely resilient to specification changes.

##### Simplified CRUD Implementation
UI and API code become nearly identical,  
and new entities are created simply by defining a derived class.

##### Entity‑level Modularity
Even very large systems maintain structural integrity  
through strong decoupling.

---

## Usage (Simplified)

1. The client loads the server schema  
   → Builds an in‑memory DB (WASM recommended) with the same schema.

2. Copy the base classes from the Shared folder  
   - BaseDataObj  
   - BaseDataObjMgr

3. Use the synchronization wrappers  
   - SyncBaseDataObj  
   - SyncBaseDataObjMgr

4. On the server, inherit from NxDataController  
   → CRUD/API is completed by defining a derived class.

5. UI directly manipulates DataObj  
   → WASM UI becomes fully isomorphic with the local DB.

---

## Target Users

- Individual developers  
- AI‑driven development workflows  
- Large development teams  
- Enterprise systems with many entities

*“Entity” refers not only to a single record,  
but also to the smallest unit of user input spanning multiple tables.*

---

## Overview of the Nx Architecture (Simplified)

The Nx Architecture is based on the principle that  
**DataObj is the smallest unit of CRUD**.

##### Core Principles
- Direct DB operations occur only through DataObj CRUD  
- Multi‑entity operations are loops over collections of DataObj  
- UI and entity maintain a 1:1 relationship

##### Effects
- Features can be implemented per entity  
- Aggregation and complex logic can be modularized  
- Strong resistance to specification changes  
- CRUD structure stabilizes because client and server share the same model

---

## Advantages of WASM UI

The **Divergence Oscillation Model** of the Nx Architecture  
assumes a local DB (SQLite / WASM FS).  
Using WASM UI enables:

- Full support for both synced and unsynced (user‑local) CRUD  
- Entity abstraction via DataObj / DataObjMgr,  
  enabling **UI abstraction**  
- Safe delegation of entity CRUD to the user  
- Fully functional copy‑and‑paste operations  
- UI base classes that support both synced and unsynced modes  
- Zero intrusion when adding CRUD to existing systems  
- Gradual migration from existing browser UIs (paging‑only mode)

---

### Divergence Oscillation Model (多態性射影モデル)

The Divergence Oscillation Model is an abstraction  
for safely handling differences that arise when  
client and server states **diverge**.

Actual processing consists of:

- Strict mutual exclusion  
- Client‑local CRUD  
- Difference tracking  
- Synchronization (convergence)  
- Reconstruction of the authoritative state  

This prevents data corruption or conflicts  
caused by multiple clients editing and syncing at different times.

---

## Conclusion of the Nx Architecture: Single‑Machine Model

The Nx Architecture allows UI developers and application logic developers  
to treat distributed environments as if they were **a single machine**.

It absorbs distributed‑system failures at the application layer  
(synchronization drift, ordering collapse, retries, partial failures, etc.),  
so UI developers do not need to handle distributed complexity.

Physical‑layer issues such as network outages, DB failures,  
or server downtime are isolated **outside** CRUD processing  
and separated from application logic.

This isolation layer is implemented by Data Handlers  
(BaseDataObj / SyncBaseDataObj).  
BaseDataObj performs CRUD on actual data,  
while SyncBaseDataObj extends it with synchronization features  
(validation, reconnection, authoritative re‑fetch, etc.).

As a result, UI developers can build applications  
as if working with a single machine,  
and network engineers only need to manage  
the limited synchronization logic inside SyncBaseDataObj.
