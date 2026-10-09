# Nx‑Rebuild‑New  
Implementation example of the **Divergence Oscillation Model**  
based on the Nexus UI–DB Transformation Architecture

---

## License

**This project is provided under the Nexus Transformation License 1.0  
(Non-Commercial & Commercial Dual License).  
Commercial use requires a separate commercial license agreement.  
Please refer to the LICENSE file for details.**

This repository uses the open data published by the Ministry of Education, Culture, Sports, Science and Technology (MEXT) in Japan:  
“Standard Tables of Food Composition in Japan.”  
No commercial datasets are included.

---

## NxTypeMapper Added

This repository now supports **NxTypeMapper**.  
NxTypeMapper is a **universal type conversion engine** that absorbs  
type mismatches (non-isomorphic types) occurring between  
client, server, and database environments.

Detailed documentation is available in `/Document/NxTypeMapper README`.

---

# What is Nx‑Rebuild‑New?

This repository is an implementation example that incorporates the new  
web system development foundation **Nx Architecture**.

Nx Architecture does not replace existing enterprise web architectures  
(Controller / Service / Repository / DTO / Validation layers, etc.).  
Instead, it is an **abstract architecture that can be added as a module  
on top of existing structures**.

---

## Key Feature of Nx Architecture

### **The client and server can share *exactly the same object structure*.**

This enables:

- Declarative CRUD  
- Automatic propagation of schema changes  
- Structural stability even in large-scale systems  

---

## Purpose

- To establish a development foundation where  
  **individual developers, AI-assisted workflows, and large teams  
  can write CRUD using the same structure**  
- To simplify distributed system development and prevent  
  **data inconsistencies and system failures** caused by CRUD  
  across multiple clients

Nx‑Rebuild‑New is developed as an implementation example of Nx Architecture,  
where **BaseDataObj** is the smallest CRUD unit.

---

# What Nx Architecture Provides

### ✔ DataObj-centered CRUD  
UI, API, and DB share the same structure, eliminating DTOs and validation layers.

### ✔ Complete isomorphism between client and server  
CRUD structure remains stable, and schema changes propagate automatically.

### ✔ Strong resistance to schema changes  
Shared structures are reflected on both sides, making the design highly robust.

### ✔ Simplified CRUD implementation  
UI and API code become nearly identical.  
New entities are completed simply by creating derived classes.

### ✔ Modularization at the entity level  
Even huge systems maintain structural stability and loose coupling.

---

# Usage (Quick Guide)

1. Load the server schema on the client  
   → Build an in-memory DB (WASM recommended) with the same schema

2. Copy the base classes from the `/Shared` folder  
   - BaseDataObj  
   - BaseDataObjMgr

3. Use synchronization wrappers  
   - SyncBaseDataObj  
   - SyncBaseDataObjMgr

4. On the server, inherit from NxDataController  
   → CRUD/API is completed by creating derived classes

5. UI directly handles DataObj  
   → WASM UI achieves complete isomorphism with the local DB

---

# Target Users

- Individual developers  
- AI-driven development workflows  
- Large-scale development teams  
- Enterprise systems (many entities)

※ “Entity” refers not only to single records but also  
**the smallest unit of user input spanning multiple tables**.

---

# Overview of Nx Architecture (Simplified)

Nx Architecture is based on the principle:

**“BaseDataObj operates as the smallest CRUD unit.”**

### Core Principles

- Direct DB operations occur **only through BaseDataObj CRUD**  
- Multi-entity operations are  
  **loops over collections of BaseDataObj**  
- UI and “user input entities” have a 1:1 relationship  
  (user input entities = smallest input units spanning multiple tables)

### Effects

- Functionality can be implemented per entity  
- Complex processing and aggregation can be modularized  
- Strong resistance to specification changes  
- CRUD structure remains stable because client and server share the same model

---

# Benefits of UIs with Local DB (WASM / Desktop Applications)

Nx Architecture’s **Divergence Oscillation Model** assumes  
a local DB (SQLite / WASM FS / native storage).  
Therefore, in WASM UIs or desktop applications  
(Electron / WPF / WinUI / Qt / Flutter Desktop, etc.),  
the following properties hold:

- Nx Architecture can be used regardless of runtime environment  
- Full CRUD operation with or without synchronization  
- Entity abstraction via BaseDataObj / BaseDataObjMgr  
- UI abstraction that handles **collections of multiple entities**  
- Safe client-side autonomous CRUD for entities  
- Easy implementation of copy/paste operations  
- UI base classes can handle both synchronized and unsynchronized modes  
  (input screens per entity type can also be abstracted)  
- Zero intrusion into existing systems—new entity CRUD can be bolted on  
- Browser UIs can transition gradually by treating existing UI as “paging-only”  
- Desktop environments provide faster local CRUD than WASM  
- Large-scale entities can be safely handled locally  

---

# Divergence Oscillation Model

The Divergence Oscillation Model is an abstract model  
for safely handling differences when client and server states  
**diverge**.

In practice:

- Multiple computers share the same schema  
- Each computer treats entities as autonomous objects  
- They maintain and synchronize partial or full snapshots  
- Consistency is preserved  
- System failure is avoided through structured convergence

Each computer forms multiple **projections**:

- UI layer  
- Work layer  
- Storage layer  
- Master layer  

By managing differences, oscillations, and convergence between projections,  
the system behaves as a **single coherent computer**  
even in distributed environments.

This structure is supported by:

- BaseDataObj  
- BaseDataObjMgr  
- Sync wrappers  
- Homomorphic interface structures  

---

# Conclusion of Nx Architecture: Single-Computer Model

Nx Architecture provides an abstraction foundation  
that allows UI developers and application logic developers  
to treat distributed environments as a **single computer**.

Distributed failures (sync drift, ordering collapse, retries, partial failures)  
are absorbed by BaseDataObj / SyncBaseDataObj,  
so UI developers do not need to handle distributed complexity.

Network outages, DB failures, and server downtime  
are isolated outside CRUD processing  
and separated from application logic.

As a result:

- UI developers can build applications as if handling a single computer  
- Network engineers only need to work within the limited scope  
  of SyncBaseDataObj  

This structural separation is the core of Nx Architecture.

