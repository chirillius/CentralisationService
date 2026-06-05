# Multi-Tenant Company Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add company-isolated one-time invitation activation, account login, expiring access grants, sessions, and company-wide blocking to CentralServer and Client.

**Architecture:** CentralServer owns a JSON-backed access repository and issues opaque bearer sessions. Every client-facing API resolves the active company from the session, while platform administration uses a separately configured bootstrap key. Company availability is also enforced by the catalog and detection scheduler.

**Tech Stack:** .NET 10, ASP.NET Core, C#, JSON persistence, PBKDF2 password hashing, SHA-256 token hashing, React, TypeScript, MUI.

---

### Task 1: Shared Access Domain

**Files:**
- Create: `Entities/CentralisationService.Entities/Models/Access/*.cs`

- [ ] Add `CompanyStatus`, `Account`, `CompanyInvitation`, `CompanyAccessGrant`, and `AccessSession`.
- [ ] Keep persisted secrets limited to hashes and salts.
- [ ] Build `CentralisationService.Entities`.

### Task 2: JSON Access Repository

**Files:**
- Create: `CentralServer/CentralServer/Models/AccessOptions.cs`
- Create: `CentralServer/CentralServer/Services/AccessStoreService.cs`
- Create: `CentralServer/CentralServer/Configuration/access/*.json`

- [ ] Implement atomic reads and writes for each access collection.
- [ ] Implement company status, invitation, account, grant, and session operations.
- [ ] Implement password and token hashing.
- [ ] Add repository tests.

### Task 3: Authentication And Platform Administration API

**Files:**
- Create: `CentralServer/CentralServer/Controllers/AuthController.cs`
- Create: `CentralServer/CentralServer/Controllers/PlatformCompaniesController.cs`
- Create: `CentralServer/CentralServer/Services/CompanyAccessService.cs`
- Create: `CentralServer/CentralServer/Middleware/CompanyAccessMiddleware.cs`
- Modify: `CentralServer/CentralServer/Program.cs`

- [ ] Add invitation activation, login, logout, and current-user endpoints.
- [ ] Add platform endpoints for companies, invitations, and company status.
- [ ] Reject expired grants and disabled companies.
- [ ] Revoke company sessions when company status changes.

### Task 4: Company-Scoped Catalog And Processing

**Files:**
- Modify: `CentralServer/CentralServer/Models/ConfiguredStoreOptions.cs`
- Modify: `CentralServer/CentralServer/Models/RegisteredServerState.cs`
- Modify: `CentralServer/CentralServer/Models/RemoteCameraState.cs`
- Modify: `CentralServer/CentralServer/Services/ServerRegistryService.cs`
- Modify: `CentralServer/CentralServer/Services/RetailDetectionMonitoringBackgroundService.cs`
- Modify: relevant CentralServer controllers

- [ ] Bind every configured site to a company.
- [ ] Filter stores, cameras, zones, profiles, and archives by session company.
- [ ] Skip disabled or expired companies in detection processing.
- [ ] Add cross-company access tests.

### Task 5: Client Invitation And Login Flow

**Files:**
- Modify: `Client/src/pages/LoginPage.tsx`
- Modify: `Client/src/store/index.tsx`
- Modify: `Client/src/types/central.ts`

- [ ] Support one-time invitation activation.
- [ ] Support subsequent login with account credentials.
- [ ] Add bearer sessions to CentralServer requests.
- [ ] Show blocked/expired company access errors clearly.

### Task 6: Verification And Documentation

- [ ] Run CentralServer and Entities tests.
- [ ] Build CentralServer, Neuro, Server, Entities, and Client.
- [ ] Smoke-test activation, login, company blocking, and company-scoped stores.
- [ ] Document current platform functionality and verification commands.
