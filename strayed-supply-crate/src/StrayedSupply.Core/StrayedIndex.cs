using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace StrayedSupply.Core
{
    /// Reads Strayed's own meshes and textures straight out of the player's Strayed install.
    /// Nothing is copied to disk: callers get decoded meshes and GPU-ready texture data in memory.
    /// A Strayed texture in Unity's own storage layout.
    public sealed class RawTexture
    {
        public string Name;
        public int Width, Height, Format, MipCount;
        public byte[] Data;
    }

    public sealed class StrayedIndex : IDisposable
    {
        public sealed class Entry
        {
            public string Name, File;
            public AssetClassID Kind;
            public long PathId;
            public AssetsFileInstance Inst;
            public AssetFileInfo Info;
        }

        readonly AssetsManager am = new AssetsManager();
        readonly List<FileStream> streams = new List<FileStream>();
        readonly HashSet<string> ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Entry> meshes = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Entry> textures = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public string DataFolder { get; }
        public IReadOnlyDictionary<string, Entry> Meshes => meshes;
        public IReadOnlyDictionary<string, Entry> Textures => textures;

        /// Key an asset is listed under: its name, or "name#pathId" when Strayed reuses the name.
        public static string Key(Entry e) => e.Name + "#" + e.PathId;
        public bool IsAmbiguous(Entry e) => ambiguous.Contains(e.Kind + ":" + e.Name);

        Entry Find(Dictionary<string, Entry> from, string key, AssetClassID kind)
        {
            if (ambiguous.Contains(kind + ":" + key)) throw new KeyNotFoundException($"Strayed {kind} name '{key}' is used more than once; give it as '{key}#<pathId>'");
            if (!from.TryGetValue(key, out var e)) throw new KeyNotFoundException($"Strayed {kind} '{key}' not found");
            return e;
        }

        /// strayedFolder: the folder Melty passes as {game:pc-strayed}. classDataTpk: type database for
        /// IL2CPP builds whose files carry no type trees (bundled with the mod, MIT, from AssetRipper/Tpk).
        public StrayedIndex(string strayedFolder, string classDataTpk)
        {
            DataFolder = FindDataFolder(strayedFolder);
            if (classDataTpk != null && System.IO.File.Exists(classDataTpk)) am.LoadClassPackage(classDataTpk);
            try
            {
                foreach (var path in Directory.GetFiles(DataFolder, "*.assets"))
                    Scan(path);
                foreach (var path in Directory.GetFiles(DataFolder, "level*"))
                    if (Path.GetExtension(path) == "") Scan(path);
            }
            catch { Dispose(); throw; }
        }

        public static string FindDataFolder(string strayedFolder)
        {
            if (strayedFolder == null || !Directory.Exists(strayedFolder))
                throw new DirectoryNotFoundException($"Strayed folder not found: {strayedFolder}");
            foreach (var d in Directory.GetDirectories(strayedFolder, "*_Data"))
                if (System.IO.File.Exists(Path.Combine(d, "globalgamemanagers")) || System.IO.File.Exists(Path.Combine(d, "data.unity3d")))
                    return d;
            throw new DirectoryNotFoundException($"no Unity *_Data folder in {strayedFolder}");
        }

        void Scan(string path)
        {
            AssetsFileInstance inst;
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            streams.Add(fs);
            try { inst = am.LoadAssetsFile(fs, true); } catch { return; }
            if (inst.file.Metadata.TypeTreeEnabled == false)
            {
                if (am.ClassPackage == null)
                    throw new FileNotFoundException($"{Path.GetFileName(path)} has no type trees; classdata.tpk is required");
                am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
            }
            Add(inst, AssetClassID.Mesh, meshes);
            Add(inst, AssetClassID.Texture2D, textures);
        }

        void Add(AssetsFileInstance inst, AssetClassID kind, Dictionary<string, Entry> into)
        {
            foreach (var info in inst.file.GetAssetsOfType(kind))
            {
                string name;
                try { name = am.GetBaseField(inst, info)["m_Name"].AsString; } catch { continue; }
                if (string.IsNullOrEmpty(name)) continue;
                var e = new Entry { Name = name, File = Path.GetFileName(inst.path), Kind = kind, PathId = info.PathId, Inst = inst, Info = info };
                // a name Strayed reuses is ambiguous; such assets are looked up as "name#pathId"
                if (into.TryGetValue(name, out var first)) { ambiguous.Add(kind + ":" + name); into[Key(first)] = first; into[Key(e)] = e; }
                else into[name] = e;
            }
        }

        public DecodedMesh LoadMesh(string name)
        {
            var e = Find(meshes, name, AssetClassID.Mesh);
            var m = am.GetBaseField(e.Inst, e.Info);
            if (m["m_MeshCompression"].AsInt != 0) throw new NotSupportedException($"Strayed mesh '{name}' is compressed");
            var vd = m["m_VertexData"];
            var channels = new List<VertexChannel>();
            foreach (var c in vd["m_Channels"]["Array"].Children)
                channels.Add(new VertexChannel(c["stream"].AsInt, c["offset"].AsInt, c["format"].AsInt, c["dimension"].AsInt));
            byte[] data = vd["m_DataSize"].AsByteArray;
            var stream = m["m_StreamData"];
            if ((data == null || data.Length == 0) && stream != null && !stream.IsDummy && stream["size"].AsLong > 0)
                data = ReadResS(e.Inst, stream["path"].AsString, stream["offset"].AsLong, stream["size"].AsLong);
            return MeshDecoder.Decode(e.Name, vd["m_VertexCount"].AsInt, channels, data,
                                      m["m_IndexBuffer"]["Array"].AsByteArray, m["m_IndexFormat"].AsInt == 1);
        }

        /// Returns the texture exactly as Unity stores it (Unity TextureFormat id, full mip chain), ready for
        /// Texture2D.LoadRawTextureData in the host game: no decoding, the GPU reads it as is.
        public RawTexture LoadTextureRaw(string name)
        {
            var e = Find(textures, name, AssetClassID.Texture2D);
            var t = am.GetBaseField(e.Inst, e.Info);
            byte[] data = t["image data"].AsByteArray;
            var stream = t["m_StreamData"];
            if ((data == null || data.Length == 0) && stream != null && !stream.IsDummy && stream["size"].AsLong > 0)
                data = ReadResS(e.Inst, stream["path"].AsString, stream["offset"].AsLong, stream["size"].AsLong);
            if (data == null || data.Length == 0) throw new InvalidDataException($"Strayed texture '{name}' has no pixel data");
            return new RawTexture
            {
                Name = e.Name, Width = t["m_Width"].AsInt, Height = t["m_Height"].AsInt,
                Format = t["m_TextureFormat"].AsInt, MipCount = t["m_MipCount"].AsInt, Data = data
            };
        }

        byte[] ReadResS(AssetsFileInstance inst, string resPath, long offset, long size)
        {
            string file = Path.Combine(Path.GetDirectoryName(inst.path), Path.GetFileName(resPath));
            using (var fs = System.IO.File.OpenRead(file))
            {
                var buf = new byte[size];
                fs.Seek(offset, SeekOrigin.Begin);
                int read = 0;
                while (read < size) { int n = fs.Read(buf, read, (int)(size - read)); if (n <= 0) break; read += n; }
                return buf;
            }
        }

        public void Dispose()
        {
            am.UnloadAll(true);
            foreach (var s in streams) s.Dispose();
            streams.Clear();
        }
    }
}
