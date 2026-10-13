using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;

namespace StrayedSupply.Core
{
    /// Reads Strayed's own meshes and textures straight out of the player's Strayed install.
    /// Nothing is copied to disk: callers get decoded arrays in memory.
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
        readonly Dictionary<string, Entry> meshes = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Entry> textures = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public string DataFolder { get; }
        public IReadOnlyDictionary<string, Entry> Meshes => meshes;
        public IReadOnlyDictionary<string, Entry> Textures => textures;

        /// strayedFolder: the folder Melty passes as {game:pc-strayed}. classDataTpk: type database for
        /// IL2CPP builds whose files carry no type trees (bundled with the mod, MIT, from AssetRipper/Tpk).
        public StrayedIndex(string strayedFolder, string classDataTpk)
        {
            DataFolder = FindDataFolder(strayedFolder);
            if (classDataTpk != null && System.IO.File.Exists(classDataTpk)) am.LoadClassPackage(classDataTpk);
            foreach (var path in Directory.GetFiles(DataFolder, "*.assets"))
                Scan(path);
            foreach (var path in Directory.GetFiles(DataFolder, "level*"))
                if (Path.GetExtension(path) == "") Scan(path);
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
            try { inst = am.LoadAssetsFile(path, true); } catch { return; }
            if (inst.file.Metadata.TypeTreeEnabled == false)
                am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
            Add(inst, AssetClassID.Mesh, meshes);
            Add(inst, AssetClassID.Texture2D, textures);
        }

        void Add(AssetsFileInstance inst, AssetClassID kind, Dictionary<string, Entry> into)
        {
            foreach (var info in inst.file.GetAssetsOfType(kind))
            {
                string name;
                try { name = am.GetBaseField(inst, info)["m_Name"].AsString; } catch { continue; }
                if (string.IsNullOrEmpty(name) || into.ContainsKey(name)) continue;
                into[name] = new Entry { Name = name, File = Path.GetFileName(inst.path), Kind = kind, PathId = info.PathId, Inst = inst, Info = info };
            }
        }

        public DecodedMesh LoadMesh(string name)
        {
            if (!meshes.TryGetValue(name, out var e)) throw new KeyNotFoundException($"Strayed mesh '{name}' not found");
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
            return MeshDecoder.Decode(name, vd["m_VertexCount"].AsInt, channels, data,
                                      m["m_IndexBuffer"]["Array"].AsByteArray, m["m_IndexFormat"].AsInt == 1);
        }

        /// Returns BGRA32 pixels, width and height.
        public byte[] LoadTexture(string name, out int width, out int height)
        {
            if (!textures.TryGetValue(name, out var e)) throw new KeyNotFoundException($"Strayed texture '{name}' not found");
            var tf = TextureFile.ReadTextureFile(am.GetBaseField(e.Inst, e.Info));
            width = tf.m_Width; height = tf.m_Height;
            return tf.DecodeTextureRaw(tf.FillPictureData(e.Inst));
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

        public void Dispose() => am.UnloadAll(true);
    }
}
