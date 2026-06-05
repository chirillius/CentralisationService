# CentralServer Product Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-ready centralized video analytics product based on the current `CentralisationService` skeleton and the domain logic of the original Tobacco platform.

**Architecture:** The final system uses `Client -> CentralServer -> Server -> camera` and `CentralServer -> Neuro`. `Server` stays weak and transport-only on each site, while `CentralServer` owns catalog, archive, analytics orchestration, incidents, users, permissions, reporting, and all future AI pipelines.

**Tech Stack:** React/Vite client, ASP.NET Core for `Server` and `CentralServer`, ASP.NET Core/Python-adjacent AI service patterns for `Neuro`, FFmpeg for frame capture, future relational DB + object/file storage, future background jobs and inference pipelines.

---

## Source Systems To Reuse

- Current worktree:
  - `Client`: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Client`
  - `Server`: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Server/Server`
  - `CentralServer`: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer`
  - `Neuro`: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Neuro/Neuro`
- Original systems to mine for behavior, DTOs, naming, screens, and business rules:
  - `CentralServerWeb`: `/Users/chirill/Работа/webService/CentralServerWeb`
  - `TobaccoServer`: `/Users/chirill/Работа/webService/TobaccoServer`
  - `TobaccoEntitiesWeb`: `/Users/chirill/Работа/webService/TobaccoEntitiesWeb`
  - `neuro`: `/Users/chirill/Работа/webService/neuro`

## Product Decomposition

This product is too large to implement safely as one feature. It should be built as twelve working phases:

1. Platform foundation
2. Central catalog and configuration
3. Live camera access
4. Archive and storage
5. Detection profiles and schedules
6. Neuro integration
7. Incidents and evidence
8. Client operator workflows
9. Multi-tenant and permissions
10. Reporting and statistics
11. Operations and hardening
12. Migration from original platform

## Phase 0: Freeze The Core Architecture

**Goal:** Make the architecture irreversible: all analytics live on `CentralServer`, all sites use weak `Server`.

**Deliverables**

- [ ] Keep `Server` transport-only: camera discovery, frame capture, metadata endpoints
- [ ] Keep `CentralServer` as the single place for motion detection, archive, incidents, AI orchestration
- [ ] Keep `Neuro` callable only from `CentralServer`
- [ ] Keep `Client` callable only against `CentralServer`
- [ ] Preserve the documented rules in:
  - `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/AGENTS.md`
  - `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/docs/processing-rule.md`

**Exit criteria**

- [ ] No code path exists where `Server` performs business analytics
- [ ] No code path exists where `Client` talks directly to `Server`

## Phase 1: Platform Foundation

**Goal:** Stabilize the current skeleton into a reliable developer and deployment baseline.

**Add**

- [ ] Unified configuration model for `Client`, `Server`, `CentralServer`, `Neuro`
- [ ] Environment-specific configs for local, pilot, and production
- [ ] Structured logs with correlation IDs
- [ ] Health endpoints for all services
- [ ] Central error handling format
- [ ] Service startup validation for required config fields

**Reuse from originals**

- [ ] Hosting and controller patterns from `CentralServerWeb` and `TobaccoServer`
- [ ] Existing DTO naming where it matches the domain

**Exit criteria**

- [ ] All four services start with validated config
- [ ] Failures are visible in logs and health endpoints

## Phase 2: Central Catalog And Configuration

**Goal:** Replace `appsettings.json` as the primary runtime catalog with central persistent configuration.

**Add**

- [ ] Core entities:
  - `Company`
  - `Site`
  - `ServerNode`
  - `Camera`
  - `Employee`
  - `Zone`
  - `DetectionType`
  - `DetectionProfile`
  - `Schedule`
- [ ] Persistent DB schema and migrations
- [ ] `CentralServer` APIs for CRUD of companies, stores, servers, cameras, zones, employees
- [ ] `Server` endpoint(s) for camera capability discovery and technical status

**Reuse from originals**

- [ ] Store/server/camera concepts from `CentralServerWeb`
- [ ] Employee/store/camera structures from `TobaccoEntitiesWeb`
- [ ] Existing UI flow “Add server -> check reachability -> save store and cameras” from the old client

**Exit criteria**

- [ ] Stores are not managed via `appsettings.json` anymore
- [ ] A new store can be added from the client and becomes visible after save

## Phase 3: Live Camera Access

**Goal:** Make live preview a stable product feature.

**Add**

- [ ] Camera availability checks
- [ ] Snapshot retry logic and timeouts
- [ ] Centralized camera status cache
- [ ] Stream mode strategy:
  - snapshot polling first
  - optional MJPEG/HLS/WebRTC later if needed
- [ ] Clear camera health statuses: online, unauthorized, unreachable, timeout

**Reuse from originals**

- [ ] Camera endpoint patterns from `TobaccoServer`
- [ ] Old client camera preview behavior and UX

**Exit criteria**

- [ ] Operator can select company -> store -> camera and consistently see current frames
- [ ] Client shows meaningful status for unreachable or unauthorized cameras

## Phase 4: Archive And Storage

**Goal:** Evolve from saved motion JPEGs to a proper evidence and archive subsystem.

**Add**

- [ ] Central archive model:
  - motion frames
  - event evidence frames
  - short clips
  - long-term archive references
- [ ] Storage abstraction for local disk first, object storage later
- [ ] Retention policies by company/store/camera/event type
- [ ] Compression and encoding strategy
- [ ] Archive rebuild/index reload on startup
- [ ] Download endpoints for operators

**Reuse from originals**

- [ ] Video/archive expectations from `TobaccoServer`
- [ ] Existing user flows for archive viewing/downloading from the old client

**Recommended storage direction**

- [ ] Keep JPEG evidence for incidents
- [ ] Keep short MP4/H.264 or H.265 clips for review windows
- [ ] Add retention tiers rather than storing everything at full quality forever

**Exit criteria**

- [ ] Operator can browse archive by store/camera/date/time
- [ ] Evidence survives `CentralServer` restart

## Phase 5: Detection Profiles And Schedules

**Goal:** Centralize all per-store rules that used to live around `TobaccoServer` jobs.

**Add**

- [ ] Detection profile entity per store/camera/zone
- [ ] Time schedules, cooldowns, sensitivity, enable/disable flags
- [ ] Zone/ROI editor in the new client
- [ ] Mapping between camera and enabled detection types
- [ ] Background scheduler in `CentralServer`

**Reuse from originals**

- [ ] Periodic job concepts from `TobaccoServer`
- [ ] Zone and configuration workflows from the old client

**Exit criteria**

- [ ] Different stores can enable different detection types and schedules without code changes

## Phase 6: Neuro Integration

**Goal:** Turn `Neuro` into a real centralized inference backend.

**Add**

- [ ] Stable request/response contracts between `CentralServer` and `Neuro`
- [ ] Input package format:
  - detection type
  - store/site/camera context
  - ROI
  - frame(s) or clip reference
  - thresholds
- [ ] Inference orchestration in `CentralServer`
- [ ] Model routing in `Neuro`
- [ ] Timeout, retry, fallback, and audit logging
- [ ] Versioned model registry

**First detection types to ship**

- [ ] `phone`
- [ ] `smoke`
- [ ] `bottles`
- [ ] `cash-register`
- [ ] `pose`

**Reuse from originals**

- [ ] Defect taxonomy and endpoint ideas from `/Users/chirill/Работа/webService/neuro`
- [ ] Business semantics from `TobaccoServer`

**Exit criteria**

- [ ] `CentralServer` can request at least one real inference and persist the result

## Phase 7: Incidents And Evidence

**Goal:** Build the business outcome layer on top of raw detections.

**Add**

- [ ] Incident model:
  - company
  - store
  - camera
  - detection type
  - timestamp
  - confidence
  - status
  - evidence references
  - review history
- [ ] Incident state machine:
  - new
  - acknowledged
  - false positive
  - confirmed
  - archived
- [ ] Evidence gallery and incident detail page
- [ ] Deduplication and cooldown logic for repeated events

**Reuse from originals**

- [ ] Violation storage concepts from `TobaccoServer`
- [ ] Existing “view incidents by day and type” behavior from the old client

**Exit criteria**

- [ ] Operator can review and manage incidents end-to-end from the new client

## Phase 8: Client Operator Workflows

**Goal:** Reach feature parity for the operator-facing workflows of the old client.

**Add**

- [ ] Authentication entry flow
- [ ] Company/store/camera navigation
- [ ] Camera configuration screens
- [ ] Zone editor
- [ ] Detection profile editor
- [ ] Live view
- [ ] Archive view
- [ ] Incident feed and incident detail
- [ ] Basic statistics pages

**Reuse from originals**

- [ ] Visual layouts, navigation ideas, and operator workflows from the current/old client

**Exit criteria**

- [ ] Main operator work can happen entirely in the new client without falling back to the old UI

## Phase 9: Multi-Tenant And Permissions

**Goal:** Turn the platform into a sellable multi-company product.

**Add**

- [ ] Tenant isolation by `Company`
- [ ] Role model:
  - system admin
  - company admin
  - regional manager
  - store manager
  - reviewer/operator
- [ ] Store-scoped and company-scoped access rules
- [ ] API authorization by tenant and role
- [ ] Tenant-aware storage paths and retention policies

**Reuse from originals**

- [ ] User/store concepts from `CentralServerWeb`

**Exit criteria**

- [ ] Multiple companies can use the same `CentralServer` safely without data leakage

## Phase 10: Reporting And Statistics

**Goal:** Restore and extend the old reporting layer.

**Add**

- [ ] Statistics aggregation jobs
- [ ] Daily/weekly/monthly incident metrics
- [ ] Per-store KPI summaries
- [ ] CSV/XLSX export
- [ ] Trend charts and dashboard cards

**Reuse from originals**

- [ ] Existing statistics/reporting expectations from the old platform

**Exit criteria**

- [ ] Managers can view actionable summaries without opening raw incidents one by one

## Phase 11: Operations And Hardening

**Goal:** Make the system production-safe.

**Add**

- [ ] Deployment model for each service
- [ ] Secrets management
- [ ] Service monitoring and alerting
- [ ] Disk usage monitoring and archive retention enforcement
- [ ] Backup strategy for DB and evidence
- [ ] Rate limits and concurrency guards
- [ ] Audit logs
- [ ] Security review of camera credentials and evidence access

**Exit criteria**

- [ ] Pilot rollout can run unattended and be supported operationally

## Phase 12: Migration From Original Platform

**Goal:** Move from the old Tobacco stack to the centralized product without losing stores or workflows.

**Add**

- [ ] Mapping table from old stores/cameras/users/zones to new entities
- [ ] Import scripts for catalog and configuration
- [ ] Parallel-run mode for selected pilot stores
- [ ] Verification checklist:
  - camera reachability
  - zone correctness
  - enabled detection types
  - archive access
  - incident correctness
- [ ] Cutover and rollback procedure

**Exit criteria**

- [ ] At least one pilot store fully runs on the new stack

## Recommended Delivery Order

- [ ] Wave 1: Phases 0-4
- [ ] Wave 2: Phases 5-7
- [ ] Wave 3: Phases 8-10
- [ ] Wave 4: Phases 11-12

## MVP Definition

The first real pilot-worthy MVP should include:

- [ ] One company
- [ ] Several stores
- [ ] Central catalog in DB
- [ ] Add/edit store and camera from client
- [ ] Live snapshot access
- [ ] Motion archive persisted on `CentralServer`
- [ ] At least one real Neuro-backed detection type
- [ ] Incident list with evidence
- [ ] Basic roles: admin + operator

## Immediate Next Build Sequence

If implementation continues from the current codebase, the next concrete sequence should be:

- [ ] Replace `CentralServer` `appsettings.json` store catalog with persistent DB entities and CRUD API
- [ ] Add client screens for store/server/camera management
- [ ] Add persistent archive index reload on startup
- [ ] Add first real `CentralServer -> Neuro` detection pipeline
- [ ] Add incident persistence and incident review UI

## Major Risks

- [ ] Browser-specific local network access quirks in Chromium-based browsers
- [ ] Camera credential and RTSP path variability across stores
- [ ] Storage growth if archive policy is not designed early
- [ ] Tight coupling between old domain rules and new generalized tenant model
- [ ] Overloading `CentralServer` if inference, archive, and live preview are scaled without queueing and backpressure

## Success Criteria For The Final Product

- [ ] New client fully replaces the old operator path for configured stores
- [ ] New stores can be onboarded without editing appsettings manually
- [ ] Detection types can be enabled/disabled per company/store/camera/zone
- [ ] New Neuro detection types can be integrated without breaking the rest of the system
- [ ] Multi-tenant isolation is enforceable
- [ ] Archive, incidents, and analytics survive restarts and support day-to-day operations
