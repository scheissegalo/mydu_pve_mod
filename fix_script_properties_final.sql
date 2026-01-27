-- Fix Properties arrays in mod_script - handles all nesting levels
-- This uses a simpler approach: update the JSON directly

-- Fix root Properties
UPDATE mod_script
SET content = jsonb_set(content, '{Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Properties') = 'array';

-- Fix Properties in Actions[0]
UPDATE mod_script
SET content = jsonb_set(content, '{Actions,0,Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Actions'->0->'Properties') = 'array';

-- Fix Properties in Actions[1]
UPDATE mod_script
SET content = jsonb_set(content, '{Actions,1,Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Actions'->1->'Properties') = 'array';

-- Fix Properties in Actions[2]
UPDATE mod_script
SET content = jsonb_set(content, '{Actions,2,Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Actions'->2->'Properties') = 'array';

-- Fix Properties in Actions[2].Actions[0] (the deeply nested one causing the error)
UPDATE mod_script
SET content = jsonb_set(content, '{Actions,2,Actions,0,Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Actions'->2->'Actions'->0->'Properties') = 'array';

-- Fix Properties in Actions[2].Actions[1] if it exists
UPDATE mod_script
SET content = jsonb_set(content, '{Actions,2,Actions,1,Properties}', '{}'::jsonb)
WHERE jsonb_typeof(content->'Actions'->2->'Actions'->1->'Properties') = 'array';

-- Verify - should all be objects now
SELECT name, 
       jsonb_typeof(content->'Properties') as root,
       jsonb_typeof(content->'Actions'->0->'Properties') as a0,
       jsonb_typeof(content->'Actions'->1->'Properties') as a1,
       jsonb_typeof(content->'Actions'->2->'Properties') as a2,
       jsonb_typeof(content->'Actions'->2->'Actions'->0->'Properties') as a2a0
FROM mod_script
WHERE name IN ('spawn-hard-pirate', 'spawn-hard-encounter');

