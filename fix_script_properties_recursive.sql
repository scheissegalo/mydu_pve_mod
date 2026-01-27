-- Recursively fix Properties arrays in mod_script table
-- This handles nested Actions at any depth

-- Create a recursive function to fix Properties arrays at any nesting level
CREATE OR REPLACE FUNCTION fix_properties_recursive(json_data jsonb)
RETURNS jsonb AS $$
DECLARE
    result jsonb;
    fixed_action jsonb;
    fixed_actions jsonb := '[]'::jsonb;
    i int;
BEGIN
    result := json_data;
    
    -- Fix root Properties if it's an array
    IF jsonb_typeof(result->'Properties') = 'array' THEN
        result := result || '{"Properties": {}}'::jsonb;
    END IF;
    
    -- Recursively fix Actions array if it exists
    IF jsonb_typeof(result->'Actions') = 'array' THEN
        FOR i IN 0..jsonb_array_length(result->'Actions') - 1 LOOP
            -- Recursively fix each action (this handles nested Actions)
            fixed_action := fix_properties_recursive(result->'Actions'->i);
            fixed_actions := fixed_actions || jsonb_build_array(fixed_action);
        END LOOP;
        
        -- Remove the first empty array element we started with
        fixed_actions := (SELECT jsonb_agg(elem) FROM jsonb_array_elements(fixed_actions) elem);
        result := result || jsonb_build_object('Actions', fixed_actions);
    END IF;
    
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- Update all scripts using the recursive function
UPDATE mod_script
SET content = fix_properties_recursive(content)
WHERE name IN ('spawn-hard-pirate', 'spawn-hard-encounter');

-- Clean up the function
DROP FUNCTION IF EXISTS fix_properties_recursive(jsonb);

-- Verify the fix - should return 0 rows
SELECT name, 
       jsonb_typeof(content->'Properties') as root_props_type,
       (SELECT COUNT(*) 
        FROM jsonb_array_elements(content->'Actions') AS action
        WHERE jsonb_typeof(action->'Properties') = 'array') as level1_arrays,
       (SELECT COUNT(*) 
        FROM jsonb_array_elements(content->'Actions') AS action1,
             jsonb_array_elements(action1->'Actions') AS action2
        WHERE jsonb_typeof(action2->'Properties') = 'array') as level2_arrays
FROM mod_script
WHERE name IN ('spawn-hard-pirate', 'spawn-hard-encounter');

