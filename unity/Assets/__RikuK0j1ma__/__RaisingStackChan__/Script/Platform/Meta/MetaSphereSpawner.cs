using System;
using System.Collections.Generic;
using System.Text;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Food spheres on Meta Quest. Spawning input and the spheres themselves. A sphere is an instance of SphereConfig.Prefab
    /// (no physics, no Interaction SDK component); holding is done by this class:
    /// - Spawn: A / X button (controller) or middle finger pinch (hand tracking), at the hand position.
    /// - Grab: grip button (controller) or index finger pinch (hand). The nearest sphere within reach follows the hand
    ///   and stays where it is let go.
    /// Spheres too far from the headset are recalled.
    /// </summary>
    public sealed class MetaSphereSpawner : ISphereSpawner, ITickable, IDiagnosticsSource
    {
        private readonly SphereConfig config;
        private readonly Dictionary<int, Sphere> spheres = new Dictionary<int, Sphere>();
        private readonly List<int> scratch = new List<int>();
        private readonly StringBuilder sb = new StringBuilder();

        private Transform root;
        private int nextId;

        private OVRCameraRig rig;
        private readonly OVRHand[] hands = new OVRHand[2]; // 0 = left, 1 = right
        private readonly bool[] prevHandSpawn = new bool[2];
        private float nextHandFindTime;

        private const float GrabReach = 0.15f;
        private const float SpawnForward = 0.1f;

        // Hold slots: 0 = left controller, 1 = right controller, 2 = left hand, 3 = right hand.
        private readonly Sphere[] holding = new Sphere[4];
        private readonly Vector3[] holdOffset = new Vector3[4];
        private readonly bool[] prevPressed = new bool[4];

        private string controllerStatus = "";
        private string handStatus = "";

        public MetaSphereSpawner(SphereConfig config)
        {
            this.config = config;
        }

        public event Action<int> Spawned;

        public int Spawn(Vector3 position)
        {
            EnsureRoot();
            while (spheres.Count >= config.MaxCount && RemoveOldestFree()) { }
            if (spheres.Count >= config.MaxCount) return -1;

            if (config.Prefab == null)
            {
                Debug.LogError("[Sphere] SphereConfig has no Prefab assigned.");
                return -1;
            }

            var go = Object.Instantiate(config.Prefab, position, Quaternion.identity, root);
            go.name = "FoodSphere";
            go.transform.localScale = Vector3.one * (config.Radius * 2f);

            var id = nextId++;
            spheres[id] = new Sphere(id, go.transform);
            Spawned?.Invoke(id);
            return id;
        }

        public bool TryGetPosition(int id, out Vector3 position)
        {
            if (TryGet(id, out var sphere))
            {
                position = sphere.Transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        public void Despawn(int id)
        {
            if (!spheres.TryGetValue(id, out var sphere)) return;
            spheres.Remove(id);
            if (sphere.Transform != null) Object.Destroy(sphere.Transform.gameObject);
        }

        public bool IsHeld(int id) => TryGet(id, out var sphere) && sphere.IsHeld;

        public void Tick()
        {
            HandleSpawnInput();
            Sweep();
        }

        public string DescribeStatus()
        {
            var held = 0;
            foreach (var sphere in spheres.Values)
            {
                if (sphere.Transform != null && sphere.IsHeld) held++;
            }

            sb.Length = 0;
            sb.AppendLine($"Spheres: {spheres.Count} (held {held})");
            sb.AppendLine(controllerStatus);
            sb.Append(handStatus);
            return sb.ToString();
        }

        private void HandleSpawnInput()
        {
            if (rig == null)
            {
                rig = Object.FindAnyObjectByType<OVRCameraRig>();
                if (rig != null) DisableLocomotion();
            }

            if (rig == null)
            {
                controllerStatus = "OVRCameraRig not found";
                return;
            }

#if UNITY_EDITOR
            // Editor without a headset: Space spawns in front of the camera.
            if (Input.GetKeyDown(KeyCode.Space)) Spawn(InFrontOfHead());
#endif
            var left = SpawnFromController(OVRInput.Controller.LTouch, rig.leftControllerAnchor);
            var right = SpawnFromController(OVRInput.Controller.RTouch, rig.rightControllerAnchor);
            UpdateControllerHold(0, OVRInput.Controller.LTouch, rig.leftControllerAnchor);
            UpdateControllerHold(1, OVRInput.Controller.RTouch, rig.rightControllerAnchor);
            controllerStatus = $"Controller X(L): {left}  A(R): {right}";

            FindHands();
            handStatus = $"Hand L: {SpawnFromHand(0)}  R: {SpawnFromHand(1)}";
            UpdateHandHold(2, 0);
            UpdateHandHold(3, 1);
        }

        /// <summary>Spawns on the A / X press. Returns a short status text.</summary>
        private string SpawnFromController(OVRInput.Controller controller, Transform anchor)
        {
            var tracked = anchor != null && OVRInput.GetControllerPositionTracked(controller);
            if (OVRInput.GetDown(OVRInput.Button.One, controller)) Spawn(tracked ? anchor.position + anchor.forward * SpawnForward : InFrontOfHead());
            if (OVRInput.Get(OVRInput.Button.One, controller)) return "pressed";
            return tracked ? "tracked" : "not tracked";
        }

        /// <summary>
        /// The interaction rig ships a FirstPersonLocomotor that applies gravity to the rig (CharacterController).
        /// This MR scene has no floor collider, so the whole OVRCameraRig would fall forever. Walking is not needed.
        /// </summary>
        private static void DisableLocomotion()
        {
            foreach (var locomotor in Object.FindObjectsByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(
                         FindObjectsSortMode.None))
            {
                locomotor.enabled = false;
            }
        }

        private void UpdateControllerHold(int slot, OVRInput.Controller controller, Transform anchor)
        {
            var tracked = anchor != null && OVRInput.GetControllerPositionTracked(controller);
            var pressed = tracked && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controller);
            UpdateHold(slot, tracked ? anchor : null, pressed);
        }

        private void UpdateHandHold(int slot, int handIndex)
        {
            var hand = hands[handIndex];
            var ok = hand != null && hand.IsTracked && hand.HandConfidence == OVRHand.TrackingConfidence.High;
            var pivot = ok ? (hand.IsPointerPoseValid && hand.PointerPose != null ? hand.PointerPose : hand.transform) : null;
            UpdateHold(slot, pivot, ok && hand.GetFingerIsPinching(OVRHand.HandFinger.Index));
        }

        /// <summary>
        /// One hold slot. On the press edge it picks up the nearest free sphere within reach, carries it while pressed
        /// (keeping the grab offset) and lets go on release; the sphere stays put.
        /// </summary>
        private void UpdateHold(int slot, Transform pivot, bool pressed)
        {
            var edge = pressed && !prevPressed[slot];
            prevPressed[slot] = pressed;

            var held = holding[slot];
            if (held != null && held.Transform == null) held = holding[slot] = null; // destroyed while held
            if (held != null && (!pressed || pivot == null))
            {
                holding[slot] = null;
                held.IsHeld = false;
                return;
            }

            if (held != null)
            {
                held.Transform.position = pivot.TransformPoint(holdOffset[slot]);
                return;
            }

            if (!edge || pivot == null) return;

            Sphere nearest = null;
            var best = GrabReach * GrabReach;
            foreach (var sphere in spheres.Values)
            {
                if (sphere.Transform == null || sphere.IsHeld) continue;
                var d = (sphere.Transform.position - pivot.position).sqrMagnitude;
                if (d > best) continue;
                best = d;
                nearest = sphere;
            }

            if (nearest == null) return;
            holding[slot] = nearest;
            holdOffset[slot] = pivot.InverseTransformPoint(nearest.Transform.position);
            nearest.IsHeld = true;
        }

        /// <summary>Fallback spawn point when the controller pose is unavailable, so the button still works.</summary>
        private static Vector3 InFrontOfHead()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 0.5f : new Vector3(0f, 1.2f, 0.5f);
        }

        /// <summary>Spawns on the start of a middle finger pinch. Returns a short status text.</summary>
        private string SpawnFromHand(int index)
        {
            var hand = hands[index];
            if (hand == null || !hand.IsTracked || hand.HandConfidence != OVRHand.TrackingConfidence.High)
            {
                prevHandSpawn[index] = false;
                return hand == null ? "no OVRHand" : "not tracked";
            }

            var pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
            if (pinching && !prevHandSpawn[index])
            {
                var position = hand.IsPointerPoseValid && hand.PointerPose != null
                    ? hand.PointerPose.position
                    : hand.transform.position;
                Spawn(position);
            }

            prevHandSpawn[index] = pinching;
            return pinching ? "pinch" : "tracked";
        }

        private void FindHands()
        {
            if (hands[0] != null && hands[1] != null) return;
            if (Time.unscaledTime < nextHandFindTime) return;
            nextHandFindTime = Time.unscaledTime + 1f;

            foreach (var candidate in Object.FindObjectsByType<OVRHand>(FindObjectsSortMode.None))
            {
                var side = candidate.GetHand();
                if (side == OVRPlugin.Hand.HandLeft) hands[0] = candidate;
                else if (side == OVRPlugin.Hand.HandRight) hands[1] = candidate;
            }
        }

        /// <summary>Removes destroyed entries and recalls free spheres that are too far away.</summary>
        private void Sweep()
        {
            if (spheres.Count == 0) return;

            var cam = Camera.main;
            var origin = cam != null ? cam.transform.position : Vector3.zero;
            var limit = config.RecallDistance;
            scratch.Clear();
            foreach (var pair in spheres)
            {
                var sphere = pair.Value;
                if (sphere.Transform == null) { scratch.Add(pair.Key); continue; }
                if (sphere.IsHeld) continue;
                if ((sphere.Transform.position - origin).sqrMagnitude > limit * limit) scratch.Add(pair.Key);
            }

            foreach (var id in scratch) Despawn(id);
        }

        private bool TryGet(int id, out Sphere sphere)
        {
            if (spheres.TryGetValue(id, out sphere) && sphere.Transform != null) return true;
            sphere = null;
            return false;
        }

        /// <summary>Frees one slot: purges destroyed entries first, else removes the oldest free sphere.</summary>
        private bool RemoveOldestFree()
        {
            scratch.Clear();
            foreach (var pair in spheres)
            {
                if (pair.Value.Transform == null) scratch.Add(pair.Key);
            }

            if (scratch.Count > 0)
            {
                foreach (var id in scratch) spheres.Remove(id);
                return true;
            }

            Sphere oldest = null;
            foreach (var sphere in spheres.Values)
            {
                if (sphere.IsHeld) continue;
                if (oldest == null || sphere.Order < oldest.Order) oldest = sphere;
            }

            if (oldest == null) return false;
            Despawn(oldest.Id);
            return true;
        }

        private void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("FoodSpheres").transform;
        }

        /// <summary>One food sphere. No physics: it is moved while held and stays where it was let go.</summary>
        private sealed class Sphere
        {
            public readonly int Id;
            public readonly Transform Transform;

            public bool IsHeld;

            /// <summary>Creation order (the id is increasing), smaller is older.</summary>
            public int Order => Id;

            public Sphere(int id, Transform transform)
            {
                Id = id;
                Transform = transform;
            }
        }
    }
}
