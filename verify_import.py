import json

with open("Config/sql-checks.json", "r", encoding="utf-8") as f:
    checks = json.load(f)
print(f"Total live checks after import: {len(checks)}")
print("Sample of newly imported checks:")
for i, check in enumerate(checks[-5:], len(checks) - 4):
    print(f"  {i}: {check['Id']} - {check['Name'][:50]}...")
