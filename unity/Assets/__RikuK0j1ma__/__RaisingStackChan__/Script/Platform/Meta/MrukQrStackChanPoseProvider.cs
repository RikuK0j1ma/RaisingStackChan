using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using UnityEngine.Android;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Detects the QR code shown by StackChan (MOGUMOGU) through MRUK trackables and exposes it as the
    /// face pose. QR center = face center; the mouth is EatConfig.MouthLocalOffset in the face space.
    /// The QR payload (ws://ip:port/ws?token=...) is handed to IStackChanLink.TrySetEndpointFromQr;
    /// QR codes that are not a StackChan endpoint are ignored.
    /// Needs an MRUK component and OVRCameraRig in the scene and the Scene permission.
    /// The pose is kept after the QR disappears (StackChan hides it once connected).
    /// </summary>
    public sealed class MrukQrStackChanPoseProvider : IStackChanPoseProvider, IDiagnosticsSource, IInitializable, ITickable, IDisposable
    {
        private readonly IStackChanLink link;
        private readonly EatConfig eatConfig;

        private MRUK mruk;
        private bool disposed;
        private int trackableCount;
        private int qrCount;
        private string lastPayload;
        private string lastResult = "no QR seen yet";
        private MRUKTrackable current;
        private bool confirmed;
        private bool hasPose;
        private Pose facePose;

        public MrukQrStackChanPoseProvider(IStackChanLink link, EatConfig eatConfig)
        {
            this.link = link;
            this.eatConfig = eatConfig;
        }

        /// <summary>
        /// Raised on the main thread when a StackChan QR is first seen as tracked (again), with the face
        /// pose. Subscribe from the anchor feature (#10) to persist it.
        /// </summary>
        public event Action<Pose> FacePoseConfirmed;

        /// <summary>True when MRUK exists in the scene and QR tracking was requested.</summary>
        public bool IsActive => mruk != null;

        public void Initialize() => TrySetUp();

        // VContainer runs Initialize inside LifetimeScope.Awake, which can be before MRUK.Awake sets
        // MRUK.Instance (script order is undefined). So retry from Tick until MRUK is there.
        private void TrySetUp()
        {
            if (disposed || mruk != null) return;
            var instance = MRUK.Instance;
            if (instance == null) return;
            mruk = instance;

            var settings = mruk.SceneSettings;
            var config = settings.TrackerConfiguration;
            config.QRCodeTrackingEnabled = true;
            settings.TrackerConfiguration = config;
            settings.TrackableAdded.AddListener(OnTrackableAdded);
            settings.TrackableRemoved.AddListener(OnTrackableRemoved);

            // Trackables found before this ran (MRUK may start first).
            var existing = new List<MRUKTrackable>();
            mruk.GetTrackables(existing);
            foreach (var t in existing) OnTrackableAdded(t);
            Debug.Log($"[QR] Requested QR tracking. Existing trackables: {existing.Count}");
        }

        public void Dispose()
        {
            disposed = true;
            if (mruk == null) return;
            var settings = mruk.SceneSettings;
            settings.TrackableAdded.RemoveListener(OnTrackableAdded);
            settings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
            mruk = null;
        }

        public void Tick()
        {
            if (mruk == null) TrySetUp();
            if (current == null || !current.IsTracked) return;

            var t = current.transform;
            facePose = new Pose(t.position, t.rotation);
            hasPose = true;
            if (confirmed) return;
            confirmed = true;
            FacePoseConfirmed?.Invoke(facePose);
        }

        /// <summary>Uses a pose restored from a saved anchor, without raising <see cref="FacePoseConfirmed"/>.</summary>
        public void ApplyRestoredPose(Pose pose)
        {
            facePose = pose;
            hasPose = true;
            confirmed = true;
        }

        /// <summary>Recalibration: the next tracked QR raises <see cref="FacePoseConfirmed"/> again.</summary>
        public void ResetConfirmation() => confirmed = false;

        public bool TryGetFacePose(out Pose pose)
        {
            pose = facePose;
            return hasPose;
        }

        public bool TryGetMouthPosition(out Vector3 position)
        {
            position = hasPose ? facePose.position + facePose.rotation * eatConfig.MouthLocalOffset : Vector3.zero;
            return hasPose;
        }

        public string DescribeStatus()
        {
            if (mruk == null) return "QR: waiting for MRUK (MRUK.Instance is null)";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Scene permission: {(Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission) ? "granted" : "NOT granted")}");
            sb.AppendLine($"QR tracking supported: {mruk.QRCodeTrackingSupported}");
            sb.AppendLine($"QR tracking: requested={mruk.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled} granted={mruk.TrackerConfiguration.QRCodeTrackingEnabled}");
            sb.AppendLine($"Trackables seen: {trackableCount} (QR: {qrCount})");
            sb.AppendLine($"Last QR: {(lastPayload ?? "-")}");
            sb.AppendLine($"Last QR result: {lastResult}");
            sb.AppendLine($"Tracking now: {(current != null && current.IsTracked ? "yes" : "no")}");
            return sb.ToString();
        }

        private void OnTrackableAdded(MRUKTrackable trackable)
        {
            if (trackable == null) return;
            trackableCount++;
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;
            qrCount++;
            var payload = trackable.MarkerPayloadString;
            lastPayload = payload;
            if (!link.TrySetEndpointFromQr(payload))
            {
                lastResult = "ignored (not a StackChan endpoint)";
                Debug.Log($"[QR] Ignored a QR that is not a StackChan endpoint: \"{payload}\"");
                return;
            }

            lastResult = "accepted";
            Debug.Log($"[QR] StackChan QR accepted: \"{payload}\"");
            current = trackable;
            confirmed = false;
        }

        private void OnTrackableRemoved(MRUKTrackable trackable)
        {
            if (trackable == current) current = null;
        }
    }
}
