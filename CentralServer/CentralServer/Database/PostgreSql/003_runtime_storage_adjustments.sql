-- Runtime adjustments needed while CentralServer owns connector orchestration.
-- The token is required to call a protected site-side Server after restart.
-- Move this value to encrypted storage or a secret store before production rollout.

ALTER TABLE catalog.server_nodes
    ADD COLUMN IF NOT EXISTS connector_access_token text NULL;

CREATE INDEX IF NOT EXISTS ix_server_nodes_enabled
    ON catalog.server_nodes (company_id, is_enabled);
