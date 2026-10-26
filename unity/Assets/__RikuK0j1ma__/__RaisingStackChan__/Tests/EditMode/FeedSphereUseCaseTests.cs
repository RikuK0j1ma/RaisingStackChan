using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class FeedSphereUseCaseTests
    {
        private sealed class FakeSpawner : ISphereSpawner
        {
            public readonly Dictionary<int, Vector3> Spheres = new Dictionary<int, Vector3>();
            public readonly HashSet<int> Held = new HashSet<int>();
            private int next;

            public event Action<int> Spawned;

            public int Spawn(Vector3 position)
            {
                var id = next++;
                Spheres[id] = position;
                Spawned?.Invoke(id);
                return id;
            }
            public bool TryGetPosition(int id, out Vector3 position) => Spheres.TryGetValue(id, out position);
            public void Despawn(int id) => Spheres.Remove(id);
            public bool IsHeld(int id) => Held.Contains(id);
        }

        private sealed class FakePose : IStackChanPoseProvider
        {
            public bool HasMouth = true;
            public Vector3 Mouth = Vector3.zero;
            public bool TryGetFacePose(out Pose pose) { pose = default; return false; }
            public bool TryGetMouthPosition(out Vector3 position) { position = Mouth; return HasMouth; }
        }

        private sealed class FakeLink : IStackChanLink
        {
            public StackChanConnectionState State { get; set; } = StackChanConnectionState.Connected;
            public bool IsConnected => State == StackChanConnectionState.Connected;
            public event Action<StackChanConnectionState> StateChanged { add { } remove { } }
            public string LastError => null;
            public readonly List<(StackChanEmotion emotion, int ms)> Sent = new List<(StackChanEmotion, int)>();

            public bool TrySetEndpointFromQr(string payload) => false;
            public UniTask ConnectAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendShowQrAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendHideQrAsync(CancellationToken ct) => UniTask.CompletedTask;

            public UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct)
            {
                Sent.Add((emotion, durationMs));
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeEffect : IEatEffect
        {
            public int Plays;
            public UniTask PlayAsync(Vector3 from, Vector3 mouth, CancellationToken ct) { Plays++; return UniTask.CompletedTask; }
        }

        private sealed class FakeSound : IEatSound
        {
            public int Plays;
            public void PlayEat() => Plays++;
        }

        private FakeSpawner spawner;
        private FakePose pose;
        private FakeLink link;
        private FakeEffect effect;
        private FakeSound sound;
        private EatConfig config;
        private FeedSphereUseCase sut;

        [SetUp]
        public void SetUp()
        {
            spawner = new FakeSpawner();
            pose = new FakePose();
            link = new FakeLink();
            effect = new FakeEffect();
            sound = new FakeSound();
            config = ScriptableObject.CreateInstance<EatConfig>();
            SetField("eatRadius", 0.1f);
            SetField("holdSeconds", 0.5f);
            SetField("happyDurationMs", 3000);
            sut = new FeedSphereUseCase(spawner, pose, link, effect, sound, config);
        }

        [TearDown]
        public void TearDown()
        {
            sut.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        private void SetField(string name, object value)
        {
            typeof(EatConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        }

        private int SpawnTracked(Vector3 position)
        {
            var id = spawner.Spawn(position);
            sut.Track(id);
            return id;
        }

        [Test]
        public void InsideRadius_ForHoldSeconds_Eats()
        {
            var id = SpawnTracked(new Vector3(0.05f, 0f, 0f));

            sut.Tick(0.3f);
            Assert.IsTrue(spawner.Spheres.ContainsKey(id));
            sut.Tick(0.3f);

            Assert.IsFalse(spawner.Spheres.ContainsKey(id));
            Assert.AreEqual(1, effect.Plays);
            Assert.AreEqual(1, sound.Plays);
            Assert.AreEqual(1, link.Sent.Count);
            Assert.AreEqual(StackChanEmotion.Happy, link.Sent[0].emotion);
            Assert.AreEqual(3000, link.Sent[0].ms);
            Assert.AreEqual(0, sut.TrackedCount);
        }

        [Test]
        public void OutsideRadius_DoesNotEat()
        {
            var id = SpawnTracked(new Vector3(0.5f, 0f, 0f));
            sut.Tick(10f);
            Assert.IsTrue(spawner.Spheres.ContainsKey(id));
            Assert.AreEqual(0, link.Sent.Count);
        }

        [Test]
        public void LeavingRadius_ResetsHold()
        {
            var id = SpawnTracked(new Vector3(0.05f, 0f, 0f));
            sut.Tick(0.4f);
            spawner.Spheres[id] = new Vector3(0.5f, 0f, 0f);
            sut.Tick(0.1f);
            spawner.Spheres[id] = new Vector3(0.05f, 0f, 0f);
            sut.Tick(0.4f);

            Assert.IsTrue(spawner.Spheres.ContainsKey(id));
            sut.Tick(0.2f);
            Assert.IsFalse(spawner.Spheres.ContainsKey(id));
        }

        [Test]
        public void HeldSphere_IsNotEaten_AndResetsHold()
        {
            var id = SpawnTracked(new Vector3(0.05f, 0f, 0f));
            spawner.Held.Add(id);
            sut.Tick(5f);
            Assert.IsTrue(spawner.Spheres.ContainsKey(id));

            spawner.Held.Remove(id);
            sut.Tick(0.4f);
            Assert.IsTrue(spawner.Spheres.ContainsKey(id));
            sut.Tick(0.2f);
            Assert.IsFalse(spawner.Spheres.ContainsKey(id));
        }

        [Test]
        public void NotConnected_PlaysEffectButSendsNothing_EvenAfterReconnect()
        {
            link.State = StackChanConnectionState.Reconnecting;
            var id = SpawnTracked(Vector3.zero);
            sut.Tick(1f);

            Assert.IsFalse(spawner.Spheres.ContainsKey(id));
            Assert.AreEqual(1, effect.Plays);
            Assert.AreEqual(0, link.Sent.Count);

            link.State = StackChanConnectionState.Connected;
            sut.Tick(1f);
            Assert.AreEqual(0, link.Sent.Count);
        }

        [Test]
        public void ConsecutiveFeeding_SendsHappyEachTime()
        {
            SpawnTracked(Vector3.zero);
            sut.Tick(1f);
            SpawnTracked(Vector3.zero);
            sut.Tick(1f);

            Assert.AreEqual(2, link.Sent.Count);
            Assert.AreEqual(3000, link.Sent[1].ms);
        }

        [Test]
        public void MouthUnknown_DoesNotEat()
        {
            pose.HasMouth = false;
            var id = SpawnTracked(Vector3.zero);
            sut.Tick(10f);
            Assert.IsTrue(spawner.Spheres.ContainsKey(id));
        }

        [Test]
        public void VanishedSphere_IsUntracked()
        {
            var id = SpawnTracked(Vector3.zero);
            spawner.Spheres.Remove(id);
            sut.Tick(0.1f);
            Assert.AreEqual(0, sut.TrackedCount);
        }

        [Test]
        public void Spawned_IsTrackedAutomatically_AndEaten()
        {
            var id = spawner.Spawn(Vector3.zero); // no explicit Track
            Assert.AreEqual(1, sut.TrackedCount);

            sut.Tick(1f);
            Assert.IsFalse(spawner.Spheres.ContainsKey(id));
            Assert.AreEqual(1, link.Sent.Count);
        }

        [Test]
        public void Track_IgnoresInvalidId()
        {
            sut.Track(-1);
            Assert.AreEqual(0, sut.TrackedCount);
        }
    }
}
