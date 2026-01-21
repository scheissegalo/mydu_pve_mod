#!/bin/bash
# Script to disable wreck encounters and add more pirate encounters

API_URL="http://localhost:5480"

echo "=== Step 1: Get all encounters ==="
ENCOUNTERS=$(curl -s "$API_URL/sector/encounter")

echo "$ENCOUNTERS" | python3 - <<'PY'
import json, sys
encounters = json.load(sys.stdin)
wreck_ids = []
pirate_id = None

for e in encounters:
    name = e.get('name', '').lower()
    if 'wreck' in name and 'pirate' not in name:
        wreck_ids.append(e['id'])
        print(f"Found wreck encounter: {e['name']} (ID: {e['id']})")
    elif 'pirate' in name.lower():
        pirate_id = e['id']
        print(f"Found pirate encounter: {e['name']} (ID: {e['id']})")

print(f"\nWreck IDs to disable: {wreck_ids}")
print(f"Pirate ID: {pirate_id}")
PY

echo ""
echo "=== Step 2: Disable wreck encounters ==="
echo "Run these commands to disable wreck encounters:"
echo ""
echo "$ENCOUNTERS" | python3 - <<'PY'
import json, sys
encounters = json.load(sys.stdin)
for e in encounters:
    name = e.get('name', '').lower()
    if 'wreck' in name and 'pirate' not in name:
        print(f"curl -X PATCH {sys.argv[1]}/sector/encounter/{e['id']}/active -H 'Content-Type: application/json' -d '{{\"active\":false}}'", end='')
        print(f"  # {e['name']}")
PY
"$API_URL"

echo ""
echo ""
echo "=== Step 3: Add more pirate encounters ==="
echo "Run these commands to add more pirate encounters:"
for i in {1..9}; do
    echo "curl -X PUT '$API_URL/sector/encounter/npc?POIScript=spawn-basic-poi&NpcScript=spawn-basic-pirate'"
done

echo ""
echo "=== Step 4: Force regenerate sectors ==="
echo "curl -X POST '$API_URL/sector/instance/expire/force/all'"

