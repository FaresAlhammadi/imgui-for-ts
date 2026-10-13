using System;
using System.Collections.Generic;
using StrayedSupply.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrayedSupply.Plugin
{
    /// systems.crate_spawn + room_gate: an open Strayed supply crate with the four items in front of the
    /// player when the level starts; hidden whenever the player is in a public lobby.
    public sealed class CrateSpawner : MonoBehaviour
    {
        // crate outer size in metres: width (x), wall height (y), depth (z); board thickness
        const float W = 0.7f, H = 0.32f, D = 0.42f, T = 0.025f;

        GameObject root;
        bool spawned, faultLogged, gateLogged;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        void Awake()
        {
            // the crate lives in the scene; once it is gone (scene reload) the next level start gets a new one
            SceneManager.activeSceneChanged += (from, to) =>
            {
                if (root != null && !Sheets.CrateOncePerRun) Destroy(root);
                if (root == null || !Sheets.CrateOncePerRun) spawned = false;
            };
        }

        void Update()
        {
            if (Plugin.Assets == null || !Plugin.Assets.IsCompleted) return;
            if (Plugin.Assets.IsFaulted)
            {
                if (!faultLogged) Plugin.L.LogError(Plugin.Assets.Exception.GetBaseException());
                faultLogged = true;
                return;
            }
            bool allowed = RoomAllows();
            if (root != null && root.activeSelf != allowed)
            {
                root.SetActive(allowed);
                Plugin.L.LogInfo(allowed ? "Strayed supply crate shown (offline or private room)" : "Strayed supply crate hidden (public lobby)");
            }
            if (!allowed && !gateLogged) { Plugin.L.LogInfo("public lobby: Strayed supply crate waits for a private room"); gateLogged = true; }
            if (!allowed || spawned) return;
            if (!GorillaTagger.hasInstance || GorillaTagger.Instance.mainCamera == null) return;   // hooks.player_ready
            Spawn(GorillaTagger.Instance.mainCamera.transform);                                   // hooks.player_head
            spawned = true;
        }

        // hooks.room_gate: Another Axiom's mod policy keeps gameplay mods out of public lobbies
        static bool RoomAllows()
        {
            var net = NetworkSystem.Instance;
            return net == null || !net.InRoom || net.SessionIsPrivate;
        }

        void Spawn(Transform head)
        {
            var assets = Plugin.Assets.Result;
            var o = Sheets.CrateOffset;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 at = head.position + right * o[0] + Vector3.up * o[1] + fwd * o[2];
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                at.y = hit.point.y;

            root = new GameObject("StrayedSupplyCrate");
            root.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-fwd, Vector3.up));
            Material wood = CrateMaterial(assets.CrateTexture);
            Board("Floor", new Vector3(0, T / 2, 0), new Vector3(W, T, D), wood);
            Board("Front", new Vector3(0, H / 2, D / 2 - T / 2), new Vector3(W, H, T), wood);
            Board("Back", new Vector3(0, H / 2, -D / 2 + T / 2), new Vector3(W, H, T), wood);
            Board("Left", new Vector3(-W / 2 + T / 2, H / 2, 0), new Vector3(T, H, D - 2 * T), wood);
            Board("Right", new Vector3(W / 2 - T / 2, H / 2, 0), new Vector3(T, H, D - 2 * T), wood);

            int n = Sheets.CrateContents.Length, built = 0;
            for (int i = 0; i < n; i++)
            {
                var row = Array.Find(Sheets.Items, r => r.Id == Sheets.CrateContents[i]);
                if (!assets.Items.TryGetValue(row.Id, out var a)) continue;
                var item = BuildItem(row, a.mesh, a.tex);
                // stand the items in a row inside the crate, leaning back against the rear boards
                float x = -W / 2 + W * (i + 0.5f) / n;
                float lean = row.LengthM > H ? 12f : 0f;
                item.transform.localPosition = new Vector3(x, T + row.LengthM / 2 + 0.01f, -0.04f);
                item.transform.localRotation = Quaternion.Euler(-lean, 0, 0);
                built++;
            }
            Plugin.L.LogInfo($"Strayed supply crate spawned at {at} with {built} items");
        }

        void Board(string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        GameObject BuildItem(ItemRow row, DecodedMesh d, RawTexture tex)
        {
            var mesh = new Mesh { name = "Strayed." + d.Name };
            if (d.Positions.Length / 3 > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = ToV3(d.Positions);
            if (d.Normals != null) mesh.normals = ToV3(d.Normals);
            if (d.Uv0 != null) mesh.uv = ToV2(d.Uv0);
            mesh.triangles = d.Indices;
            if (d.Normals == null) mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Strayed." + row.Id);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = TexturedMaterial(row.Id, tex, Color.white);
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center; box.size = mesh.bounds.size;
            var body = go.AddComponent<Rigidbody>();
            body.mass = row.MassKg;
            body.isKinematic = true;                      // rests in the crate until grabbed
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<ItemBehaviour>().Init(row);
            return go;
        }

        Material CrateMaterial(RawTexture tex) =>
            TexturedMaterial("crate", tex, tex == null ? new Color(0.55f, 0.36f, 0.2f) : Color.white);

        Material TexturedMaterial(string key, RawTexture tex, Color tint)
        {
            if (materials.TryGetValue(key, out var m)) return m;
            m = new Material(LitShader()) { name = "Strayed." + key, color = tint };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            var t = ToTexture(tex);
            if (t != null)
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                    if (m.HasProperty(prop)) m.SetTexture(prop, t);
            return materials[key] = m;
        }

        // hooks.shader
        static Shader LitShader()
        {
            foreach (var name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit", "GorillaTag/UberShader", "Standard", "Unlit/Texture" })
            {
                var s = Shader.Find(name);
                if (s != null) return s;
            }
            throw new InvalidOperationException("no usable shader found");
        }

        /// Strayed textures are uploaded in Unity's own compressed layout (DXT1 for these items): no decoding.
        static Texture2D ToTexture(RawTexture raw)
        {
            if (raw == null) return null;
            try
            {
                var t = new Texture2D(raw.Width, raw.Height, (TextureFormat)raw.Format, Math.Max(1, raw.MipCount), false) { name = "Strayed." + raw.Name };
                t.LoadRawTextureData(raw.Data);
                t.Apply(false, true);
                return t;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"texture {raw.Name} (format {raw.Format}, {raw.Width}x{raw.Height}): {e.Message}");
                return null;
            }
        }

        static Vector3[] ToV3(float[] f) { var v = new Vector3[f.Length / 3]; for (int i = 0; i < v.Length; i++) v[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]); return v; }
        static Vector2[] ToV2(float[] f) { var v = new Vector2[f.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(f[i * 2], f[i * 2 + 1]); return v; }
    }
}
