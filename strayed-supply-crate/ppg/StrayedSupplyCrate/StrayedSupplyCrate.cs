// Strayed Supply Crate for People Playground. Compiled by the game's own mod compiler, so it uses only
// ModAPI and UnityEngine: the Strayed side (reading the player's Strayed install) runs outside the game in
// StrayedSupplyBaker, which leaves one sprite per item in strayed/<id>.png next to this file.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrayedSupplyCrate
{
    public class Mod
    {
        public const string CategoryName = "Strayed Supply";
        public const string CrateName = "Strayed Supply Crate";
        const float PixelsPerUnit = 35f, PixelsPerMetre = 50f;

        public static string ItemName(ItemRow row) { return row.DisplayName + " (Strayed)"; }

        // systems.item_register
        public static void Main()
        {
            Sprite icon = ModAPI.LoadSprite("thumb.png", 1f, true);
            ModAPI.RegisterCategory(CategoryName, "Gear from your own Strayed install", icon);
            var missing = new List<string>();
            int order = 0;
            foreach (var row in Sheets.Items)
            {
                Sprite sprite = TryLoadSprite("strayed/" + row.Id + ".png");
                if (sprite == null) { missing.Add(row.Id); continue; }
                ItemRow r = row;
                ModAPI.Register(new Modification
                {
                    OriginalItem = ModAPI.FindSpawnable(r.PpgBase),
                    NameOverride = ItemName(r),
                    NameToOrderByOverride = "strayed " + (order++),
                    DescriptionOverride = r.DisplayName + " from Strayed, read from your own install. Strayed damage: " + r.StrayedDamage + ".",
                    CategoryOverride = ModAPI.FindCategory(CategoryName),
                    ThumbnailOverride = sprite,
                    AfterSpawn = instance => Dress(instance, r, sprite)
                });
            }
            ModAPI.Register(new Modification
            {
                OriginalItem = ModAPI.FindSpawnable(Sheets.CrateBase),
                NameOverride = CrateName,
                NameToOrderByOverride = "strayed a crate",
                DescriptionOverride = "Use it to open: the Rock, Torch, Stone Hatchet and Wooden Bat from your Strayed install.",
                CategoryOverride = ModAPI.FindCategory(CategoryName),
                ThumbnailOverride = icon,
                AfterSpawn = instance => instance.AddComponent<SupplyCrate>()
            });
            if (missing.Count > 0)
                ModAPI.Notify("Strayed Supply Crate: no sprite for " + string.Join(", ", missing.ToArray()) + ". Run StrayedSupplyBaker on your Strayed install first.");
            if (GameObject.Find(MapWatcher.ObjectName) == null)
            {
                var watcher = new GameObject(MapWatcher.ObjectName);
                Object.DontDestroyOnLoad(watcher);
                watcher.AddComponent<MapWatcher>();
            }
        }

        static Sprite TryLoadSprite(string path)
        {
            try { return ModAPI.LoadSprite(path, 1f, true); }
            catch { return null; }
        }

        static void Dress(GameObject instance, ItemRow row, Sprite sprite)
        {
            var main = instance.GetComponent<SpriteRenderer>();
            foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>())
                if (sr != main) sr.enabled = false;
            main.sprite = sprite;
            Utils.FixColliders(instance);
            var body = instance.GetComponent<Rigidbody2D>();
            if (body != null) body.mass = row.MassKg;
            if (row.LightRange > 0 && instance.GetComponentInChildren<LightSprite>() == null)
                ModAPI.CreateLight(instance.transform, new Color(1f, 0.62f, 0.3f), row.LightRange * PixelsPerMetre / PixelsPerUnit, 1.5f);
        }

        // hooks.spawn_item: same path the catalog uses, so AfterSpawn and the game's bookkeeping run
        public static GameObject Spawn(string name, Vector3 at)
        {
            SpawnableAsset asset = ModAPI.FindSpawnable(name);
            if (asset == null && CatalogBehaviour.Main != null) asset = CatalogBehaviour.Main.GetSpawnable(name);
            if (asset == null || CatalogBehaviour.Main == null) { Debug.LogWarning("Strayed Supply Crate: cannot spawn " + name); return null; }
            bool flipped = false;
            return CatalogBehaviour.Main.PerformInstantiation(asset, at, ref flipped, 0f);
        }
    }

    // systems.crate_spawn (open): Use/activate releases the items once
    public class SupplyCrate : MonoBehaviour
    {
        bool opened;

        public void Use(ActivationPropagation activation)
        {
            if (opened) return;
            opened = true;
            int n = Sheets.CrateContents.Length;
            for (int i = 0; i < n; i++)
            {
                ItemRow row = Sheets.Find(Sheets.CrateContents[i]);
                Vector3 at = transform.position + new Vector3((i - (n - 1) / 2f) * 0.7f, 1.4f, 0f);
                Mod.Spawn(Mod.ItemName(row), at);
            }
            ModAPI.Notify("Strayed supply crate opened");
        }
    }

    // systems.crate_spawn (drop): a crate in view each time a map loads
    public class MapWatcher : MonoBehaviour
    {
        public const string ObjectName = "StrayedSupplyCrate.MapWatcher";
        Map lastMap;
        bool sceneChanged = true;

        void Awake() { SceneManager.sceneLoaded += (scene, mode) => sceneChanged = true; }

        void Update()
        {
            Map map = MapLoaderBehaviour.CurrentMap;                       // hooks.map_loaded
            if (map == null || (map == lastMap && !sceneChanged)) return;
            Camera view = Camera.main;                                     // hooks.view_centre
            if (view == null || CatalogBehaviour.Main == null) return;
            lastMap = map; sceneChanged = !Sheets.CrateOncePerMap;
            Vector3 c = view.transform.position;
            Mod.Spawn(Mod.CrateName, new Vector3(c.x + Sheets.CrateOffset[0], c.y + Sheets.CrateOffset[1], 0f));
        }
    }
}
