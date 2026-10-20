using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Face anchor persisted as an OVRSpatialAnchor (local storage). The UUID is kept in IFaceAnchorIdStore.
    /// Restore fails (-> QR flow) when the UUID is unknown, the anchor no longer exists (room changed,
    /// Guardian reset) or it cannot be localized within the timeout.
    /// </summary>
    public sealed class SpatialAnchorFaceAnchorStore : IFaceAnchorStore
    {
        private const double LocalizeTimeoutSeconds = 5.0;

        private readonly IFaceAnchorIdStore idStore;
        private OVRSpatialAnchor anchor;

        public SpatialAnchorFaceAnchorStore(IFaceAnchorIdStore idStore)
        {
            this.idStore = idStore;
        }

        public async UniTask<bool> SaveAsync(Pose facePose, CancellationToken ct)
        {
            DestroyLive();

            var go = new GameObject("FaceSpatialAnchor");
            go.transform.SetPositionAndRotation(facePose.position, facePose.rotation);
            var created = go.AddComponent<OVRSpatialAnchor>();
            anchor = created;

            while (created != null && !created.Created)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (created == null) return false;

            var saveResult = await created.SaveAnchorAsync();
            if (!saveResult.Success)
            {
                Debug.LogWarning($"[SpatialAnchor] save failed: {saveResult.Status}");
                DestroyLive();
                return false;
            }

            idStore.Save(created.Uuid.ToString());
            return true;
        }

        public async UniTask<FaceAnchorRestoreResult> RestoreAsync(CancellationToken ct)
        {
            if (!idStore.TryLoad(out var text) || !Guid.TryParse(text, out var uuid))
                return FaceAnchorRestoreResult.Failed;

            var unbound = new List<OVRSpatialAnchor.UnboundAnchor>();
            var load = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[] { uuid }, unbound);
            if (!load.Success || unbound.Count == 0)
            {
                // Anchor is gone (room / Guardian changed): forget it so the QR flow starts cleanly.
                idStore.Clear();
                return FaceAnchorRestoreResult.Failed;
            }

            var target = unbound[0];
            if (!await target.LocalizeAsync(LocalizeTimeoutSeconds))
            {
                // Possibly just not localized yet in this room; keep the UUID, fall back to QR for now.
                return FaceAnchorRestoreResult.Failed;
            }
            ct.ThrowIfCancellationRequested();

            DestroyLive();
            var go = new GameObject("FaceSpatialAnchor");
            var bound = go.AddComponent<OVRSpatialAnchor>();
            target.BindTo(bound);
            anchor = bound;

            return new FaceAnchorRestoreResult(true, new Pose(go.transform.position, go.transform.rotation));
        }

        public async UniTask DiscardAsync(CancellationToken ct)
        {
            var live = anchor;
            if (live != null && live.Created)
            {
                var erase = await live.EraseAnchorAsync();
                if (!erase.Success) Debug.LogWarning($"[SpatialAnchor] erase failed: {erase.Status}");
            }
            DestroyLive();
            idStore.Clear();
        }

        public bool TryGetPose(out Pose pose)
        {
            if (anchor != null && anchor.Localized)
            {
                pose = new Pose(anchor.transform.position, anchor.transform.rotation);
                return true;
            }
            pose = Pose.identity;
            return false;
        }

        private void DestroyLive()
        {
            if (anchor != null) UnityEngine.Object.Destroy(anchor.gameObject);
            anchor = null;
        }
    }
}
