ALTER TABLE catalog.cameras
    ADD COLUMN IF NOT EXISTS host text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS high_quality_path text NOT NULL DEFAULT '/Streaming/Channels/101',
    ADD COLUMN IF NOT EXISTS low_quality_path text NOT NULL DEFAULT '/Streaming/Channels/102';
