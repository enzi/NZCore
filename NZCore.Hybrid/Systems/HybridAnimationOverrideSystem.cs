// <copyright project="NZCore" file="HybridAnimationOverrideSystem.cs" version="1.2.2">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;

namespace NZCore.Hybrid
{
    public partial class HybridAnimationOverrideSystem : SystemBase
    {
        private const float TransitionSpeed = 5;

        protected override void OnCreate()
        {
            CheckedStateRef.CreateSingleton<HybridAnimatorRequestSingleton>();
        }

        protected override void OnDestroy()
        {
            CheckedStateRef.DisposeSingleton<HybridAnimatorRequestSingleton>();
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<HybridAnimatorRequestSingleton>();
            
            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            ref var singleton = ref SystemAPI.GetSingletonRW<HybridAnimatorRequestSingleton>().ValueRW;
            var enumerator = singleton.ClipRequests.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref var request = ref enumerator.Current;

                foreach (var clipRequest in request)
                {
                    ref var animatorOverride = ref SystemAPI.GetComponentRW<AnimatorOverride>(clipRequest.Entity).ValueRW;
                    animatorOverride.SetClip(clipRequest.Clip, clipRequest.Speed);
                }

                request.Clear();
            }
            
            foreach (var (hybridAnimatorRW, animatorOverrideRW)
                     in SystemAPI.Query<RefRW<HybridAnimator>, RefRW<AnimatorOverride>>())
            {
                ref var animatorComp = ref hybridAnimatorRW.ValueRW;

                if (!animatorComp.Graph.IsValid())
                {
                    continue;
                }

                ref var animatorOverride = ref animatorOverrideRW.ValueRW;

                if (animatorComp.TransitionTo != HybridAnimatorTransitionPhase.None)
                {
                    var transitionSpeed = TransitionSpeed * deltaTime;
                    animatorComp.Weight = animatorComp.TransitionTo == HybridAnimatorTransitionPhase.ToCustom ?
                        Mathf.Min(animatorComp.Weight + transitionSpeed, 1.0f) :
                        Mathf.Max(animatorComp.Weight - transitionSpeed, 0.0f);

                    if (Mathf.Approximately(animatorComp.Weight, animatorComp.TransitionTo == HybridAnimatorTransitionPhase.ToCustom ? 1.0f : 0.0f))
                    {
                        animatorComp.TransitionTo = HybridAnimatorTransitionPhase.None;
                    }

                    animatorComp.Mixer.SetInputWeight(0, 1.0f - animatorComp.Weight);
                    animatorComp.Mixer.SetInputWeight(1, animatorComp.Weight);
                }

                if (animatorOverride.State == AnimatorOverrideEnum.Playing)
                {
                    if (animatorComp.Mixer.IsValid() && animatorComp.Mixer.GetInputCount() >= 2)
                    {
                        var overridePlayable = animatorComp.Mixer.GetInput(1);

                        if (overridePlayable.IsValid())
                        {
                            double currentTime = overridePlayable.GetTime();
                            double duration = overridePlayable.GetDuration();

                            //Debug.Log($"{currentTime} -- {duration} -- {overridePlayable.IsDone()}");

                            // Check if clip finished playing
                            if (duration > 0 && currentTime >= duration)
                            {
                                //Debug.Log($"Clip finished! Time: {currentTime} / Duration: {duration}");
                                animatorComp.Reset();
                                animatorOverrideRW.ValueRW.Clear();
                            }
                        }
                    }
                }

                if (animatorOverride.State == AnimatorOverrideEnum.Requested)
                {
                    var clip = animatorOverrideRW.ValueRO.AnimationClip;

                    if (!clip.IsReferenceValid ||
                        !assetLoader.Load(clip) ||
                        !assetLoader.HasLoaded(clip))
                    {
                        continue;
                    }

                    animatorComp.ChangeClip(clip, animatorOverrideRW.ValueRO.Speed);
                    animatorComp.Graph.Play();
                    animatorOverride.State = AnimatorOverrideEnum.Playing;
                }
            }
        }
    }
}