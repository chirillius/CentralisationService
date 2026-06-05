# Central Zone Markup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build centralized polygon-based zone markup so `Client` can configure per-camera zones only through `CentralServer`, while preserving the original zone naming workflow and preparing geometry for future YOLO/ONNX inference.

**Architecture:** `CentralServer` becomes the source of truth for zone names and saved camera zones in JSON-backed configuration files. `Client` reuses the existing camera selection flow and borrows interaction patterns from the original stores/zones UI, but sends all reads and writes to new `CentralServer` APIs. Zones are stored as normalized polygons plus derived bounding rectangles for future detection crops.

**Tech Stack:** ASP.NET Core, C#/.NET 10, React 19, TypeScript, Vite, Material UI, JSON file storage

---

### Task 1: CentralServer zone domain and storage

**Files:**
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/ZonePointDto.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/ZonePolygonDto.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/ZoneBoundsDto.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/ZoneRecord.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/ZoneCatalogOptions.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Services/ZoneCatalogService.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Configuration/zone_names.json`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Configuration/zones.json`
- Modify: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Program.cs`

- [ ] Write a failing storage test for loading default zone names and custom option behavior.
- [ ] Run the test and verify it fails because the zone catalog service does not exist yet.
- [ ] Implement `ZoneCatalogService` with JSON-backed loading of `zone_names.json` and normalized `ZoneRecord` persistence in `zones.json`.
- [ ] Run the storage test and verify it passes.
- [ ] Commit the storage slice.

### Task 2: CentralServer zone API

**Files:**
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Models/UpsertZoneRequest.cs`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Controllers/ZonesController.cs`
- Modify: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/Program.cs`

- [ ] Write a failing API test for `GET /api/zones/names`.
- [ ] Run the test and verify it fails because the endpoint is missing.
- [ ] Implement `GET /api/zones/names`.
- [ ] Run the API test and verify it passes.
- [ ] Write a failing API test for create/list/delete zone flow scoped by `cameraKey`.
- [ ] Run the test and verify it fails for the expected missing behavior.
- [ ] Implement `GET /api/zones`, `POST /api/zones`, `PUT /api/zones/{zoneId}`, and `DELETE /api/zones/{zoneId}` with validation for polygon point count, normalized coordinates, and display name resolution.
- [ ] Run the API tests and verify they pass.
- [ ] Commit the API slice.

### Task 3: Client data contracts and central zones screen state

**Files:**
- Modify: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Client/src/App.tsx`
- Create: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Client/src/types/zones.ts`

- [ ] Write a failing TypeScript build check by introducing typed zone DTO usage in `App.tsx`.
- [ ] Run `npm run build` and verify it fails because zone DTOs and client state are not implemented yet.
- [ ] Add typed DTOs for zone names, polygon points, bounds, zone records, and upsert payloads.
- [ ] Refactor client screen state so camera selection, zone loading, and zone editor state live alongside the existing central preview flow.
- [ ] Run `npm run build` and verify the client compiles again.
- [ ] Commit the state slice.

### Task 4: Client polygon editor and stores-inspired workflow

**Files:**
- Modify: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Client/src/App.tsx`

- [ ] Write a failing build check by wiring UI controls for zone names, custom name input, zone list, and polygon editor actions before implementation is complete.
- [ ] Run `npm run build` and verify it fails for the expected missing handlers/types.
- [ ] Implement the zone markup section using the current central camera screen plus interaction patterns borrowed from `/Users/chirill/Работа/webArchiveRetrieval/src/pages/StoresSettingsPage.tsx` and `/Users/chirill/Работа/webArchiveRetrieval/src/components/stores/StoreZonesConfigurator.tsx`.
- [ ] Support polygon point placement, selected-zone highlighting, draft reset, save, and delete flows through `CentralServer`.
- [ ] Run `npm run build` and verify the client passes.
- [ ] Commit the UI slice.

### Task 5: Verification

**Files:**
- Modify if needed: `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/appsettings.json`

- [ ] Run `dotnet build /Users/chirill/Работа/webArchiveRetrieval/CentralisationService/CentralServer/CentralServer/CentralServer.csproj` and verify it passes.
- [ ] Run `npm run build` in `/Users/chirill/Работа/webArchiveRetrieval/CentralisationService/Client` and verify it passes.
- [ ] Smoke-check the new zone API against the local `CentralServer` instance.
- [ ] Review created JSON files to confirm zones persist per `siteKey` and `cameraKey`.
- [ ] Commit final cleanup if verification required any small fixes.
