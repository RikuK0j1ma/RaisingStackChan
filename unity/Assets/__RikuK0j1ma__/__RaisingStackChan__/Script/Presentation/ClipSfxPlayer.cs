using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>
    /// Plays the hungry and eat sounds on the headset. Clips are loaded from Resources/Sfx/Hungry and
    /// Resources/Sfx/Eat; one is picked at random (never the same twice in a row). Each sound comes from
    /// StackChan's mouth when its position is known, and from the origin otherwise.
    /// </summary>
    public sealed class ClipSfxPlayer : IHungrySound, IEatSound
    {
        private const string HungryPath = "Sfx/Hungry";
        private const string EatPath = "Sfx/Eat";

        private readonly IStackChanPoseProvider pose;
        private readonly AudioClip[] hungryClips;
        private readonly AudioClip[] eatClips;
        private AudioSource source;
        private AudioClip lastHungry;
        private AudioClip lastEat;

        public ClipSfxPlayer(IStackChanPoseProvider pose)
        {
            this.pose = pose;
            hungryClips = Resources.LoadAll<AudioClip>(HungryPath);
            eatClips = Resources.LoadAll<AudioClip>(EatPath);
            if (hungryClips.Length == 0) Debug.LogWarning($"[Sfx] no clips in Resources/{HungryPath}");
            if (eatClips.Length == 0) Debug.LogWarning($"[Sfx] no clips in Resources/{EatPath}");
        }

        public void PlayHungry() => Play(hungryClips, ref lastHungry);

        public void PlayEat() => Play(eatClips, ref lastEat);

        private void Play(AudioClip[] clips, ref AudioClip last)
        {
            if (clips.Length == 0) return;
            EnsureSource();

            var clip = clips[Random.Range(0, clips.Length)];
            if (clips.Length > 1)
            {
                while (clip == last) clip = clips[Random.Range(0, clips.Length)];
            }
            last = clip;

            if (pose.TryGetMouthPosition(out var mouth)) source.transform.position = mouth;
            source.PlayOneShot(clip);
        }

        private void EnsureSource()
        {
            if (source != null) return;
            var go = new GameObject("SfxPlayer");
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3D: heard from the mouth
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = 6f;
        }
    }
}
