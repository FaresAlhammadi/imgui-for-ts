using System;
using UnityEngine;

namespace StrayedSupply.Plugin
{
    /// systems.item_combat: the torch gives light; melee items report hits on monsters.
    /// Damage needs hooks.monster_hit (Animal Company's monster health class), still open in the sheet.
    public sealed class ItemBehaviour : MonoBehaviour
    {
        public ItemBehaviour(IntPtr ptr) : base(ptr) { }
        string id; float damage;

        public void Init(ItemRow row)
        {
            id = row.Id; damage = row.AcDamage;
            if (row.HoldKind == "light" && row.LightRange > 0)
            {
                var l = new GameObject("Strayed.torch.light").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point; l.range = row.LightRange; l.color = new Color(1f, 0.62f, 0.3f); l.intensity = 2f;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (float.IsNaN(damage) || c.relativeVelocity.magnitude < 2f) return;
            Plugin.L.LogInfo($"{id} hit {c.gameObject.name} at {c.relativeVelocity.magnitude:0.0} m/s");
        }
    }
}
