-- This script creates the missing faction-territory link needed for sector generation
-- Run this in your PostgreSQL database if sectors aren't being generated

-- First, check if the default faction exists (should be id=1)
SELECT id, name, tag FROM mod_faction WHERE id = 1;

-- Check if the default territory exists (should match your encounters' territoryId)
-- Your encounters show territoryId: b3f964e2-fe1a-4cda-8ff5-dce74a7988bd
SELECT id, name FROM mod_territory WHERE id = 'b3f964e2-fe1a-4cda-8ff5-dce74a7988bd';

-- Check if faction-territory link already exists
SELECT * FROM mod_faction_territory 
WHERE faction_id = 1 
  AND territory_id = 'b3f964e2-fe1a-4cda-8ff5-dce74a7988bd';

-- If the above returns empty, create the link:
INSERT INTO mod_faction_territory (faction_id, territory_id, permanent, active, sector_count)
VALUES (
    1,  -- Default faction (Pirates)
    'b3f964e2-fe1a-4cda-8ff5-dce74a7988bd',  -- Your territory ID
    false,  -- Not permanent
    true,   -- Active
    10      -- Number of sectors to generate (default)
)
ON CONFLICT DO NOTHING;

-- Verify it was created
SELECT * FROM mod_faction_territory 
WHERE faction_id = 1 
  AND territory_id = 'b3f964e2-fe1a-4cda-8ff5-dce74a7988bd';


