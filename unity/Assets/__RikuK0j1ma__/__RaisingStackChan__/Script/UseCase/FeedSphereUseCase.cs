using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Decides when a sphere is eaten: it must stay within EatRadius of the mouth for HoldSeconds
    /// while not grabbed. On eating, the sphere is despawned, the effect and sound play, and Happy is
    /// sent to StackChan (only if connected right now; nothing is queued for a later reconnect).
    /// Eating again while happy sends Happy again, which resets the duration on StackChan (extension).
    /// </summary>
    public sealed class FeedSphereUseCase : IDisposable
    {
        private readonly ISphereSpawner spawner;
        private readonly IStackChanPoseProvider pose;
        private readonly IStackChanLink link;
        private readonly IEatEffect effect;
        private readonly IEatSound sound;
        private readonly EatConfig config;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        // sphere id -> seconds spent inside the radius
        private readonly Dictionary<int, float> holds = new Dictionary<int, float>();
        private readonly List<int> scratch = new List<int>();

        public FeedSphereUseCase(
            ISphereSpawner spawner,
            IStackChanPoseProvider pose,
            IStackChanLink link,
            IEatEffect effect,
            IEatSound sound,
            EatConfig config)
        {
            this.spawner = spawner;
            this.pose = pose;
            this.link = link;
            this.effect = effect;
            this.sound = sound;
            this.config = config;

            // Every sphere the spawner creates is judged automatically; callers need not call Track.
            spawner.Spawned += Track;
        }

        /// <summary>Number of spheres being tracked.</summary>
        public int TrackedCount => holds.Count;

        /// <summary>Starts judging a sphere. Called automatically via ISphereSpawner.Spawned; idempotent.</summary>
        public void Track(int sphereId)
        {
            if (sphereId < 0) return;
            if (!holds.ContainsKey(sphereId)) holds.Add(sphereId, 0f);
        }

        public void Untrack(int sphereId) => holds.Remove(sphereId);

        /// <summary>Call every frame.</summary>
        public void Tick(float deltaTime)
        {
            if (holds.Count == 0) return;

            scratch.Clear();
            scratch.AddRange(holds.Keys);

            if (!pose.TryGetMouthPosition(out var mouth))
            {
                // Mouth position unknown (not calibrated): nothing can be eaten, restart all holds.
                foreach (var id in scratch) holds[id] = 0f;
                return;
            }

            foreach (var id in scratch)
            {
                if (!spawner.TryGetPosition(id, out var position))
                {
                    holds.Remove(id); // sphere is gone
                    continue;
                }

                if (spawner.IsHeld(id) || Vector3.Distance(position, mouth) > config.EatRadius)
                {
                    holds[id] = 0f;
                    continue;
                }

                var held = holds[id] + deltaTime;
                if (held >= config.HoldSeconds)
                {
                    holds.Remove(id);
                    Eat(id, position, mouth);
                }
                else
                {
                    holds[id] = held;
                }
            }
        }

        private void Eat(int id, Vector3 position, Vector3 mouth)
        {
            spawner.Despawn(id);
            sound.PlayEat();
            PlayEffectAsync(position, mouth).Forget();

            // Unconnected: effect only. Do not send after a later reconnect.
            if (link.State == StackChanConnectionState.Connected)
            {
                SendHappyAsync().Forget();
            }
        }

        private async UniTaskVoid PlayEffectAsync(Vector3 from, Vector3 mouth)
        {
            try { await effect.PlayAsync(from, mouth, cts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        private async UniTaskVoid SendHappyAsync()
        {
            try { await link.SendEmotionAsync(StackChanEmotion.Happy, config.HappyDurationMs, cts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[Feed] SendEmotion failed: {e.Message}"); }
        }

        public void Dispose()
        {
            spawner.Spawned -= Track;
            cts.Cancel();
            cts.Dispose();
        }
    }
}
