# PostgreSQL Architecture

This document describes the target PostgreSQL storage architecture for `CentralisationService`.

The goal is to replace JSON-backed runtime storage with a normalized database while preserving the current centralized processing rule:

```text
Client -> CentralServer -> Server -> camera
CentralServer -> Neuro
```

`CentralServer` remains the only owner of analytics, detection profiles, incidents, archive indexes, company access, and business rules. Site-side `Server` stays transport-only.

## Design Decision

PostgreSQL is the source of truth for platform and company configuration.

JSON files should remain only as:

- temporary development seed files;
- local Server binding/cache files;
- migration source during the transition to PostgreSQL.

Former configuration files map to PostgreSQL like this:

| Previous file/table | New PostgreSQL area |
| --- | --- |
| `thisStore.json` | `platform.companies`, `catalog.sites`, `catalog.server_nodes`, `catalog.cameras` |
| `zones.json` | `catalog.zones`, `catalog.zone_points` |
| `zone_names.json` | `catalog.zone_name_templates` |
| `periodal_tasks.json` | `detection.detection_profiles`, `detection.detection_profile_parameters`, `detection.detection_profile_schedules`, `detection.detection_profile_zones` |
| CentralServer `Users`, `Roles`, `RegistrationCodes` | `access.accounts`, `access.roles`, `access.company_invitations`, `access.company_access_grants` |
| CentralServer `Audit` | `audit.audit_events` |
| TobaccoServer `PhoneFailures`, `BottleFailures`, `SmokeFailures`, etc. | `incidents.incidents`, `incidents.incident_evidence`, `incidents.incident_events` |
| TobaccoServer `DefectImages` | `incidents.incident_evidence` |

The schema is split into PostgreSQL schemas:

```text
platform
access
catalog
detection
incidents
archive
audit
ops
```

## Migration Files

SQL files live in:

```text
CentralServer/CentralServer/Database/PostgreSql/
```

Current files:

- `001_initial_platform_schema.sql` creates schemas, tables, constraints, and indexes.
- `002_seed_detection_catalog.sql` inserts initial roles, permissions, zone names, and detection types.

## Multi-Tenant Rule

`company_id` is the top-level ownership boundary for company-owned data.

Every table containing customer-owned data must include `company_id` directly or through a strict FK path. This keeps queries fast and prevents accidental cross-company data exposure.

Use:

- `id uuid` for primary keys;
- `key text` for stable technical identifiers;
- `name text` for display names.

Do not use `company_key` as a foreign key. Use `company_id`.

## Schemas And Tables

### `platform`

Platform-level entities that are not owned by a normal company user.

#### `platform.companies`

Stores companies.

Important columns:

- `id`
- `key`
- `name`
- `status`
- `access_expires_at_utc`
- `disabled_at_utc`
- `disabled_reason`
- `created_at_utc`
- `updated_at_utc`

Allowed status values:

- `active`
- `suspended`
- `disabled`
- `archived`

#### `platform.platform_admins`

Stores platform administrator accounts.

This replaces the temporary `admin / 1234` configuration for production.

Important columns:

- `login`
- `display_name`
- `password_hash`
- `password_salt`
- `is_enabled`
- `last_login_at_utc`
- `last_login_ip`

#### `platform.platform_admin_sessions`

Stores platform admin bearer sessions.

Tokens are stored only as hashes.

### `access`

Authentication, authorization, roles, invitations, grants, and company user sessions.

#### `access.accounts`

Reusable user identities.

An account may have access to one or more companies through `company_access_grants`.

Important columns:

- `login`
- `display_name`
- `password_hash`
- `password_salt`
- `is_enabled`
- `last_login_at_utc`
- `last_login_ip`

#### `access.roles`

Role dictionary.

Initial company roles:

- `company-admin`
- `company-operator`

#### `access.permissions`

Permission dictionary.

Initial permissions:

- `sites.read`
- `cameras.read`
- `archive.read`
- `zones.manage`
- `detection-profiles.manage`
- `users.manage`

#### `access.role_permissions`

Many-to-many role/permission mapping.

#### `access.company_access_grants`

Binds account to company, role, status, and optional expiration.

This is the main company access table.

Allowed status values:

- `active`
- `suspended`
- `disabled`

#### `access.company_invitations`

One-time invitation tokens.

Rules:

- token is shown only once;
- only `token_hash` is stored;
- invitation can be used once;
- invitation may expire;
- invitation creates an account and a company grant.

#### `access.access_sessions`

Company user bearer sessions.

Tokens are stored only as hashes.

### `catalog`

Company-owned operational catalog: sites, servers, cameras, zones.

#### `catalog.sites`

Stores points/stores.

Important columns:

- `company_id`
- `key`
- `name`
- `address`
- `cleaning_day`
- `status`

#### `catalog.server_nodes`

Stores site-side `Server` bindings.

Important columns:

- `company_id`
- `site_id`
- `connector_id`
- `base_url`
- `public_address`
- `connector_token_hash`
- `is_enabled`
- `last_seen_at_utc`
- `last_sync_at_utc`

Site-side Server still keeps only local binding/cache. Business settings live in PostgreSQL.

#### `catalog.cameras`

Stores camera metadata known by CentralServer.

Important columns:

- `company_id`
- `site_id`
- `server_node_id`
- `source_camera_key`
- `global_camera_key`
- `name`
- `is_enabled`
- `last_seen_at_utc`

RTSP addresses should stay on site-side `Server` unless CentralServer becomes responsible for full camera management. If CentralServer stores RTSP credentials later, they must be encrypted or stored in a secret store.

#### `catalog.zone_name_templates`

Replaces `zone_names.json`.

Initial values:

- `Прилавок`
- `Клиентская`
- `Касса`
- `Дым`
- `Телефон`
- `Бутылки`
- `Бейдж`
- `Стол`
- `Свет`
- `Мойка полов`

#### `catalog.zones`

Stores zone headers and bounds.

Important columns:

- `company_id`
- `site_id`
- `camera_id`
- `zone_type_key`
- `zone_name`
- `custom_name`
- `display_name`
- `bounds_x`
- `bounds_y`
- `bounds_width`
- `bounds_height`

#### `catalog.zone_points`

Stores polygon points for each zone.

Coordinates are normalized:

- `x` from `0` to `1`
- `y` from `0` to `1`

This supports polygons and avoids being locked into old rectangle-only zones.

### `detection`

Detection types, model registry, configurable parameters, schedules, and profile-zone binding.

#### `detection.detection_types`

Global catalog of available fixation/violation types.

Initial types include:

- `client-presence-test`
- `phone`
- `bottles`
- `smoke`
- `cash-register`
- `counting-cash-register`
- `abandoned-open-cash-register`
- `mopping`
- `badge`
- `clothes`
- `pose`
- `conversion`
- `clear-stall`
- `delays`
- `crowd`
- `light`
- `service-near-cabinet`
- `no-one-at-stall`
- `human-before-after-shift`
- `inactive-salesman`

Important columns:

- `key`
- `name`
- `category`
- `detection_kind`
- `default_severity`
- `is_enabled`

#### `detection.detection_type_parameters`

Defines which settings are available for each detection type.

This table lets the UI build configuration forms without hardcoding every field.

Examples:

- `interval_seconds`
- `cooldown_seconds`
- `confidence_threshold`
- `requires_client_zone_presence`
- `save_evidence_on_positive_result`
- `target_zone_type_key`
- `client_zone_type_key`

Values are typed through `value_type`.

#### `detection.model_registry`

Stores model metadata, not model binaries.

Important columns:

- `key`
- `name`
- `model_kind`
- `model_path`
- `input_width`
- `input_height`
- `labels jsonb`
- `is_enabled`

Model files remain outside the DB, for example in `Neuro/Neuro/DNNModels` or object storage.

#### `detection.detection_type_models`

Maps detection types to compatible models.

Example:

```text
phone -> phone-yolo-v1
bottles -> bottles-yolo-v1
```

#### `detection.detection_profiles`

Concrete company/site/camera detection configuration.

This is the replacement for `periodal_tasks.json`.

Important columns:

- `company_id`
- `site_id`
- `camera_id`
- `server_node_id`
- `detection_type_id`
- `model_id`
- `key`
- `name`
- `is_enabled`
- `severity`
- `interval_seconds`
- `cooldown_seconds`
- `confidence_threshold`
- `requires_client_zone_presence`
- `client_zone_type_key`
- `target_zone_type_key`
- `save_evidence_on_positive_result`
- `settings jsonb`

Use regular columns for frequently filtered values. Use `settings jsonb` only for rare type-specific options.

#### `detection.detection_profile_parameters`

Stores custom overrides for a profile.

This keeps the system extensible when new fixation types need extra parameters.

#### `detection.detection_profile_schedules`

Stores active time windows.

Important columns:

- `day_of_week`
- `active_from_local_time`
- `active_to_local_time`
- `timezone`

#### `detection.detection_profile_zones`

Maps profiles to zones by usage.

Examples of `usage_key`:

- `client`
- `target`
- `ignored`
- `conversion-entry`
- `conversion-exit`

This is more flexible than storing only one client zone and one target zone.

### `incidents`

Universal incident storage.

The old `TobaccoServer` had separate tables for each failure type. The new platform stores all violations in one normalized incident model.

#### `incidents.incidents`

Stores violations/fixations.

Important columns:

- `company_id`
- `site_id`
- `camera_id`
- `detection_profile_id`
- `detection_type_id`
- `status`
- `severity`
- `opened_at_utc`
- `closed_at_utc`
- `verified_at_utc`
- `verified_by_account_id`
- `confidence`
- `details jsonb`

Allowed status values:

- `open`
- `confirmed`
- `closed`
- `dismissed`

Old mappings:

| Old entity | New representation |
| --- | --- |
| `PhoneFailure` | `incidents.incidents` with `detection_type = phone` |
| `BottleFailure` | `incidents.incidents` with `detection_type = bottles` |
| `Smoke` | `incidents.incidents` with `detection_type = smoke` |
| `CashRegisterFailure` | `incidents.incidents` with `detection_type = cash-register` |
| `ConversionRegisterEvent` | `incidents.incidents` with `detection_type = conversion`, `details.peopleNumber` |

#### `incidents.incident_events`

Stores incident history.

Examples:

- `created`
- `confirmed`
- `dismissed`
- `closed`
- `commented`
- `evidence-added`

#### `incidents.incident_evidence`

Stores evidence metadata.

Files are not stored in PostgreSQL.

Important columns:

- `incident_id`
- `evidence_type`
- `relative_path`
- `captured_at_utc`
- `metadata jsonb`

### `archive`

Archive indexes only. Binary files remain on disk or object storage.

#### `archive.motion_frames`

Central motion frame index.

#### `archive.video_fragments`

Future video fragment index.

### `audit`

#### `audit.audit_events`

Unified audit trail.

Replaces old `Audit` and `UserActionDescriptions`.

Important columns:

- `company_id`
- `account_id`
- `platform_admin_id`
- `action_key`
- `entity_type`
- `entity_id`
- `ip_address`
- `user_agent`
- `details jsonb`
- `created_at_utc`

### `ops`

Operational telemetry and diagnostics.

#### `ops.server_health_checks`

Stores site-side Server availability checks.

#### `ops.background_job_runs`

Stores CentralServer background job runs.

#### `ops.neuro_model_calls`

Stores Neuro/model call periods, latency, status, and response summaries.

This table is important for debugging model call frequency per company/site/camera/profile.

## Performance Rules

Frequently filtered fields must be regular columns, not only JSONB.

Use columns for:

- `company_id`
- `site_id`
- `camera_id`
- `detection_type_id`
- `detection_profile_id`
- `status`
- timestamps
- confidence thresholds
- enabled flags

Use JSONB for rare per-detection details:

- `peopleNumber`
- `clientZoneHasPeople`
- model-specific labels
- one-off state machine metadata
- raw response summaries

Large tables must always be queried with:

- `company_id`
- time range;
- pagination.

## Important Indexes

The initial schema includes indexes for:

- company access grants by `company_id/status`;
- active sessions by `token_hash/expires_at_utc`;
- sites by `company_id/status`;
- cameras by `company_id/site_id`;
- zones by `company_id/site_id/camera_id`;
- detection profiles by `company_id/site_id/camera_id/is_enabled`;
- incidents by `company_id/opened_at_utc`;
- incidents by `company_id/site_id/opened_at_utc`;
- incidents by `company_id/camera_id/opened_at_utc`;
- incident evidence by `incident_id`;
- motion/video archive by `company_id/camera_id/time`;
- audit events by company/account/time;
- ops telemetry by server/job/profile/time.

## Next Implementation Steps

1. Add a CentralServer database access layer with Npgsql/EF Core or SQL migrations runner.
2. Add configuration:

```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=localhost;Port=5432;Database=centralisation_service;Username=centralisation"
  }
}
```

The password must be supplied through environment variables, user-secrets, secret storage, or ignored local config.

3. Create a JSON-to-PostgreSQL migration command for:

- `Configuration/access/*.json`;
- `Configuration/zones.json`;
- `Configuration/zone_names.json`;
- `Configuration/detection_profiles.json`;

4. Replace JSON services one by one:

- access storage;
- company/site binding storage;
- zone catalog storage;
- detection profile storage;
- incident/evidence persistence.

5. Keep JSON fallback only for local Server connector binding/cache.
