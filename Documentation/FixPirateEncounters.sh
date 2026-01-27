#!/bin/bash
# Script to disable wreck encounters and add more pirate encounters

API_URL="http://localhost:5480"
TMPFILE=$(mktemp)

echo "=== Step 1: Get all encounters ==="
curl -s "$API_URL/sector/encounter" > "$TMPFILE"

if [ ! -s "$TMPFILE" ] || [ "$(cat "$TMPFILE")" = "[]" ]; then
    echo "ERROR: No encounters found or API not responding"
    rm -f "$TMPFILE"
    exit 1
fi

python3 <<PYEOF
import json, sys
try:
    with open("$TMPFILE", 'r') as f:
        encounters = json.load(f)
    
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
except json.JSONDecodeError as e:
    print(f"ERROR: Failed to parse JSON: {e}", file=sys.stderr)
    sys.exit(1)
PYEOF

echo ""
echo "=== Step 2: Disable wreck encounters ==="
echo "Run these commands to disable wreck encounters:"
echo ""
API_URL="$API_URL" python3 <<PYEOF
import json, sys
import os
api_url = os.environ.get('API_URL', 'http://localhost:5480')
try:
    with open("$TMPFILE", 'r') as f:
        encounters = json.load(f)
    
    for e in encounters:
        name = e.get('name', '').lower()
        if 'wreck' in name and 'pirate' not in name:
            print(f"curl -X PATCH {api_url}/sector/encounter/{e['id']}/active -H 'Content-Type: application/json' -d '{{\"active\":false}}'  # {e['name']}")
except json.JSONDecodeError as e:
    print(f"ERROR: Failed to parse JSON: {e}", file=sys.stderr)
    sys.exit(1)
PYEOF

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

echo ""
echo "=== Step 5: Load new sectors ==="
echo "curl -X POST '$API_URL/sector/instance/load'"

rm -f "$TMPFILE"
