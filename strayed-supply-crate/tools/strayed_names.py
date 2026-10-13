"""List Mesh / Texture2D names in a Strayed install whose name contains any of the given words.
Read-only, standard library only (no .NET SDK needed) - a quick way to fill items.strayedMesh /
items.strayedTexture. The plugin itself still loads the assets through StrayedIndex (AssetsTools.NET).
    python tools/strayed_names.py "<Strayed folder>" rock torch hatchet bat"""
import os, struct, sys

MESH, TEX = 43, 28

def read_names(path, want):
    with open(path, "rb") as f:
        head = f.read(48)
        meta_size, file_size, version, data_offset = struct.unpack(">IIII", head[:16])
        if version < 17 or version > 30:
            return
        pos = 20
        if version >= 22:
            meta_size, file_size, data_offset = struct.unpack(">IQQ", head[20:40]); pos = 48
        f.seek(pos); m = f.read(meta_size)
    e = "<" if head[16] == 0 else ">"
    p = m.index(b"\0") + 1                        # unity version
    p += 4                                        # platform
    tree = m[p]; p += 1
    (ntypes,) = struct.unpack_from(e + "i", m, p); p += 4
    classes = []
    for _ in range(ntypes):
        (cid,) = struct.unpack_from(e + "i", m, p); p += 4 + 1 + 2
        if cid == 114: p += 16
        p += 16
        if tree:
            nn, sb = struct.unpack_from(e + "ii", m, p); p += 8 + nn * (32 if version >= 19 else 24) + sb
            if version >= 21:
                (nd,) = struct.unpack_from(e + "i", m, p); p += 4 + 4 * nd
        classes.append(cid)
    (nobj,) = struct.unpack_from(e + "i", m, p); p += 4
    objs = []
    for _ in range(nobj):
        p = (p + 3) & ~3
        if version >= 22:
            pid, start, size, t = struct.unpack_from(e + "qqIi", m, p); p += 24
        else:
            pid, start, size, t = struct.unpack_from(e + "qIIi", m, p); p += 20
        if classes[t] in (MESH, TEX):
            objs.append((classes[t], pid, data_offset + start))
    with open(path, "rb") as f:
        for cid, pid, at in objs:
            f.seek(at); (n,) = struct.unpack(e + "i", f.read(4))
            if not 0 < n < 256: continue
            name = f.read(n).decode("utf-8", "replace")
            if any(w in name.lower() for w in want):
                yield ("mesh" if cid == MESH else "texture", name, os.path.basename(path), pid)

def main():
    folder, words = sys.argv[1], [w.lower() for w in sys.argv[2:]]
    data = next(os.path.join(folder, d) for d in os.listdir(folder) if d.endswith("_Data"))
    files = [x for x in os.listdir(data) if x.endswith(".assets") or (x.startswith("level") and "." not in x)]
    rows = []
    for x in sorted(files):
        try: rows += read_names(os.path.join(data, x), words)
        except Exception as ex: print(f"# skip {x}: {ex}", file=sys.stderr)
    for kind, name, file, pid in sorted(rows):
        print(f"{kind:8} {name:48} {file}  pathId={pid}")

if __name__ == "__main__":
    main()
