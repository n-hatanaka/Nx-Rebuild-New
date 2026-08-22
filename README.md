# Nx‑Rebuild‑New  
**A Synchronization Worldline Architecture Based on the Nexus UI–DB Transformation Model**

## Introduction
For more than 20 years, enterprise applications have been built around layered patterns—  
controllers, services, repositories, DTOs, validators, and the glue code connecting them.

Nx‑Rebuild‑New does not “replace” these traditional architectures.  
Instead, it introduces a completely new structural model—the **Nexus UI–DB Transformation Model**,  
where the client and server share the exact same object structure.

This model enables:

- Declarative CRUD  
- Automatic propagation of schema changes  
- Structural stability even in large‑scale systems  

---

## Purpose
To enable individual developers, AI‑assisted workflows, and large teams to build systems where  
**CRUD operations are written using the same structure on both client and server.**

Nx‑Rebuild‑New achieves this by providing  
**a worldline architecture where DataObj acts as the smallest CRUD unit.**

---

## What This Architecture Provides

### DataObj‑centric CRUD
Each entity is represented as a DataObj,  
shared across UI, API, and database layers.

### Full structural symmetry between client and server
Both sides operate on the same DataObj structure,  
eliminating the need for DTOs or validation layers.

### Strong resilience to schema changes
Shared structures propagate automatically,  
making the system highly tolerant to specification updates.

### Simplified CRUD implementation
UI and API code become nearly identical.  
Adding a new entity requires only creating derived classes.

### Entity‑level modularization
Large systems maintain structural stability.  
New entities can be added to existing systems with zero intrusion—  
a true “bolt‑on” architecture.

---

## How to Use (Simplified)

1. **Client loads the schema from the server**  
   → Builds an in‑memory database with the same schema (WASM recommended).

2. **Copy foundational classes from the Shared folder**  
   - BaseDataObj  
   - BaseDataObjMgr

3. **Use synchronization wrappers on the client side**  
   - SyncBaseDataObj  
   - SyncBaseDataObjMgr

4. **On the server side, derive your controllers from NxDataController**  
   → CRUD/API is completed simply by defining entity‑specific derived classes.

5. **UI directly operates on DataObj**  
   → With a WASM UI, the local DB becomes fully isomorphic to the server,  
      enabling both synchronous and asynchronous worldlines to function seamlessly.

---

## Target Users

- Individual developers  
- AI‑driven development workflows  
- Large‑scale teams  
- Systems handling a large number of entities  

*Here, “entity” refers not only to a single record,  
but to the smallest unit of user input that spans multiple tables.*

---

## Overview of the Worldline Architecture (Simplified)

Nx‑Rebuild‑New is based on the principle:

> **“DataObj performs CRUD as the smallest operational unit.”**

### Core Principles

- Direct DB operations are restricted to DataObj CRUD  
- Multi‑entity operations are expressed as loops over collections of DataObj  
- UI and entities maintain a 1:1 relationship  

### Resulting Benefits

- Entity‑level feature implementation becomes straightforward  
- Aggregation and complex logic can be separated into dedicated modules  
- Highly resilient to specification changes and feature expansion  
- Client and server share an identical object model,  
  ensuring structural stability across CRUD operations  

All of this is based on the **Nexus UI–DB Transformation Architecture**.

---

## Benefits of Using a WASM UI

Nx’s worldline architecture assumes the presence of a local database  
(e.g., SQLite / WASM FS).  
When using a WASM UI, the following become possible:

- Full operation of synchronous and asynchronous worldlines  
- Complete structural symmetry of DataObj / DataObjMgr between client and server  
- Full support for copy‑and‑paste operations between synced and unsynced data  
- UI base classes can be designed to support both synchronized and unsynchronized DataObj types  
- New CRUD modules can be added to existing systems with zero intrusion  

Existing non‑WASM browser UIs can remain as  
**“paging + read‑only views”**,  
allowing gradual modernization without breaking the legacy system.

---

## Summary

Nx‑Rebuild‑New provides  
**a synchronization worldline architecture that unifies UI and DB structures,  
allowing client and server to operate on the same CRUD model.**

- New CRUD modules run fully on WASM using Nx worldlines  
- Existing UIs remain as paging‑only views  
- New entities can be added without modifying the existing system  
- Schema changes propagate safely  
- Large systems maintain structural stability  

---

## What Is a Worldline (Worldline Model)?

A **worldline** refers to the point where the client’s edited state  
and the server’s authoritative state **diverge**.

It is a conceptual model for safely handling these state differences.

Actual processing is implemented through:

- strict concurrency control  
- difference tracking  
- synchronization  
- (including local database operations)

This ensures that even when editing and synchronization occur at different times,  
**no data corruption or conflicts occur.**
