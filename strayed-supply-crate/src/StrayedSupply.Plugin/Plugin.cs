using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using StrayedSupply.Core;

namespace StrayedSupply.Plugin
{
    /// The items, decoded from the player's Strayed install off the main thread.
    public sealed class StrayedAssets
    {
        public readonly Dictionary<string, (DecodedMesh mesh, RawTexture tex)> Items = new Dictionary<string, (DecodedMesh, RawTexture)>();
        public RawTexture CrateTexture;
    }

    [BepInPlugin(Guid, "Strayed Supply Crate", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "melty.strayed-supply-crate", Version = "0.3.0";
        internal static ManualLogSource L;
        internal static Task<StrayedAssets> Assets;

        void Awake()
        {
            L = Logger;
            var folder = Config.Bind("General", "StrayedFolder", "", "Leave empty to find Strayed automatically");
            string strayed = FindStrayed(folder.Value);
            string tpk = Path.Combine(Path.GetDirectoryName(Info.Location), "classdata.tpk");
            Assets = Task.Run(() => Load(strayed, tpk));
            gameObject.AddComponent<CrateSpawner>();
            gameObject.AddComponent<Grabber>();
        }

        // systems.strayed_index + mesh_fit: read each item from the player's Strayed install
        static StrayedAssets Load(string strayed, string tpk)
        {
            if (strayed == null) throw new DirectoryNotFoundException("Strayed not found; set StrayedFolder in BepInEx/config/" + Guid + ".cfg");
            var watch = Stopwatch.StartNew();
            var outp = new StrayedAssets();
            using (var idx = new StrayedIndex(strayed, tpk))
            {
                foreach (var row in Sheets.Items)
                {
                    var mesh = MeshFit.Upright(idx.LoadMesh(row.StrayedMesh), row.LengthM);
                    outp.Items[row.Id] = (mesh, idx.LoadTextureRaw(row.StrayedTexture));
                }
                try { outp.CrateTexture = idx.LoadTextureRaw(Sheets.CrateTexture); }
                catch (Exception e) { L.LogWarning("crate texture: " + e.Message); }
                L.LogInfo($"Strayed: {outp.Items.Count} items read from {idx.DataFolder} in {watch.ElapsedMilliseconds} ms");
            }
            return outp;
        }

        /// Config value, then Melty's STRAYED_SUPPLY_STRAYED_DIR, then every Steam library next to Gorilla Tag's.
        static string FindStrayed(string configured)
        {
            var candidates = new List<string> { configured, Environment.GetEnvironmentVariable("STRAYED_SUPPLY_STRAYED_DIR") };
            string common = Path.GetDirectoryName(Paths.GameRootPath);                 // ...\steamapps\common
            candidates.Add(Path.Combine(common, "Strayed"));
            string vdf = Path.Combine(Path.GetDirectoryName(common), "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (var line in File.ReadAllLines(vdf))
                {
                    var parts = line.Trim().Split('"');
                    if (parts.Length >= 4 && parts[1] == "path")
                        candidates.Add(Path.Combine(parts[3].Replace("\\\\", "\\"), "steamapps", "common", "Strayed"));
                }
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && Directory.Exists(Path.Combine(c, "Strayed_Data"))) return c;
            return null;
        }
    }
}
