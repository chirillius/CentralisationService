# CentralisationService Platform Checklist

This document is the living functional checklist for the platform.

Update it after every functional change in `CentralServer`, `Server`, `Neuro`, `Client`, or `Entities`.

## 1. Current Implemented Functionality

### CentralServer

- Owns centralized processing according to `docs/processing-rule.md`.
- Has a target PostgreSQL schema for platform, access, catalog, detection, incidents, archive, audit, and operational telemetry.
- Uses PostgreSQL as the runtime source of truth for companies, platform admins, users, grants, invitations, sessions, sites, server bindings, synced cameras, zones, and retail detection profiles.
- Applies PostgreSQL schema SQL files on startup when enabled.
- Seeds the first database state from existing JSON/appsettings configuration only when the database is empty.
- Polls site-side `Server` instances and builds a central store/camera catalog.
- Imports legacy configured stores from `appsettings.json` into PostgreSQL for transition/bootstrap.
- Stores dynamic company-site bindings in PostgreSQL.
- Filters stores, cameras, archive, zones, and detection settings by authenticated company context.
- Proxies camera frames through `Server`.
- Runs motion detection on central frames.
- Saves motion video fragments centrally under `company/<companyKey>/<siteKey>/videos/<yyyy-MM-dd>/<cameraName>/videos/`.
- Exposes archive item list and archive file download endpoints with image/video content type support.
- Stores zone names in PostgreSQL.
- Stores zones and polygon points in PostgreSQL and supports zone CRUD through CentralServer APIs.
- Stores retail detection profiles and model parameters in PostgreSQL.
- Runs centralized retail detection background monitoring.
- Calls `Neuro` for retail scene analysis.
- Saves clean detection evidence images under `company/<companyKey>/<siteKey>/defects/<yyyy-MM-dd>/<defectName>/images/`.
- Saves sidecar JSON metadata for evidence images, including company/site/camera/profile data and detected object ROI boxes.
- Saves test evidence frames when a person is present in the client zone.
- Supports multi-tenant company access with companies, accounts, grants, invitations, sessions, and platform admin sessions.
- Supports platform admin login through `/api/platform/auth/login`.
- Supports company creation and company status updates from platform admin APIs.
- Supports one-time invitation token creation and activation.
- Stores only token hashes for invitations and sessions.
- Supports revoking active company invitations.
- Supports listing company users and company invitations for the platform admin UI.
- Supports viewing a company user details card with role, status, last login time, and last login IP.
- Supports platform-admin account access changes: active, suspended, and disabled.
- Supports platform-admin password reset for company users.
- Supports binding a site-side `Server` to a company through platform admin APIs.
- Generates connector access tokens for bound site-side servers.
- Uses `X-Connector-Token` for bound `CentralServer -> Server` transport calls.
- Stores connector transport token metadata in PostgreSQL so protected site-side calls survive CentralServer restarts.
- Supports platform-admin camera frame proxying for site settings.
- Supports platform-admin camera add/update/delete flow through CentralServer with propagation to site-side Server.
- Stores public camera host and high/low stream paths in PostgreSQL without camera credentials.
- Supports platform-admin zone CRUD for zone markup from admin site settings.
- Has unit tests for access/session behavior.

### Neuro

- Runs as centralized AI service.
- Contains a defect catalog inspired by the original Neuro service.
- Exposes defect catalog endpoints.
- Exposes analysis endpoints for retail scene checks.
- Loads ONNX models from `Neuro/Neuro/DNNModels`.
- Has YOLO/ONNX runtime integration for detector execution.
- Supports model configuration for client/person, phone, and bottle checks.
- Returns structured detection results to `CentralServer`.
- Keeps low-level inference responsibilities separate from business rules.

### Server

- Runs as weak site-side connector.
- Stores local public camera configuration in `appsettings.json`.
- Stores camera login/password locally in ignored `Configuration/camera-secrets.json`.
- Exposes connector information through `/api/connector/info`.
- Exposes camera metadata through `/api/cameras`.
- Exposes JPEG frame capture through `/api/cameras/{cameraKey}/frame`.
- Captures frames through `ffmpeg`.
- Supports safe camera configuration by `Host`, `HighQualityPath`, and `LowQualityPath`.
- Keeps backward compatibility with legacy `Address` and `StreamAddress` camera settings.
- Uses low-quality stream path for frame preview by default.
- Supports camera add/update/delete endpoints protected by `X-Connector-Token`.
- Supports registration from `CentralServer` through `/api/connector/register`.
- Stores local company/site binding in `Configuration/connector-binding.json`.
- Stores only connector access token hash locally.
- Requires `X-Connector-Token` for camera and connector info access after binding.
- Remains transport-only and does not run analytics, archive writing, incident creation, or Neuro calls.

### Client

- Uses the visual shell and navigation direction from the original frontend.
- Has Russian UI copy for the current working screens.
- Supports company user login.
- Supports one-time invitation activation.
- Checks password confirmation on invitation activation before sending registration request.
- Stores company bearer session locally.
- Stores the successful CentralServer host and restores it on the next login screen opening.
- Accepts CentralServer address input as host/IP only and resolves it to `http://<host>:5120`.
- Stores company user role and permissions locally for UI capability filtering.
- Sends company bearer session in API requests.
- Supports platform admin login through the login screen.
- Stores platform admin bearer session separately from company session.
- Has protected company routes and protected platform admin route.
- Shows store list for the authenticated company.
- Allows selecting active store.
- Shows camera streaming/preview through `CentralServer`.
- Fits camera frames fully inside preview containers without cropping.
- Opens point settings as an overlay on the stores page.
- Point settings contain camera management and zone markup tabs.
- Allows company administrators to add, edit, and delete cameras from point settings.
- Hides point/camera/zone management actions from company operators.
- Supports zone markup UI on a last captured frame.
- Hides company-side zone settings from users without `zones.manage`.
- Supports model profile settings UI inside point settings.
- Shows motion archive data from `CentralServer`.
- Provides admin company list.
- Provides company detail page with tabs for sites and users/tokens.
- Allows creating companies from admin UI.
- Allows enabling, suspending, and disabling companies from admin UI.
- Allows adding a site-side `Server` address to a company from admin UI.
- Shows site/server availability with a status indicator.
- Shows cameras for a selected site in admin UI.
- Shows company users and invitation statuses in admin UI.
- Opens company user details from admin UI.
- Shows user role, access status, last login time, last login IP, grant expiration, and permissions in admin UI.
- Allows enabling, suspending, and blocking company users from admin UI.
- Allows setting a new company user password from admin UI.
- Allows issuing one-time invitation tokens from admin UI.
- Allows selecting invitation role: administrator or operator.
- Allows closing active invitation tokens from admin UI.
- Allows marking zones from the selected site settings in admin UI.
- Allows adding, editing, and deleting cameras from admin selected site settings.
- Shows repeated access errors as one centered Russian notification instead of repeatedly restarting page loading.
- Requires a correct site display name when binding a new site-side `Server`.

## 2. Functionality Recreated From Original Projects

### From TobaccoServer

- Centralized business-rule direction was preserved, but moved from site-side logic to `CentralServer`.
- Camera/frame capture idea was adapted into the new weak `Server`.
- Periodic model/job configuration idea was adapted into detection profiles and model parameters.
- Zone-dependent detection direction was preserved.
- Client-zone presence as a condition for checks was preserved.
- Motion/evidence frame saving was recreated in centralized form.
- Motion video fragment saving was adapted from the old ffmpeg recording idea into the centralized architecture.
- Server-side ffmpeg frame capture was reused conceptually.

### From CentralServerWeb

- Central server as the operator-facing API entry point was preserved.
- Store/server catalog direction was recreated.
- Centralized client access direction was kept.
- The new implementation expands this into multi-company access and platform administration.

### From TobaccoEntitiesWeb

- Shared entity/project direction was recreated through `CentralisationService.Entities`.
- Domain objects are kept out of unrelated service code where practical.
- Access, catalog, zones, and vision contracts are represented as shared models.
- Solution files were added so projects can be opened independently in Rider or Visual Studio.

### From Neuro

- Defect catalog direction was recreated.
- Detection type names were carried over conceptually.
- ONNX inference service direction was recreated.
- YOLO/ONNX object detection is wired for the new centralized Neuro service.
- Phone and bottle checks are the first practical retail object checks.

### From Front

- Main visual shell direction was ported.
- Store list flow was recreated.
- Point settings and zone markup flow were recreated.
- Zone markup uses a stable last frame instead of a constantly changing frame.
- Authentication screen exists as real entry point instead of only placeholder.
- Admin UI direction is now being rebuilt around companies, sites, users, and tokens.

## 3. What Still Needs To Be Done

### Platform And Data Storage

- Add an explicit one-shot migration/verification command for existing production JSON files.
- Decide whether company isolation is by `company_id`, PostgreSQL schema, or separate DB for large customers.
- Move initial platform admin bootstrap password from `appsettings.json` to environment variables or secret storage.
- Encrypt or move connector transport tokens from PostgreSQL plaintext transition storage to a proper secret store.
- Add token rotation for site-side connector tokens.
- Add audit log for admin actions, token creation/revocation, server binding, and failed access.
- Add persistent archive index rebuild on startup.

### CentralServer

- Add platform-admin detection profile endpoints scoped to selected company/site/camera.
- Add API for editing/deleting company site bindings.
- Add API for disabling a single site-side server binding.
- Add API for viewing and revoking active company user sessions.
- Add real incident pipeline and incident persistence.
- Add business state machine for customer presence, phone, bottle, and future detections.
- Add archive index rebuild on startup for saved video fragments and evidence files.
- Add retention policy for archive/evidence files.
- Add health/status endpoints for all background services.
- Add tests for server binding and platform admin endpoints.

### Server

- Add safe connector re-registration or token rotation flow.
- Add explicit endpoint for connector binding status.
- Add support for editing camera metadata from CentralServer if needed.
- Add stronger protection around registration endpoint before production.
- Add operational logs for unauthorized connector-token attempts.

### Neuro

- Document exact ONNX input/output names, shapes, preprocessing, and postprocessing.
- Add model warmup and latency metrics.
- Add per-model confidence configuration API.
- Add model enable/disable state from CentralServer profiles.
- Add smoke tests for available ONNX models.
- Add better error responses when a model is missing or has incompatible input/output shape.

### Client

- Add platform-admin model profile settings inside selected company site settings.
- Add company user grant expiration editing and role change actions.
- Add active session view and session revoke actions.
- Add site edit/delete UI.
- Add better loading, empty, and error states in admin detail tabs.
- Add code splitting to reduce Vite bundle warning.
- Add browser-level visual regression checks for admin and store flows.

### Deployment And Operations

- Add production environment configuration examples.
- Add Docker or service deployment scripts if required.
- Add structured logs and correlation IDs.
- Add backup/restore strategy for DB and archive files.
- Add troubleshooting document for connector registration and token failures.

## 4. Change Timeline

### 2026-06-04

- Created centralized architecture direction: `Client -> CentralServer -> Server -> camera`, `CentralServer -> Neuro`.
- Added processing rule that all processing lives on `CentralServer`.
- Implemented weak site-side `Server` frame transport.
- Implemented `CentralServer` store/camera catalog polling from configured servers.
- Implemented frame proxying through `CentralServer`.
- Implemented central motion detection and motion JPEG archive writing.
- Implemented initial zone names and zone markup storage.
- Added shared `Entities` project direction and solution files.

### 2026-06-05

- Ported the original frontend shell direction into the new `Client`.
- Added Russian UI flow for stores, streaming, point settings, zones, and archive.
- Changed point zone setup to use one latest captured frame for markup.
- Added point settings overlay window.
- Added model profile settings tab in point settings.
- Wired `Neuro` to ONNX models from `DNNModels`.
- Added YOLO/ONNX detector integration for person/client, phone, and bottle checks.
- Added centralized retail detection monitoring in `CentralServer`.
- Added test evidence saving when a person is present in the client zone.
- Added multi-tenant company access model.
- Added company invitations, accounts, grants, company sessions, and platform admin sessions.
- Added company user login and one-time invitation activation in `Client`.
- Added platform admin login in `Client`.
- Added platform admin API and admin UI for companies, status, and invitation tokens.
- Recommended PostgreSQL as target DBMS.
- Added company-site connector binding model.
- Added site-side `Server` registration from `CentralServer`.
- Added local connector binding file on `Server`.
- Added connector token checking through `X-Connector-Token`.
- Added admin company detail flow with `Точки` and `Пользователи и токены` tabs.
- Added server availability indicator and camera list in admin company site settings.
- Added this living platform checklist.
- Changed client error handling so blocked-company and access errors are shown once as centered Russian notifications.
- Required correct site display name during site-side `Server` binding.
- Localized main API/client-facing error messages to Russian.

### 2026-06-07

- Added successful CentralServer host restoration on the login screen.
- Changed CentralServer address input to host/IP-only entry with automatic `http://<host>:5120` resolution.
- Installed and configured PostgreSQL 17 on the CentralServer Windows machine.
- Created `centralisation_service` database and applied initial platform/detection SQL schema.
- Added `Npgsql` runtime database access to `CentralServer`.
- Added startup PostgreSQL schema initializer.
- Migrated CentralServer runtime storage from JSON files to PostgreSQL for access, companies, site bindings, synced cameras, zones, and retail detection profiles.
- Kept existing JSON/appsettings configuration only as bootstrap seed when the database is empty.
- Added password confirmation to invitation activation.
- Added company roles for invitations: administrator and operator.
- Added role-based permissions for company administrator and operator invitations.
- Added local client storage of company role and permissions.
- Hid company-side zone settings from operators without `zones.manage`.
- Added last login time and last login IP tracking for company users.
- Added platform-admin user details endpoint.
- Added platform-admin company user access control: active, suspended, disabled.
- Added platform-admin company user password reset.
- Added platform-admin camera frame proxy endpoint.
- Added platform-admin zone CRUD endpoints.
- Added zone markup from admin selected site settings.
- Added admin UI user details dialog with access actions and password reset.
- Added target PostgreSQL architecture document and initial SQL schema files.
- Added PostgreSQL seed data for company roles, permissions, zone name templates, and detection types.
- Made platform-admin company sites load from PostgreSQL first and use live Server sync only as availability/status overlay.
- Added more specific admin UI diagnostics for failed company detail sections: points, users, or invitations.
- Added clearer platform-admin company status display for active, suspended, disabled, and archived companies.
- Added platform-admin company deletion with explicit confirmation and PostgreSQL cascade cleanup.

### 2026-06-08

- Changed CentralServer archive storage to split files by company and site under `company/<companyKey>/<siteKey>/`.
- Changed central motion archive output from single JPEG frames to short `.mp4` video fragments encoded by `ffmpeg`.
- Added clean defect evidence image storage under `defects/<date>/<defectName>/images`.
- Added sidecar JSON evidence metadata with company, site, camera, detection profile, and object ROI boxes.
- Updated archive file serving to support `.mp4` content type.
- Changed camera preview and zone-markup frame rendering to fit the full frame without cropping.
- Added safe camera configuration with host-only camera address in CentralServer/UI and local camera credentials on Server.
- Added high-quality and low-quality stream path fields for each camera.
- Added Server camera add/update/delete endpoints that persist public camera settings into `appsettings.json`.
- Added platform-admin and company-admin camera management flows in point settings.
- Hid point/camera/zone management actions from company operators.
