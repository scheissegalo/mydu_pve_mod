-- Fix Properties arrays in mod_script table
-- This script converts all Properties arrays ([]) to empty objects ({})
-- Run this in your PostgreSQL database: psql -U dual -d dual -h localhost -p 5442 -f fix_script_properties.sql

-- Simple fix: Replace Properties arrays with empty objects
UPDATE mod_script
SET content = jsonb_set(content, '{Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Properties') = 'array';

-- Fix Properties in nested Actions (for spawn-hard-pirate)
UPDATE mod_script
SET content = (
    SELECT jsonb_set(
        content,
        '{Actions}',
        (
            SELECT jsonb_agg(
                CASE 
                    WHEN jsonb_typeof(elem->'Properties') = 'array' 
                    THEN jsonb_set(elem, '{Properties}', '{}'::jsonb)
                    ELSE elem
                END
            )
            FROM jsonb_array_elements(content->'Actions') AS elem
        )
    )
)
WHERE EXISTS (
    SELECT 1 
    FROM jsonb_array_elements(content->'Actions') AS action
    WHERE jsonb_typeof(action->'Properties') = 'array'
);

-- Verify the fix - should return 0 rows
SELECT name, 
       jsonb_typeof(content->'Properties') as root_properties_type
FROM mod_script
WHERE jsonb_typeof(content->'Properties') = 'array';

-- Verify nested Actions - should return 0 rows  
SELECT name
FROM mod_script
WHERE EXISTS (
    SELECT 1 
    FROM jsonb_array_elements(content->'Actions') AS action
    WHERE jsonb_typeof(action->'Properties') = 'array'
);
