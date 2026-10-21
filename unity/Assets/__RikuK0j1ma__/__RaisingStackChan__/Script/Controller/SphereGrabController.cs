using System.Collections.Generic;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Controller
{
    /// <summary>
    /// Turns hand / controller input into spawn, grab, carry and release of spheres.
    /// Input-device independent: it only talks to <see cref="IHandInputSource"/>.
    /// </summary>
    public sealed class SphereGrabController : ITickable
    {
        private const float VelocitySmoothing = 0.5f;

        private readonly ISphereSpawner spawner;
        private readonly ISphereInteraction interaction;
        private readonly SphereConfig config;
        private readonly List<Hand> hands = new List<Hand>();

        public SphereGrabController(
            ISphereSpawner spawner,
            ISphereInteraction interaction,
            SphereConfig config,
            IReadOnlyList<IHandInputSource> sources)
        {
            this.spawner = spawner;
            this.interaction = interaction;
            this.config = config;
            foreach (var source in sources) hands.Add(new Hand(source));
        }

        public void Tick()
        {
            var dt = Time.deltaTime;
            foreach (var hand in hands) Step(hand, dt);
        }

        private void Step(Hand hand, float dt)
        {
            if (!hand.Source.TryGetState(out var state))
            {
                // Tracking lost: drop whatever is held.
                Release(hand, Vector3.zero);
                hand.HasPrev = false;
                hand.PrevSpawn = false;
                return;
            }

            if (hand.HasPrev && dt > 0f)
            {
                var v = (state.Position - hand.PrevPosition) / dt;
                hand.Velocity = Vector3.Lerp(hand.Velocity, v, VelocitySmoothing);
            }
            hand.PrevPosition = state.Position;
            hand.HasPrev = true;

            if (hand.HeldId >= 0 && !spawner.IsHeld(hand.HeldId)) hand.HeldId = -1; // eaten or recalled

            if (state.Spawn && !hand.PrevSpawn && hand.HeldId < 0)
            {
                // Spawn at the hand and, if already gripping, pick it up at once.
                var id = spawner.Spawn(state.Position);
                if (id >= 0 && state.Grab && interaction.BeginHold(id)) hand.HeldId = id;
            }
            hand.PrevSpawn = state.Spawn;

            if (state.Grab)
            {
                if (hand.HeldId < 0 && interaction.TryFindGrabbable(state.Position, config.GrabRadius, out var found)
                    && interaction.BeginHold(found))
                {
                    hand.HeldId = found;
                }
                if (hand.HeldId >= 0 && !interaction.MoveHeld(hand.HeldId, state.Position)) hand.HeldId = -1;
            }
            else
            {
                Release(hand, hand.Velocity * config.ThrowScale);
            }
        }

        private void Release(Hand hand, Vector3 velocity)
        {
            if (hand.HeldId < 0) return;
            interaction.EndHold(hand.HeldId, velocity);
            hand.HeldId = -1;
        }

        private sealed class Hand
        {
            public readonly IHandInputSource Source;
            public int HeldId = -1;
            public bool PrevSpawn;
            public bool HasPrev;
            public Vector3 PrevPosition;
            public Vector3 Velocity;

            public Hand(IHandInputSource source)
            {
                Source = source;
            }
        }
    }
}
