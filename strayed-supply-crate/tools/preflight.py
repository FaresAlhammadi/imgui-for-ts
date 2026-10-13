"""Preflight: lay every sheet over the others and list unfilled cells, unverified rows and references
that do not resolve; then scan the People Playground mod source for identifiers the game's sandboxed
mod compiler rejects. Exit code 1 when anything is open."""
import json, os, re, sys

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

# ppgModCompiler (People Playground 1.27.18) rejects these namespaces and identifiers
FORBIDDEN_USING = ["System.Net", "System.Security", "System.Web", "System.Diagnostics", "System.Reflection",
                   "System.IO", "UnityEngine.Networking", "Steamworks"]
FORBIDDEN_IDS = {"Activator", "AppDomain", "Application", "Assembly", "BinaryFormatter", "CodeDom", "Directory",
                 "DirectoryInfo", "DynamicMethod", "Expressions", "File", "FileInfo", "FileStream", "GetField",
                 "GetMethod", "GetProperty", "HttpWebRequest", "InteropServices", "MethodInfo", "Process",
                 "Remoting", "StreamReader", "StreamWriter", "TypeDescriptor", "UnityWebRequest", "WebClient",
                 "WebRequest", "WebSocket", "Win32"}
mod = os.path.join(ROOT, "ppg", "StrayedSupplyCrate")
meta = json.load(open(os.path.join(mod, "mod.json"), encoding="utf-8"))
for script in meta["Scripts"]:
    path = os.path.join(mod, script)
    if not os.path.exists(path):
        problems.append(f"mod.json Scripts: {script} does not exist")
        continue
    code = re.sub(r"//[^\n]*|\"(?:\\.|[^\"\\])*\"", "", open(path, encoding="utf-8").read())
    for u in re.findall(r"^\s*using\s+([\w.]+)\s*;", code, re.M):
        if any(u == f or u.startswith(f + ".") for f in FORBIDDEN_USING):
            problems.append(f"{script}: 'using {u}' is rejected by ppgModCompiler")
    for ident in sorted(set(re.findall(r"\b[A-Za-z_]\w*\b", code)) & FORBIDDEN_IDS):
        problems.append(f"{script}: identifier '{ident}' is rejected by ppgModCompiler")
if not os.path.exists(os.path.join(mod, meta["ThumbnailPath"])):
    problems.append("mod.json ThumbnailPath: file missing (run tools/gen.py)")

for p in problems:
    print("OPEN  " + p)
print(f"\n{len(problems)} open cell(s)" if problems else "preflight clean")
sys.exit(1 if problems else 0)
