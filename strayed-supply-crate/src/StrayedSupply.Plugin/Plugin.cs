using System;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using StrayedSupply.Core;

namespace StrayedSupply.Plugin
{
    [BepInPlugin(Guid, "Strayed Supply Crate", "0.1.0")]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "melty.strayed-supply-crate";
        internal static ManualLogSource L;
        internal static Task<StrayedIndex> Index;

        public override void Load()
        {
            L = Log;
            // Melty passes the player's own Strayed folder ({game:pc-strayed}) in this variable.
            string strayed = Environment.GetEnvironmentVariable("STRAYED_SUPPLY_STRAYED_DIR");
            string tpk = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "classdata.tpk");
            Index = Task.Run(() =>
            {
                var idx = new StrayedIndex(strayed, tpk);
                L.LogInfo($"Strayed index: {idx.Meshes.Count} meshes, {idx.Textures.Count} textures in {idx.DataFolder}");
                return idx;
            });
            AddComponent<CrateSpawner>();
        }
    }
}
