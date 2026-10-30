using System;
using System.Collections.Generic;
using System.Text;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// The food (manga meat) on Meta Quest: spawning input and the meat itself. A food is an instance of
    /// SphereConfig.Prefab (the MangaMeat model) with a Meat and a Bone mesh. No physics, no Interaction SDK component.
    /// - Spawn: A / X button (controller) or middle finger pinch (hand tracking), at the hand position.
    ///   The bone end is placed at the hand, so it can be grabbed right away.
    /// - Grab: grip button (controller) or index finger pinch (hand). Only the bone can be grabbed; the meat follows.
    ///   The food keeps its pose relative to the hand, and stays where it is let go.
    /// - Eat: StackChan eats the meat, not the bone. Each <see cref="Bite"/> carves a hole out of the meat
    ///   (see the MangaMeat shader); the last bite takes the rest and only the bone is left (it stays and can still be grabbed).
    /// Foods too far from the headset are recalled.
    /// </summary>
    public sealed class MetaSphereSpawner : ISphereSpawner, ITickable, IDiagnosticsSource
    {
        private static readonly int[] BiteIds = BuildBiteIds();
        private static readonly int BiteCountId = Shader.PropertyToID("_BiteCount");
        private static readonly int MetersPerUnitId = Shader.PropertyToID("_MetersPerUnit");
        private static readonly int MeatCenterId = Shader.PropertyToID("_MeatCenter");
        private static readonly int MeatExtentsId = Shader.PropertyToID("_MeatExtents");

        private readonly SphereConfig config;
        private readonly Dictionary<int, Food> foods = new Dictionary<int, Food>();
        private readonly List<int> scratch = new List<int>();
        private readonly StringBuilder sb = new StringBuilder();

        private Transform root;
        private int nextId;

        private OVRCameraRig rig;
        private readonly OVRHand[] hands = new OVRHand[2]; // 0 = left, 1 = right
        private readonly OVRSkeleton[] handSkeletons = new OVRSkeleton[2];
        private readonly Transform[] pinchPivots = new Transform[2];
        private readonly bool[] prevHandSpawn = new bool[2];
        private float nextHandFindTime;

        private const float SpawnForward = 0.1f;

        /// <summary>The bone end is placed this fraction of the half length from the bone center, at the hand.</summary>
        private const float SpawnGripFraction = 0.85f;

        // Hold slots: 0 = left controller, 1 = right controller, 2 = left hand, 3 = right hand.
        private readonly Food[] holding = new Food[4];
        private readonly Vector3[] holdOffsetPosition = new Vector3[4];
        private readonly Quaternion[] holdOffsetRotation = new Quaternion[4];
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
            while (foods.Count >= config.MaxCount && RemoveOldestFree()) { }
            if (foods.Count >= config.MaxCount) return -1;

            if (config.Prefab == null)
            {
                Debug.LogError("[Food] SphereConfig has no Prefab assigned.");
                return -1;
            }

            // The model keeps its own root rotation (the FBX is Z-up, so it is turned 270 degrees about X).
            // Everything below moves the holder, never the model root.
            var go = new GameObject("MangaMeat");
            go.transform.SetParent(root, false);
            Object.Instantiate(config.Prefab, go.transform);

            if (!TryFindParts(go, out var meat, out var bone))
            {
                Debug.LogError("[Food] The model needs two mesh children named Meat and Bone.");
                Object.Destroy(go);
                return -1;
            }

            // Scale so the bone is BoneLength long, whatever scale the model was imported with.
            var size = bone.bounds.size;
            var axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var scale = config.BoneLength / Mathf.Max(size[axis], 1e-4f);
            go.transform.localScale = Vector3.one * scale;

            // Lay the bone along the headset's right, so the meat hangs on it in front of the player.
            var axisVector = Vector3.zero;
            axisVector[axis] = 1f;
            var yaw = Quaternion.Euler(0f, HeadYaw(), 0f);
            go.transform.rotation = yaw * Quaternion.FromToRotation(axisVector, Vector3.right);

            // Put the bone end at the requested position (the hand).
            var gripEnd = bone.bounds.center + yaw * Vector3.right * (config.BoneLength * 0.5f * SpawnGripFraction);
            go.transform.position += position - gripEnd;

            var food = new Food(nextId++, go.transform, meat, bone);
            PrepareMaterials(food);
            foods[food.Id] = food;
            Spawned?.Invoke(food.Id);
            return food.Id;
        }

        public bool TryGetPosition(int id, out Vector3 position)
        {
            if (TryGet(id, out var food))
            {
                position = food.Meat.bounds.center;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        public bool TryGetEatDistance(int id, Vector3 point, out float distance)
        {
            if (TryGet(id, out var food))
            {
                distance = DistanceToRenderer(food.Meat, point, out _);
                return true;
            }

            distance = float.MaxValue;
            return false;
        }

        public void GetActiveIds(List<int> results)
        {
            results.Clear();
            foreach (var pair in foods)
            {
                if (pair.Value.Root != null && !pair.Value.Finished) results.Add(pair.Key);
            }
        }

        public void Despawn(int id)
        {
            if (!foods.TryGetValue(id, out var food)) return;
            foods.Remove(id);
            food.Destroy();
        }

        public bool Bite(int id, Vector3 mouth)
        {
            if (!TryGet(id, out var food)) return false;

            var meatTransform = food.Meat.transform;
            var metersPerUnit = Mathf.Max(meatTransform.lossyScale.x, 1e-5f);
            var localBounds = food.Meat.localBounds;
            var last = food.BiteCount + 1 >= config.BiteCount;

            Vector3 localCenter;
            float radius;
            if (last)
            {
                // The last bite takes everything that is left.
                localCenter = localBounds.center;
                radius = localBounds.extents.magnitude * metersPerUnit * 1.1f;
            }
            else
            {
                // Bite at the surface point nearest to the mouth, a little inside, with some randomness.
                DistanceToRenderer(food.Meat, mouth, out var closest);
                var inside = Vector3.MoveTowards(closest, food.Meat.bounds.center, config.BiteRadius * 0.4f);
                inside += Random.insideUnitSphere * (config.BiteRadius * 0.3f);
                localCenter = meatTransform.InverseTransformPoint(inside);
                radius = config.BiteRadius * Random.Range(0.85f, 1.15f);
            }

            food.Bites[food.BiteCount++] = new Vector4(localCenter.x, localCenter.y, localCenter.z, radius);
            food.ApplyBites(metersPerUnit);

            if (!last)
            {
                food.Caps?.AddBite(localCenter, radius, metersPerUnit);
                return false;
            }

            food.Caps?.Clear(); // nothing is left to cut
            food.Finished = true; // only the bone is left; it stays and can still be grabbed
            return true;
        }

        public bool IsHeld(int id) => TryGet(id, out var food) && food.IsHeld;

        public void Tick()
        {
            HandleSpawnInput();
            Sweep();
            foreach (var pair in foods) pair.Value.Caps?.Tick();
        }

        public string DescribeStatus()
        {
            var held = 0;
            foreach (var food in foods.Values)
            {
                if (food.Root != null && food.IsHeld) held++;
            }

            sb.Length = 0;
            sb.AppendLine($"Food: {foods.Count} (held {held})");
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
            var ok = hand != null && hand.IsTracked;
            var pivot = ok ? GetPinchPivot(handIndex) : null;
            UpdateHold(slot, pivot, ok && pivot != null && hand.GetFingerIsPinching(OVRHand.HandFinger.Index));
        }

        /// <summary>
        /// The pinch point of a hand: the middle of the thumb tip and the index tip, turned with the hand.
        /// The pointer pose is a ray origin away from the fingers, so it is only the fallback (no skeleton yet).
        /// The same point places a spawned food and grabs it, so both agree.
        /// </summary>
        private Transform GetPinchPivot(int handIndex)
        {
            var hand = hands[handIndex];
            if (hand == null) return null;

            if (handSkeletons[handIndex] == null) handSkeletons[handIndex] = hand.GetComponent<OVRSkeleton>();
            var skeleton = handSkeletons[handIndex];
            if (skeleton != null && skeleton.IsInitialized && skeleton.IsDataValid && skeleton.Bones != null)
            {
                Transform thumb = null;
                Transform index = null;
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip || bone.Id == OVRSkeleton.BoneId.XRHand_ThumbTip) thumb = bone.Transform;
                    else if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip || bone.Id == OVRSkeleton.BoneId.XRHand_IndexTip) index = bone.Transform;
                }

                if (thumb != null && index != null)
                {
                    if (pinchPivots[handIndex] == null)
                    {
                        EnsureRoot();
                        pinchPivots[handIndex] = new GameObject("HandPinchPivot" + handIndex).transform;
                        pinchPivots[handIndex].SetParent(root, false);
                    }

                    pinchPivots[handIndex].SetPositionAndRotation((thumb.position + index.position) * 0.5f, hand.transform.rotation);
                    return pinchPivots[handIndex];
                }
            }

            return hand.IsPointerPoseValid && hand.PointerPose != null ? hand.PointerPose : hand.transform;
        }

        /// <summary>
        /// One hold slot. On the press edge it picks up the free food whose bone is nearest and within reach, carries it
        /// while pressed (keeping the grab offset, rotation included) and lets go on release; the food stays put.
        /// </summary>
        private void UpdateHold(int slot, Transform pivot, bool pressed)
        {
            var edge = pressed && !prevPressed[slot];
            prevPressed[slot] = pressed;

            var held = holding[slot];
            if (held != null && held.Root == null) held = holding[slot] = null; // destroyed while held
            if (held != null && (!pressed || pivot == null))
            {
                holding[slot] = null;
                held.IsHeld = false;
                return;
            }

            if (held != null)
            {
                held.Root.SetPositionAndRotation(
                    pivot.TransformPoint(holdOffsetPosition[slot]),
                    pivot.rotation * holdOffsetRotation[slot]);
                return;
            }

            if (!edge || pivot == null) return;

            Food nearest = null;
            var best = config.GrabReach;
            foreach (var food in foods.Values)
            {
                if (food.Root == null || food.IsHeld) continue;
                var d = DistanceToRenderer(food.Bone, pivot.position, out _); // only the bone can be grabbed
                if (d > best) continue;
                best = d;
                nearest = food;
            }

            if (nearest == null) return;
            holding[slot] = nearest;
            holdOffsetPosition[slot] = pivot.InverseTransformPoint(nearest.Root.position);
            holdOffsetRotation[slot] = Quaternion.Inverse(pivot.rotation) * nearest.Root.rotation;
            nearest.IsHeld = true;
        }

        /// <summary>Fallback spawn point when the controller pose is unavailable, so the button still works.</summary>
        private static Vector3 InFrontOfHead()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 0.5f : new Vector3(0f, 1.2f, 0.5f);
        }

        /// <summary>Heading of the headset (degrees around Y), 0 when there is no camera.</summary>
        private static float HeadYaw()
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            var forward = cam.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-4f ? 0f : Quaternion.LookRotation(forward).eulerAngles.y;
        }

        /// <summary>Spawns on the start of a middle finger pinch. Returns a short status text.</summary>
        private string SpawnFromHand(int index)
        {
            var hand = hands[index];
            if (hand == null || !hand.IsTracked)
            {
                prevHandSpawn[index] = false;
                return hand == null ? "no OVRHand" : "not tracked";
            }

            var pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
            if (pinching && !prevHandSpawn[index])
            {
                var pivot = GetPinchPivot(index);
                Spawn(pivot != null ? pivot.position : hand.transform.position);
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

        /// <summary>Removes destroyed entries, and recalls free foods (bones left after a meal included) that are too far away.</summary>
        private void Sweep()
        {
            if (foods.Count == 0) return;

            var cam = Camera.main;
            var origin = cam != null ? cam.transform.position : Vector3.zero;
            var limit = config.RecallDistance;
            scratch.Clear();
            foreach (var pair in foods)
            {
                var food = pair.Value;
                if (food.Root == null) { scratch.Add(pair.Key); continue; }
                if (food.IsHeld) continue;
                if ((food.Root.position - origin).sqrMagnitude > limit * limit) scratch.Add(pair.Key);
            }

            foreach (var id in scratch) Despawn(id);
        }

        private bool TryGet(int id, out Food food)
        {
            if (foods.TryGetValue(id, out food) && food.Root != null && !food.Finished) return true;
            food = null;
            return false;
        }

        /// <summary>Frees one slot: purges destroyed entries first, else removes the oldest free food.</summary>
        private bool RemoveOldestFree()
        {
            scratch.Clear();
            foreach (var pair in foods)
            {
                if (pair.Value.Root == null) scratch.Add(pair.Key);
            }

            if (scratch.Count > 0)
            {
                foreach (var id in scratch) foods.Remove(id);
                return true;
            }

            Food oldest = null;
            foreach (var food in foods.Values)
            {
                if (food.IsHeld) continue;
                if (oldest == null || food.Order < oldest.Order) oldest = food;
            }

            if (oldest == null) return false;
            Despawn(oldest.Id);
            return true;
        }

        private void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("Foods").transform;
#if UNITY_EDITOR || RSC_DEVELOPMENT
            root.gameObject.AddComponent<MangaMeatBiteTester>().Initialize(this);
#endif
        }

        private static int[] BuildBiteIds()
        {
            var ids = new int[SphereConfig.MaxBiteCount];
            for (var i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID("_Bite" + i);
            return ids;
        }

        private static bool TryFindParts(GameObject go, out Renderer meat, out Renderer bone)
        {
            meat = null;
            bone = null;
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.IndexOf("meat", StringComparison.OrdinalIgnoreCase) >= 0) meat = renderer;
                else if (renderer.name.IndexOf("bone", StringComparison.OrdinalIgnoreCase) >= 0) bone = renderer;
            }

            return meat != null && bone != null;
        }

        /// <summary>Gives the meat its own material copy (it holds the bites) and the bone the shared bone material.</summary>
        private void PrepareMaterials(Food food)
        {
            if (config.MeatMaterial != null)
            {
                food.MeatMaterial = new Material(config.MeatMaterial);
                SetAllMaterials(food.Meat, food.MeatMaterial);
                food.Caps = MeatBiteCaps.Create(food.Meat, config.VolumeMaterial, config.BiteDepthMaterial, config.BiteCapMaterial);
                food.ApplyBites(Mathf.Max(food.Meat.transform.lossyScale.x, 1e-5f));
            }

            if (config.BoneMaterial != null) SetAllMaterials(food.Bone, config.BoneMaterial);
        }

        private static void SetAllMaterials(Renderer renderer, Material material)
        {
            var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (var i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Distance (m) from a world point to the renderer's box (its local bounds, so tight when the model is rotated).
        /// 0 when the point is inside. <paramref name="closest"/> is the nearest point of the box in world space.
        /// </summary>
        private static float DistanceToRenderer(Renderer renderer, Vector3 worldPoint, out Vector3 closest)
        {
            var t = renderer.transform;
            var local = t.InverseTransformPoint(worldPoint);
            closest = t.TransformPoint(renderer.localBounds.ClosestPoint(local));
            return Vector3.Distance(worldPoint, closest);
        }

        /// <summary>One food. No physics: it is moved while held and stays where it was let go.</summary>
        private sealed class Food
        {
            public readonly int Id;
            public readonly Transform Root;
            public readonly Renderer Meat;
            public readonly Renderer Bone;
            public readonly Vector4[] Bites = new Vector4[SphereConfig.MaxBiteCount];

            public Material MeatMaterial;
            public MeatBiteCaps Caps;
            public int BiteCount;
            public bool IsHeld;
            public bool Finished;

            /// <summary>Creation order (the id is increasing), smaller is older.</summary>
            public int Order => Id;

            public Food(int id, Transform root, Renderer meat, Renderer bone)
            {
                Id = id;
                Root = root;
                Meat = meat;
                Bone = bone;
            }

            public void ApplyBites(float metersPerUnit)
            {
                if (MeatMaterial == null) return;
                MeatMaterial.SetFloat(MetersPerUnitId, metersPerUnit);
                var bounds = Meat.localBounds;
                MeatMaterial.SetVector(MeatCenterId, bounds.center);
                MeatMaterial.SetVector(MeatExtentsId, bounds.extents);
                for (var i = 0; i < Bites.Length; i++) MeatMaterial.SetVector(BiteIds[i], Bites[i]);
                MeatMaterial.SetFloat(BiteCountId, BiteCount);
                Caps?.ApplyBites(Bites, BiteCount, metersPerUnit);
            }

            public void Destroy()
            {
                Caps?.Destroy();
                if (MeatMaterial != null) Object.Destroy(MeatMaterial);
                if (Root != null) Object.Destroy(Root.gameObject);
            }
        }
    }
}
