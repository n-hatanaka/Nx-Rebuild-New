### NxTypeMapper Added
NxTypeMapper is now available in this repository.  
It is a **general-purpose type conversion engine** that absorbs type non-isomorphism occurring between client, server, and database.  
Detailed documentation is available in the “NxTypeMapper README” inside the Document folder.

---

# Nx‑Rebuild‑New
Worldline Branching Model based on the Nexus UI–DB Transformation Architecture

---

## Introduction
For more than 20 years, enterprise applications have been designed on the assumption of multi-layered structures such as  
controllers, services, repositories, DTOs, and validation layers.

Nx‑Rebuild‑New does not replace these layers directly.  
Instead, it introduces a completely new structural model — the **Nexus UI–DB Transformation Architecture** (hereafter Nx Architecture),  
in which the client and server share the same object structure.

This architecture enables:

- Declarative CRUD  
- Automatic propagation of schema changes  
- Structural stability even in large-scale systems  

---

## Purpose
- To establish a development foundation where individual developers, AI-assisted workflows, and large teams can all write CRUD using the same structure.  
- To simplify distributed system construction and prevent system failures caused by data inconsistencies originating from CRUD operations across multiple clients.

Nx‑Rebuild‑New is developed as an implementation example of the Nx Architecture,  
which defines the data handler object (DataObj) as the smallest unit of CRUD.  
This repository provides the technical foundation for that architecture.

---

## What This Architecture Provides

##### DataObj-Centric CRUD
UI, API, and DB share the same structure,  
eliminating the need for DTOs and validation layers.

##### Complete Isomorphism Between Client and Server
Because both sides handle the same DataObj,  
CRUD structure remains stable and schema changes propagate automatically.

##### Strong Resistance to Schema Changes
Shared structures are reflected on both sides as-is,  
resulting in extremely robust design against specification changes.

##### Simplified CRUD Implementation
UI and API code become nearly identical,  
and new entities are completed simply by creating derived classes.

##### Entity-Level Modularity
Even very large systems maintain structural stability through loose coupling.

---

## Usage (Simplified)

1. Load the server schema on the client side  
   → Build an in-memory DB with the same schema (WASM recommended).

2. Copy the base classes from the Shared folder  
   - BaseDataObj  
   - BaseDataObjMgr

3. Use the synchronization wrappers  
   - SyncBaseDataObj  
   - SyncBaseDataObjMgr

4. On the server side, inherit from NxDataController  
   → CRUD/API is completed simply by creating derived classes.

5. UI directly handles DataObj  
   → WASM UI achieves complete isomorphism with the local DB.

---

## Target Users

- Individual developers  
- AI-driven development workflows  
- Large-scale development teams  
- Enterprise systems (many entities)

*“Entity” refers not only to a single record,  
but also to the smallest unit of user input spanning multiple tables.*

---

## Overview of the Nx Architecture (Simplified)

The Nx Architecture is based on the principle that  
**“DataObj operates as the smallest unit of CRUD.”**

##### Basic Principles
- Direct DB operations occur only through CRUD performed by DataObj  
- Multi-entity operations are loops over collections of DataObj  
- UI and entities maintain a 1:1 relationship

##### Resulting Benefits
- Features can be implemented at the entity level  
- Aggregation and complex logic can be modularized  
- Strong resistance to specification changes  
- CRUD structure remains stable because client and server share the same model

---

## Benefits of WASM UI

The worldline branching model of the Nx Architecture assumes a local DB (SQLite / WASM FS).  
Using a WASM UI enables:

- Complete operation of both synchronized and unsynchronized worldlines (user-local CRUD)  
- Entity abstraction via DataObj / DataObjMgr, and the **UI abstraction enabled by it**  
- Safe delegation of entity management (CRUD) to the user  
- Fully functional copy/paste operations  
- UI base classes that handle both synchronized and unsynchronized modes  
- Zero intrusion when attaching CRUD to existing systems  
- Gradual migration where existing browser UI remains “paging-only”

---

### Worldline Branching Model (Worldline Model)

The worldline branching model is an abstraction for safely handling  
differences that arise when the states of the client and server “branch.”

Actual processing includes:

- Strict mutual exclusion  
- Client-local CRUD  
- Difference management  
- Synchronization  
- Reconstruction of the authoritative copy  

These mechanisms prevent data corruption or conflicts caused by  
multiple clients editing and synchronizing at different timings.

---

## Conclusion of the Nx Architecture: Single-Machine Model

The Nx Architecture provides an abstraction layer that allows  
UI developers and application logic developers to treat distributed environments  
as **a single logical machine**.

The Nx Architecture absorbs distributed-system failures at the application layer  
(synchronization drift, ordering collapse, retransmission, partial failure, etc.),  
allowing UI developers to avoid dealing with distributed complexity.

Network outages, DB hardware failures, and server downtime  
are isolated outside CRUD processing and treated as separate from application logic.

This isolation layer is composed of data handlers (BaseDataObj / SyncBaseDataObj).  
BaseDataObj performs CRUD on actual data,  
while SyncBaseDataObj extends BaseDataObj and handles distributed-system arise when the states of the client and server “branch.”

Actual processing includes:

- Strict mutual exclusion  
- Client-local CRUD  
- Difference management  
- Synchronization  
- Reconstruction of the authoritative copy  

These mechanisms prevent data corruption or conflicts caused by  
multiple clients editing and synchronizing at different timings.

---

## Conclusion of the Nx Architecture: Single-Machine Model

The Nx Architecture provides an abstraction layer that allows  
UI developers and application logic developers to treat distributed environments  
as **a single logical machine**.

The Nx Architecture absorbs distributed-system failures at the application layer  
(synchronization drift, ordering collapse, retransmission, partial failure, etc.),  
allowing UI developers to avoid dealing with distributed complexity.

Network outages, DB hardware failures, and server downtime  
are isolated outside CRUD processing and treated as separate from application logic.

This isolation layer is composed of data handlers (BaseDataObj / SyncBaseDataObj).  
BaseDataObj performs CRUD on actual data,  
while SyncBaseDataObj extends BaseDataObj and handles distributed-system failures  
(validation, reconnection, authoritative copy retrieval, etc.).

As a result, UI developers can build applications as if handling a single machine,  
and network engineers only need to work within the limited scope of SyncBaseDataObj.

---
