using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Decides when the food is bitten: its edible part (the meat) must stay within EatRadius of the mouth for
    /// HoldSeconds. The bone is usually still held by the player; that does not matter. Each bite takes a chunk
    /// (ISphereSpawner.Bite), plays the effect and sound, and sends Satisfied to StackChan (only if connected
    /// right now; nothing is queued for a later reconnect). The next bite needs another HoldSeconds.
    /// When the spawner says the last bite was taken, the food is not judged any more.
    /// Biting again while satisfied sends Satisfied again, which resets the duration on StackChan (extension).
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
                if (!spawner.TryGetEatDistance(id, mouth, out var distance))
                {
                    holds.Remove(id); // food is gone
                    continue;
                }

                if (distance > config.EatRadius)
                {
                    holds[id] = 0f;
                    continue;
                }

                var held = holds[id] + deltaTime;
                if (held >= config.HoldSeconds)
                {
                    holds[id] = 0f;
                    Eat(id, mouth);
                }
                else
                {
                    holds[id] = held;
                }
            }
        }

        private void Eat(int id, Vector3 mouth)
        {
            spawner.TryGetPosition(id, out var position);
            var finished = spawner.Bite(id, mouth);
            if (finished) holds.Remove(id);
            sound.PlayEat();
            PlayEffectAsync(position, mouth).Forget();

            // Unconnected: effect only. Do not send after a later reconnect.
            if (link.State == StackChanConnectionState.Connected)
            {
                SendSatisfiedAsync().Forget();
            }
        }

        private async UniTaskVoid PlayEffectAsync(Vector3 from, Vector3 mouth)
        {
            try { await effect.PlayAsync(from, mouth, cts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        private async UniTaskVoid SendSatisfiedAsync()
        {
            try { await link.SendEmotionAsync(StackChanEmotion.Satisfied, config.HappyDurationMs, cts.Token); }
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
