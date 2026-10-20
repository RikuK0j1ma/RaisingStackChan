using System.Collections.Generic;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>
    /// On-screen debug info (IMGUI): last message sent to the fake link, distance of each
    /// sphere to the mouth and whether it is inside the eat radius. Also draws the radius
    /// as a translucent sphere around the mouth. Space spawns a sphere.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        private DebugStackChanLink link;
        private DebugSphereSpawner spawner;
        private IStackChanPoseProvider pose;
        private EatConfig config;
        private GameObject radiusVisual;

        public void Initialize(DebugStackChanLink link, DebugSphereSpawner spawner,
            IStackChanPoseProvider pose, EatConfig config)
        {
            this.link = link;
            this.spawner = spawner;
            this.pose = pose;
            this.config = config;

            radiusVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            radiusVisual.name = "DebugEatRadius";
            radiusVisual.transform.SetParent(transform, false);
            Destroy(radiusVisual.GetComponent<Collider>());
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                radiusVisual.GetComponent<Renderer>().material = new Material(shader)
                {
                    color = new Color(1f, 0.8f, 0.2f, 0.25f),
                };
            }
        }

        private void Update()
        {
            if (pose == null)
            {
                return;
            }

            if (pose.TryGetMouthPosition(out Vector3 mouth) && config != null)
            {
                radiusVisual.SetActive(true);
                radiusVisual.transform.position = mouth;
                radiusVisual.transform.localScale = Vector3.one * (config.EatRadius * 2f);
            }
            else
            {
                radiusVisual.SetActive(false);
            }

            Keyboard keyboard = Keyboard.current;
            Camera cam = Camera.main;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && cam != null)
            {
                spawner.Spawn(cam.transform.position + cam.transform.forward * 0.6f);
            }
        }

        private void OnGUI()
        {
            if (link == null || pose == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(8, 8, 480, 400), GUI.skin.box);
            GUILayout.Label("DebugTools (RSC_DEVELOPMENT)");
            GUILayout.Label("Space: spawn sphere / Left drag: move sphere or face");
            GUILayout.Label($"Link: connected={link.IsConnected}");
            GUILayout.Label($"Last sent: {link.LastMessage}");

            bool hasMouth = pose.TryGetMouthPosition(out Vector3 mouth);
            if (config != null)
            {
                GUILayout.Label($"Eat radius: {config.EatRadius:F3} m, hold: {config.HoldSeconds:F2} s");
            }
            foreach (KeyValuePair<int, GameObject> kv in spawner.Spheres)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                if (!hasMouth)
                {
                    GUILayout.Label($"Sphere {kv.Key}: no mouth pose");
                    continue;
                }
                float d = Vector3.Distance(kv.Value.transform.position, mouth);
                bool inside = config != null && d <= config.EatRadius;
                GUILayout.Label($"Sphere {kv.Key}: {d:F3} m {(inside ? "INSIDE" : "outside")}");
            }
            GUILayout.EndArea();
        }
    }
}
