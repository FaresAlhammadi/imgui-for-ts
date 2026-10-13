using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace StrayedSupply.Plugin
{
    /// systems.grab: one Strayed item. Grip near it to hold it (it follows the hand), release to throw.
    /// The torch carries a flickering light at its burning end.
    public sealed class ItemBehaviour : MonoBehaviour
    {
        internal static readonly List<ItemBehaviour> All = new List<ItemBehaviour>();

        Rigidbody body;
        Collider[] colliders;
        Transform hand;
        Vector3 heldPos;
        Quaternion heldRot;
        Light torch;
        float baseIntensity;

        public void Init(ItemRow row)
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            if (row.HoldKind == "light" && row.LightRange > 0)
            {
                torch = new GameObject("Strayed.torch.light").AddComponent<Light>();
                torch.transform.SetParent(transform, false);
                torch.transform.localPosition = new Vector3(0, row.LengthM / 2 - 0.05f, 0);   // the wrapped head
                torch.type = LightType.Point;
                torch.range = row.LightRange;
                torch.color = new Color(1f, 0.62f, 0.3f);
                torch.intensity = baseIntensity = 2f;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); if (hand != null) Release(Vector3.zero); }

        void Update()
        {
            if (torch != null) torch.intensity = baseIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, GetInstanceID()));
        }

        void LateUpdate()
        {
            if (hand != null) transform.SetPositionAndRotation(hand.TransformPoint(heldPos), hand.rotation * heldRot);
        }

        internal bool Held => hand != null;

        internal float DistanceTo(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var c in colliders) best = Mathf.Min(best, Vector3.Distance(c.ClosestPoint(p), p));
            return best;
        }

        internal void Grab(Transform by)
        {
            hand = by;
            heldPos = by.InverseTransformPoint(transform.position);
            heldRot = Quaternion.Inverse(by.rotation) * transform.rotation;
            body.isKinematic = true;
            // a held item must not shove the gorilla around
            foreach (var c in colliders) c.isTrigger = true;
        }

        internal void Release(Vector3 velocity)
        {
            hand = null;
            foreach (var c in colliders) c.isTrigger = false;
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }
    }

    /// Reads both hands every frame: grip picks up the nearest Strayed item within reach, release throws it.
    public sealed class Grabber : MonoBehaviour
    {
        const float Reach = 0.15f;
        readonly ItemBehaviour[] held = new ItemBehaviour[2];
        readonly Vector3[] lastPos = new Vector3[2];
        readonly Vector3[] velocity = new Vector3[2];

        void Update()
        {
            if (!GorillaTagger.hasInstance || ControllerInputPoller.instance == null) return;
            for (int i = 0; i < 2; i++)
            {
                XRNode node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;                                   // hooks.grip
                Transform hand = i == 0 ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;  // hooks.hands
                if (hand == null) continue;
                if (Time.deltaTime > 0) velocity[i] = Vector3.Lerp(velocity[i], (hand.position - lastPos[i]) / Time.deltaTime, 0.5f);
                lastPos[i] = hand.position;

                if (held[i] != null && (!held[i].isActiveAndEnabled || ControllerInputPoller.GetGrabReleaseMomentary(node)))
                {
                    if (held[i].isActiveAndEnabled) held[i].Release(velocity[i]);
                    held[i] = null;
                }
                else if (held[i] == null && ControllerInputPoller.GetGrabMomentary(node))
                {
                    ItemBehaviour best = null; float bestDist = Reach;
                    foreach (var item in ItemBehaviour.All)
                    {
                        if (item.Held) continue;
                        float d = item.DistanceTo(hand.position);
                        if (d < bestDist) { best = item; bestDist = d; }
                    }
                    if (best != null) { best.Grab(hand); held[i] = best; }
                }
            }
        }
    }
}
