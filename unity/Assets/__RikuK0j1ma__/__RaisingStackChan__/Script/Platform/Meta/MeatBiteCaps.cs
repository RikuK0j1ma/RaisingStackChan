using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Draws the cut face of a bitten meat. The meat mesh is a hollow shell, so the MangaMeat shader only carves the bites out
    /// of it. This class adds what is needed to see the solid cross section, with the stencil buffer:
    /// 1. <c>MangaMeatBiteDepth</c>: the back faces of an invisible sphere per bite are written to the depth buffer
    ///    (only where the shell was carved away, since elsewhere the shell is nearer).
    /// 2. <c>MangaMeatVolume</c>: the uncarved shell is drawn again; each fragment nearer than that depth flips a stencil bit,
    ///    so the bit is 1 where the sphere surface lies inside the meat.
    /// 3. <c>MangaMeatBiteCap</c>: the sphere back faces are drawn where the bit is 1: the cut face (a concave dent).
    /// The three steps are separate renderers because they have to run in this order (render queue Geometry+1, +2, +3).
    /// The spheres are children of the meat, so they follow it.
    /// </summary>
    public sealed class MeatBiteCaps
    {
        private const string SphereName = "BiteSphere";

        /// <summary>The sphere mesh is a polygon inscribed in the true sphere; this keeps the cap from leaving a gap at the rim.</summary>
        private const float SphereOversize = 1.03f;

        private static readonly int[] BiteIds = BuildIds("_Bite", 8);
        private static readonly int[] WorldToLocalIds = BuildIds("_MeatW2L", 3);
        private static readonly int BiteCountId = Shader.PropertyToID("_BiteCount");
        private static readonly int MetersPerUnitId = Shader.PropertyToID("_MetersPerUnit");

        private static Mesh sphereMesh;

        private readonly Transform meat;
        private readonly Material depthMaterial;
        private readonly Material capMaterial;
        private readonly GameObject volume;
        private readonly List<GameObject> spheres = new List<GameObject>();

        private MeatBiteCaps(Transform meat, GameObject volume, Material depthMaterial, Material capMaterial)
        {
            this.meat = meat;
            this.volume = volume;
            this.depthMaterial = depthMaterial;
            this.capMaterial = capMaterial;
        }

        /// <summary>Null when a material is missing (the meat is then only carved, without a cut face).</summary>
        public static MeatBiteCaps Create(Renderer meatRenderer, Material volumeSource, Material depthSource, Material capSource)
        {
            var filter = meatRenderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || volumeSource == null || depthSource == null || capSource == null)
            {
                return null;
            }

            var volumeObject = new GameObject("MeatVolume");
            volumeObject.transform.SetParent(meatRenderer.transform, false);
            volumeObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var volumeRenderer = volumeObject.AddComponent<MeshRenderer>();
            Configure(volumeRenderer);
            volumeRenderer.sharedMaterial = new Material(volumeSource);

            return new MeatBiteCaps(meatRenderer.transform, volumeObject, new Material(depthSource), new Material(capSource));
        }

        /// <summary>Adds an invisible bite sphere at the center (meat object space) with the radius in meters.</summary>
        public void AddBite(Vector3 localCenter, float radiusMeters, float metersPerUnit)
        {
            var diameterInUnits = 2f * radiusMeters / Mathf.Max(metersPerUnit, 1e-5f) * SphereOversize;
            spheres.Add(CreateSphere("BiteDepth", localCenter, diameterInUnits, depthMaterial));
            spheres.Add(CreateSphere("BiteCap", localCenter, diameterInUnits, capMaterial));
        }

        /// <summary>Copies the bites to the cut face materials.</summary>
        public void ApplyBites(Vector4[] bites, int count, float metersPerUnit)
        {
            foreach (var material in new[] { depthMaterial, capMaterial })
            {
                material.SetFloat(MetersPerUnitId, metersPerUnit);
                material.SetFloat(BiteCountId, count);
                for (var i = 0; i < BiteIds.Length; i++) material.SetVector(BiteIds[i], bites[i]);
            }
        }

        /// <summary>Call every frame: the shaders need the meat's current world to local matrix.</summary>
        public void Tick()
        {
            var matrix = meat.worldToLocalMatrix;
            for (var i = 0; i < WorldToLocalIds.Length; i++)
            {
                var row = matrix.GetRow(i);
                depthMaterial.SetVector(WorldToLocalIds[i], row);
                capMaterial.SetVector(WorldToLocalIds[i], row);
            }
        }

        /// <summary>Removes the spheres and the counting pass (the meat is gone, only the bone is left).</summary>
        public void Clear()
        {
            foreach (var sphere in spheres)
            {
                if (sphere != null) Object.Destroy(sphere);
            }

            spheres.Clear();
            if (volume != null) volume.SetActive(false);
        }

        public void Destroy()
        {
            Clear();
            if (volume != null)
            {
                var renderer = volume.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null) Object.Destroy(renderer.sharedMaterial);
                Object.Destroy(volume);
            }

            if (depthMaterial != null) Object.Destroy(depthMaterial);
            if (capMaterial != null) Object.Destroy(capMaterial);
        }

        private GameObject CreateSphere(string name, Vector3 localCenter, float diameterInUnits, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(meat, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = Vector3.one * diameterInUnits;
            go.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            Configure(renderer);
            renderer.sharedMaterial = material;
            return go;
        }

        private static void Configure(Renderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static Mesh GetSphereMesh()
        {
            if (sphereMesh != null) return sphereMesh;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            // Destroy only works in Play mode; the editor tools (and tests) run outside it.
            if (Application.isPlaying) Object.Destroy(primitive);
            else Object.DestroyImmediate(primitive);
            return sphereMesh;
        }

        private static int[] BuildIds(string prefix, int count)
        {
            var ids = new int[count];
            for (var i = 0; i < count; i++) ids[i] = Shader.PropertyToID(prefix + i);
            return ids;
        }
    }
}
