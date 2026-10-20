using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Presentation;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Creates the setup guide UI, drives SetupGuideUseCase, and routes the menu's recalibrate request.</summary>
    public sealed class SetupGuideEntryPoint : IStartable, ITickable, IDisposable
    {
        private readonly SetupGuideUseCase guide;
        private readonly RecalibrationService recalibration;
        private readonly IStackChanPoseProvider pose;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private SetupGuideView view;

        public SetupGuideEntryPoint(SetupGuideUseCase guide, RecalibrationService recalibration, IStackChanPoseProvider pose)
        {
            this.guide = guide;
            this.recalibration = recalibration;
            this.pose = pose;
        }

        public void Start()
        {
            var go = new GameObject("SetupGuide");
            view = go.AddComponent<SetupGuideView>();
            var cam = Camera.main;
            view.Initialize(guide, pose, cam != null ? cam.transform : null);
            view.RecalibrateRequested += OnRecalibrate;
        }

        public void Tick() => guide.Tick(Time.deltaTime);

        private void OnRecalibrate() => recalibration.RequestAsync(cts.Token).Forget();

        public void Dispose()
        {
            if (view != null) view.RecalibrateRequested -= OnRecalibrate;
            cts.Cancel();
            cts.Dispose();
        }
    }
}
