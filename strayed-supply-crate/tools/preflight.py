"""Preflight: lay every sheet over the others and list unfilled cells, unverified rows and references
that do not resolve. Exit code 1 when anything is open."""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sheets = {}
for f in sorted(os.listdir(os.path.join(ROOT, "sheets"))):
    if f.endswith(".json"):
        s = json.load(open(os.path.join(ROOT, "sheets", f), encoding="utf-8"))
        sheets[s["sheet"]] = s

ids = {name: {r["id"] for r in s["rows"]} for name, s in sheets.items()}
problems = []

def ref(where, sheet, value):
    if value not in ids.get(sheet, ()):
        problems.append(f"{where}: '{value}' is not a row in sheet '{sheet}'")

for name, s in sheets.items():
    cols = s["columns"]
    for r in s["rows"]:
        where = f"{name}.{r.get('id')}"
        for c in cols:
            if c not in r:
                problems.append(f"{where}.{c}: missing")
            elif r[c] is None:
                problems.append(f"{where}.{c}: unfilled")
        for c in r:
            if c not in cols:
                problems.append(f"{where}.{c}: column not declared")
        if r.get("verified") is False:
            problems.append(f"{where}: not verified against the real game")

for r in sheets["crate"]["rows"]:
    ref(f"crate.{r['id']}.spawnHook", "hooks", r["spawnHook"])
    for i in r["contents"]:
        ref(f"crate.{r['id']}.contents", "items", i)
for r in sheets["systems"]["rows"]:
    for u in r["uses"]:
        sh, _, rid = u.partition(".")
        ref(f"systems.{r['id']}.uses", sh, rid)
    if not os.path.exists(os.path.join(ROOT, r["code"])):
        problems.append(f"systems.{r['id']}.code: {r['code']} does not exist")

for p in problems:
    print("OPEN  " + p)
print(f"\n{len(problems)} open cell(s)" if problems else "preflight clean")
sys.exit(1 if problems else 0)
