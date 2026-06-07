-- CentralisationService PostgreSQL schema.
-- This file defines the target relational storage for platform access,
-- catalog configuration, detection settings, incidents, archive index,
-- audit trail, and operational telemetry.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS platform;
CREATE SCHEMA IF NOT EXISTS access;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS detection;
CREATE SCHEMA IF NOT EXISTS incidents;
CREATE SCHEMA IF NOT EXISTS archive;
CREATE SCHEMA IF NOT EXISTS audit;
CREATE SCHEMA IF NOT EXISTS ops;

CREATE TABLE IF NOT EXISTS platform.companies (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    access_expires_at_utc timestamptz NULL,
    disabled_at_utc timestamptz NULL,
    disabled_reason text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_companies_key UNIQUE (key),
    CONSTRAINT ck_companies_status CHECK (status IN ('active', 'suspended', 'disabled', 'archived'))
);

CREATE TABLE IF NOT EXISTS platform.platform_admins (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    login text NOT NULL,
    display_name text NOT NULL,
    password_hash text NOT NULL,
    password_salt text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    last_login_at_utc timestamptz NULL,
    last_login_ip inet NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_platform_admins_login UNIQUE (login)
);

CREATE TABLE IF NOT EXISTS platform.platform_admin_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    platform_admin_id uuid NOT NULL REFERENCES platform.platform_admins(id) ON DELETE CASCADE,
    token_hash text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    last_used_at_utc timestamptz NULL,
    CONSTRAINT uq_platform_admin_sessions_token_hash UNIQUE (token_hash)
);

CREATE TABLE IF NOT EXISTS access.accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    login text NOT NULL,
    display_name text NOT NULL,
    password_hash text NOT NULL,
    password_salt text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    last_login_at_utc timestamptz NULL,
    last_login_ip inet NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_accounts_login UNIQUE (login)
);

CREATE TABLE IF NOT EXISTS access.roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    scope text NOT NULL DEFAULT 'company',
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_roles_key UNIQUE (key),
    CONSTRAINT ck_roles_scope CHECK (scope IN ('platform', 'company'))
);

CREATE TABLE IF NOT EXISTS access.permissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    description text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_permissions_key UNIQUE (key)
);

CREATE TABLE IF NOT EXISTS access.role_permissions (
    role_id uuid NOT NULL REFERENCES access.roles(id) ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES access.permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE IF NOT EXISTS access.company_access_grants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    account_id uuid NOT NULL REFERENCES access.accounts(id) ON DELETE CASCADE,
    role_id uuid NOT NULL REFERENCES access.roles(id),
    status text NOT NULL DEFAULT 'active',
    expires_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_company_access_grants_company_account UNIQUE (company_id, account_id),
    CONSTRAINT ck_company_access_grants_status CHECK (status IN ('active', 'suspended', 'disabled'))
);

CREATE TABLE IF NOT EXISTS access.company_invitations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    name text NOT NULL,
    token_hash text NOT NULL,
    role_id uuid NOT NULL REFERENCES access.roles(id),
    expires_at_utc timestamptz NULL,
    used_at_utc timestamptz NULL,
    used_by_account_id uuid NULL REFERENCES access.accounts(id),
    revoked_at_utc timestamptz NULL,
    created_by_platform_admin_id uuid NULL REFERENCES platform.platform_admins(id),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_company_invitations_token_hash UNIQUE (token_hash)
);

CREATE TABLE IF NOT EXISTS access.access_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    account_id uuid NOT NULL REFERENCES access.accounts(id) ON DELETE CASCADE,
    grant_id uuid NOT NULL REFERENCES access.company_access_grants(id) ON DELETE CASCADE,
    token_hash text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    last_used_at_utc timestamptz NULL,
    CONSTRAINT uq_access_sessions_token_hash UNIQUE (token_hash)
);

CREATE TABLE IF NOT EXISTS catalog.sites (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    key text NOT NULL,
    name text NOT NULL,
    address text NULL,
    cleaning_day integer NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'active',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_sites_company_key UNIQUE (company_id, key),
    CONSTRAINT ck_sites_status CHECK (status IN ('active', 'suspended', 'disabled', 'archived'))
);

CREATE TABLE IF NOT EXISTS catalog.server_nodes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    connector_id text NULL,
    base_url text NOT NULL,
    public_address text NULL,
    connector_token_hash text NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    last_seen_at_utc timestamptz NULL,
    last_sync_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_server_nodes_site_base_url UNIQUE (site_id, base_url)
);

CREATE TABLE IF NOT EXISTS catalog.cameras (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    server_node_id uuid NOT NULL REFERENCES catalog.server_nodes(id) ON DELETE CASCADE,
    source_camera_key text NOT NULL,
    global_camera_key text NOT NULL,
    name text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    last_seen_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_cameras_global_key UNIQUE (global_camera_key),
    CONSTRAINT uq_cameras_server_source_key UNIQUE (server_node_id, source_camera_key)
);

CREATE TABLE IF NOT EXISTS catalog.zone_name_templates (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    zone_type_key text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    display_order integer NOT NULL DEFAULT 0,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_zone_name_templates_key UNIQUE (key)
);

CREATE TABLE IF NOT EXISTS catalog.zones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NOT NULL REFERENCES catalog.cameras(id) ON DELETE CASCADE,
    zone_type_key text NOT NULL,
    zone_name text NOT NULL,
    custom_name text NULL,
    display_name text NOT NULL,
    bounds_x double precision NOT NULL DEFAULT 0,
    bounds_y double precision NOT NULL DEFAULT 0,
    bounds_width double precision NOT NULL DEFAULT 0,
    bounds_height double precision NOT NULL DEFAULT 0,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS catalog.zone_points (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    zone_id uuid NOT NULL REFERENCES catalog.zones(id) ON DELETE CASCADE,
    point_index integer NOT NULL,
    x double precision NOT NULL,
    y double precision NOT NULL,
    CONSTRAINT uq_zone_points_zone_index UNIQUE (zone_id, point_index),
    CONSTRAINT ck_zone_points_x CHECK (x >= 0 AND x <= 1),
    CONSTRAINT ck_zone_points_y CHECK (y >= 0 AND y <= 1)
);

CREATE TABLE IF NOT EXISTS detection.detection_types (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    category text NOT NULL,
    detection_kind text NOT NULL,
    default_severity text NOT NULL DEFAULT 'medium',
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_types_key UNIQUE (key),
    CONSTRAINT ck_detection_types_default_severity CHECK (default_severity IN ('low', 'medium', 'high', 'critical'))
);

CREATE TABLE IF NOT EXISTS detection.detection_type_parameters (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    detection_type_id uuid NOT NULL REFERENCES detection.detection_types(id) ON DELETE CASCADE,
    key text NOT NULL,
    name text NOT NULL,
    value_type text NOT NULL,
    default_value jsonb NULL,
    min_value jsonb NULL,
    max_value jsonb NULL,
    is_required boolean NOT NULL DEFAULT false,
    display_order integer NOT NULL DEFAULT 0,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_type_parameters_type_key UNIQUE (detection_type_id, key),
    CONSTRAINT ck_detection_type_parameters_value_type CHECK (value_type IN ('int', 'double', 'bool', 'string', 'zone_type', 'zone', 'time_range', 'string_array', 'json'))
);

CREATE TABLE IF NOT EXISTS detection.model_registry (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key text NOT NULL,
    name text NOT NULL,
    model_kind text NOT NULL,
    model_path text NOT NULL,
    input_width integer NULL,
    input_height integer NULL,
    labels jsonb NOT NULL DEFAULT '[]'::jsonb,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_model_registry_key UNIQUE (key)
);

CREATE TABLE IF NOT EXISTS detection.detection_type_models (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    detection_type_id uuid NOT NULL REFERENCES detection.detection_types(id) ON DELETE CASCADE,
    model_id uuid NOT NULL REFERENCES detection.model_registry(id) ON DELETE CASCADE,
    is_default boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_type_models_type_model UNIQUE (detection_type_id, model_id)
);

CREATE TABLE IF NOT EXISTS detection.detection_profiles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE CASCADE,
    server_node_id uuid NULL REFERENCES catalog.server_nodes(id) ON DELETE SET NULL,
    detection_type_id uuid NOT NULL REFERENCES detection.detection_types(id),
    model_id uuid NULL REFERENCES detection.model_registry(id),
    key text NOT NULL,
    name text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    severity text NOT NULL DEFAULT 'medium',
    interval_seconds integer NOT NULL DEFAULT 5,
    cooldown_seconds integer NOT NULL DEFAULT 30,
    confidence_threshold double precision NULL,
    requires_client_zone_presence boolean NOT NULL DEFAULT false,
    client_zone_type_key text NULL,
    target_zone_type_key text NULL,
    save_evidence_on_positive_result boolean NOT NULL DEFAULT true,
    settings jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_profiles_company_key UNIQUE (company_id, key),
    CONSTRAINT ck_detection_profiles_severity CHECK (severity IN ('low', 'medium', 'high', 'critical')),
    CONSTRAINT ck_detection_profiles_interval CHECK (interval_seconds > 0),
    CONSTRAINT ck_detection_profiles_cooldown CHECK (cooldown_seconds >= 0)
);

CREATE TABLE IF NOT EXISTS detection.detection_profile_parameters (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL REFERENCES detection.detection_profiles(id) ON DELETE CASCADE,
    parameter_key text NOT NULL,
    value jsonb NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_profile_parameters_profile_key UNIQUE (profile_id, parameter_key)
);

CREATE TABLE IF NOT EXISTS detection.detection_profile_schedules (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL REFERENCES detection.detection_profiles(id) ON DELETE CASCADE,
    day_of_week smallint NOT NULL,
    active_from_local_time time NOT NULL,
    active_to_local_time time NOT NULL,
    timezone text NOT NULL DEFAULT 'Europe/Moscow',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_detection_profile_schedules_day CHECK (day_of_week >= 0 AND day_of_week <= 6)
);

CREATE TABLE IF NOT EXISTS detection.detection_profile_zones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL REFERENCES detection.detection_profiles(id) ON DELETE CASCADE,
    zone_id uuid NOT NULL REFERENCES catalog.zones(id) ON DELETE CASCADE,
    usage_key text NOT NULL,
    is_required boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_detection_profile_zones_profile_zone_usage UNIQUE (profile_id, zone_id, usage_key)
);

CREATE TABLE IF NOT EXISTS incidents.incidents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE SET NULL,
    detection_profile_id uuid NULL REFERENCES detection.detection_profiles(id) ON DELETE SET NULL,
    detection_type_id uuid NOT NULL REFERENCES detection.detection_types(id),
    status text NOT NULL DEFAULT 'open',
    severity text NOT NULL DEFAULT 'medium',
    opened_at_utc timestamptz NOT NULL,
    closed_at_utc timestamptz NULL,
    verified_at_utc timestamptz NULL,
    verified_by_account_id uuid NULL REFERENCES access.accounts(id) ON DELETE SET NULL,
    confidence double precision NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_incidents_status CHECK (status IN ('open', 'confirmed', 'closed', 'dismissed')),
    CONSTRAINT ck_incidents_severity CHECK (severity IN ('low', 'medium', 'high', 'critical'))
);

CREATE TABLE IF NOT EXISTS incidents.incident_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    incident_id uuid NOT NULL REFERENCES incidents.incidents(id) ON DELETE CASCADE,
    event_type text NOT NULL,
    account_id uuid NULL REFERENCES access.accounts(id) ON DELETE SET NULL,
    platform_admin_id uuid NULL REFERENCES platform.platform_admins(id) ON DELETE SET NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS incidents.incident_evidence (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    incident_id uuid NOT NULL REFERENCES incidents.incidents(id) ON DELETE CASCADE,
    evidence_type text NOT NULL,
    relative_path text NOT NULL,
    captured_at_utc timestamptz NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_incident_evidence_type CHECK (evidence_type IN ('image', 'video', 'frame', 'audio', 'metadata'))
);

CREATE TABLE IF NOT EXISTS archive.motion_frames (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE SET NULL,
    relative_path text NOT NULL,
    file_name text NOT NULL,
    captured_at_utc timestamptz NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS archive.video_fragments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE SET NULL,
    relative_path text NOT NULL,
    started_at_utc timestamptz NOT NULL,
    ended_at_utc timestamptz NOT NULL,
    duration_seconds integer NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS audit.audit_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NULL REFERENCES platform.companies(id) ON DELETE SET NULL,
    account_id uuid NULL REFERENCES access.accounts(id) ON DELETE SET NULL,
    platform_admin_id uuid NULL REFERENCES platform.platform_admins(id) ON DELETE SET NULL,
    action_key text NOT NULL,
    entity_type text NULL,
    entity_id uuid NULL,
    ip_address inet NULL,
    user_agent text NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ops.server_health_checks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NOT NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    server_node_id uuid NOT NULL REFERENCES catalog.server_nodes(id) ON DELETE CASCADE,
    is_available boolean NOT NULL,
    status_code integer NULL,
    latency_ms integer NULL,
    error_message text NULL,
    checked_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ops.background_job_runs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    job_key text NOT NULL,
    company_id uuid NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE CASCADE,
    status text NOT NULL,
    started_at_utc timestamptz NOT NULL,
    finished_at_utc timestamptz NULL,
    duration_ms integer NULL,
    error_message text NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_background_job_runs_status CHECK (status IN ('started', 'completed', 'failed', 'skipped'))
);

CREATE TABLE IF NOT EXISTS ops.neuro_model_calls (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NULL REFERENCES platform.companies(id) ON DELETE CASCADE,
    site_id uuid NULL REFERENCES catalog.sites(id) ON DELETE CASCADE,
    camera_id uuid NULL REFERENCES catalog.cameras(id) ON DELETE CASCADE,
    detection_profile_id uuid NULL REFERENCES detection.detection_profiles(id) ON DELETE SET NULL,
    model_id uuid NULL REFERENCES detection.model_registry(id) ON DELETE SET NULL,
    detection_type_id uuid NULL REFERENCES detection.detection_types(id) ON DELETE SET NULL,
    status text NOT NULL,
    confidence_threshold double precision NULL,
    latency_ms integer NULL,
    error_message text NULL,
    called_at_utc timestamptz NOT NULL DEFAULT now(),
    response_summary jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_neuro_model_calls_status CHECK (status IN ('completed', 'failed', 'timeout', 'skipped'))
);

CREATE INDEX IF NOT EXISTS ix_platform_admin_sessions_active
    ON platform.platform_admin_sessions (token_hash, expires_at_utc)
    WHERE revoked_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_company_access_grants_company_status
    ON access.company_access_grants (company_id, status);

CREATE INDEX IF NOT EXISTS ix_company_access_grants_account
    ON access.company_access_grants (account_id);

CREATE INDEX IF NOT EXISTS ix_company_invitations_company_active
    ON access.company_invitations (company_id, expires_at_utc)
    WHERE used_at_utc IS NULL AND revoked_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_access_sessions_active
    ON access.access_sessions (token_hash, expires_at_utc)
    WHERE revoked_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_sites_company_status
    ON catalog.sites (company_id, status);

CREATE INDEX IF NOT EXISTS ix_server_nodes_company_site
    ON catalog.server_nodes (company_id, site_id);

CREATE INDEX IF NOT EXISTS ix_cameras_company_site
    ON catalog.cameras (company_id, site_id);

CREATE INDEX IF NOT EXISTS ix_zones_company_site_camera
    ON catalog.zones (company_id, site_id, camera_id);

CREATE INDEX IF NOT EXISTS ix_detection_profiles_company_scope
    ON detection.detection_profiles (company_id, site_id, camera_id, is_enabled);

CREATE INDEX IF NOT EXISTS ix_detection_profiles_type
    ON detection.detection_profiles (detection_type_id);

CREATE INDEX IF NOT EXISTS ix_incidents_company_opened
    ON incidents.incidents (company_id, opened_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_incidents_company_site_opened
    ON incidents.incidents (company_id, site_id, opened_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_incidents_company_camera_opened
    ON incidents.incidents (company_id, camera_id, opened_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_incidents_company_status_opened
    ON incidents.incidents (company_id, status, opened_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_incident_evidence_incident
    ON incidents.incident_evidence (incident_id, captured_at_utc);

CREATE INDEX IF NOT EXISTS ix_motion_frames_company_camera_captured
    ON archive.motion_frames (company_id, camera_id, captured_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_video_fragments_company_camera_started
    ON archive.video_fragments (company_id, camera_id, started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_company_created
    ON audit.audit_events (company_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_account_created
    ON audit.audit_events (account_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_server_health_checks_server_checked
    ON ops.server_health_checks (server_node_id, checked_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_background_job_runs_job_started
    ON ops.background_job_runs (job_key, started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_neuro_model_calls_profile_called
    ON ops.neuro_model_calls (detection_profile_id, called_at_utc DESC);
