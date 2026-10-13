using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;
using StrayedSupply.Core;

namespace StrayedSupply.Plugin
{
    /// systems.crate_spawn: puts the Strayed supply crate in front of the player when a level loads.
    public sealed class CrateSpawner : MonoBehaviour
    {
        public CrateSpawner(IntPtr ptr) : base(ptr) { }

        bool pending = true;
        readonly Dictionary<string, (Mesh mesh, Texture2D tex)> cache = new Dictionary<string, (Mesh, Texture2D)>();

        void Awake() => SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)((s, m) => pending = true));

        void Update()
        {
            if (!pending || !Plugin.Index.IsCompleted) return;
            if (Plugin.Index.IsFaulted) { Plugin.L.LogError(Plugin.Index.Exception); pending = false; return; }
            var head = Camera.main;                      // hooks.player_head
            if (head == null) return;
            pending = !Sheets.CrateOncePerRun;
            Spawn(head.transform);
        }

        void Spawn(Transform head)
        {
            var o = Sheets.CrateOffset;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 at = head.position + head.right * o[0] + Vector3.up * o[1] + fwd * o[2];
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "StrayedSupplyCrate";
            crate.transform.position = at;
            crate.transform.localScale = new Vector3(0.6f, 0.35f, 0.4f);
            int i = 0;
            foreach (var id in Sheets.CrateContents)
            {
                var row = Array.Find(Sheets.Items, r => r.Id == id);
                var go = Build(row);
                if (go == null) continue;
                go.transform.position = at + Vector3.up * 0.35f + head.right * (-0.24f + 0.16f * i++);
            }
            Plugin.L.LogInfo($"Strayed supply crate spawned at {at}");
        }

        GameObject Build(ItemRow row)
        {
            if (row.StrayedMesh == null) { Plugin.L.LogWarning($"{row.Id}: strayedMesh not set in items sheet"); return null; }
            if (!cache.TryGetValue(row.Id, out var asset))
            {
                var idx = Plugin.Index.Result;
                DecodedMesh d = idx.LoadMesh(row.StrayedMesh);
                var mesh = new Mesh { name = "Strayed." + d.Name };
                if (d.Indices.Length > 65535 || d.Positions.Length / 3 > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = ToV3(d.Positions);
                if (d.Normals != null) mesh.normals = ToV3(d.Normals);
                if (d.Uv0 != null) mesh.uv = ToV2(d.Uv0);
                mesh.triangles = d.Indices;
                if (d.Normals == null) mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                Texture2D tex = null;
                if (row.StrayedTexture != null)
                {
                    byte[] px = idx.LoadTexture(row.StrayedTexture, out int w, out int h);
                    tex = new Texture2D(w, h, TextureFormat.BGRA32, false);
                    tex.LoadRawTextureData((Il2CppStructArray<byte>)px);
                    tex.Apply();
                }
                cache[row.Id] = asset = (mesh, tex);
            }
            var go = new GameObject("Strayed." + row.Id);
            go.AddComponent<MeshFilter>().sharedMesh = asset.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            if (asset.tex != null) mr.material.mainTexture = asset.tex;
            go.AddComponent<MeshCollider>().convex = true;
            go.GetComponent<MeshCollider>().sharedMesh = asset.mesh;
            go.AddComponent<Rigidbody>().mass = row.MassKg;
            go.AddComponent<ItemBehaviour>().Init(row);
            return go;
        }

        static Vector3[] ToV3(float[] f) { var v = new Vector3[f.Length / 3]; for (int i = 0; i < v.Length; i++) v[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]); return v; }
        static Vector2[] ToV2(float[] f) { var v = new Vector2[f.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(f[i * 2], f[i * 2 + 1]); return v; }
    }
}
