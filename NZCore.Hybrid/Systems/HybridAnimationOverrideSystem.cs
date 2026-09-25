// <copyright project="NZCore" file="HybridAnimationOverrideSystem.cs" version="1.2.2">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using AOT;
using NZCore.AssetManagement;
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Playables;

namespace NZCore.Hybrid
{
    public partial struct HybridAnimationOverrideSystem : ISystem
    {
        private const float TransitionSpeed = 5;
        
        private delegate void ChangeClipDelegate(ref HybridAnimator animator, in UntypedWeakReferenceId clipId, float speed);
        private ManagedDelegate<ChangeClipDelegate> _changeClipFunction;

        public void OnCreate(ref SystemState state)
        {
            state.CreateSingleton<HybridAnimatorRequestSingleton>();

            _changeClipFunction = new ManagedDelegate<ChangeClipDelegate>(ChangeClip);
        }

        public void OnDestroy(ref SystemState state)
        {
            _changeClipFunction.Dispose();

            state.DisposeSingleton<HybridAnimatorRequestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRW<HybridAnimatorRequestSingleton>();

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
                    if (!animatorOverride.IsOwned)
                    {
                        animatorOverride.SetClip(clipRequest.Clip, clipRequest.Speed);
                    }
                }

                request.Clear();
            }

            foreach (var (hybridAnimatorRW, animatorOverrideRW) in SystemAPI.Query<RefRW<HybridAnimator>, RefRW<AnimatorOverride>>())
            {
                ref var animatorComp = ref hybridAnimatorRW.ValueRW;

                if (!animatorComp.Graph.IsValid())
                {
                    continue;
                }

                ref var animatorOverride = ref animatorOverrideRW.ValueRW;

                if (animatorOverride.State == AnimatorOverrideEnum.Default && animatorComp.Weight > 0)
                {
                    animatorComp.Reset();
                }

                if (animatorOverride.State == AnimatorOverrideEnum.Requested)
                {
                    var clip = animatorOverride.AnimationClip;

                    if (!clip.IsValidBurst() ||
                        !assetLoader.Load(clip) ||
                        !assetLoader.HasLoaded(clip))
                    {
                        continue;
                    }

                    // unbursted call
                    _changeClipFunction.Ptr.Invoke(ref animatorComp, clip.Id, animatorOverride.Speed);
                    animatorComp.Graph.Play();
                    animatorOverride.State = AnimatorOverrideEnum.Playing;
                }

                if (animatorComp.TransitionTo != HybridAnimatorTransitionPhase.None)
                {
                    var transitionSpeed = TransitionSpeed * deltaTime;
                    var target = animatorComp.TransitionTo == HybridAnimatorTransitionPhase.ToCustom ? 1.0f : 0.0f;

                    animatorComp.Weight = animatorComp.TransitionTo == HybridAnimatorTransitionPhase.ToCustom
                        ? math.min(animatorComp.Weight + transitionSpeed, 1.0f)
                        : math.max(animatorComp.Weight - transitionSpeed, 0.0f);

                    // min/max clamp to exactly the target, so an exact compare is enough
                    if (animatorComp.Weight == target)
                    {
                        animatorComp.TransitionTo = HybridAnimatorTransitionPhase.None;
                    }

                    animatorComp.Mixer.SetInputWeight(0, 1.0f - animatorComp.Weight);
                    animatorComp.Mixer.SetInputWeight(1, animatorComp.Weight);
                }

                // An owned override's time and end are up to its owner.
                if (animatorOverride.State == AnimatorOverrideEnum.Playing && !animatorOverride.IsOwned)
                {
                    if (animatorComp.Mixer.IsValid() && animatorComp.Mixer.GetInputCount() >= 2)
                    {
                        var overridePlayable = animatorComp.Mixer.GetInput(1);

                        if (overridePlayable.IsValid())
                        {
                            var currentTime = overridePlayable.GetTime();
                            var duration = overridePlayable.GetDuration();

                            //Debug.Log($"{currentTime} -- {duration} -- {overridePlayable.IsDone()}");

                            // Check if clip finished playing
                            if (duration > 0 && currentTime >= duration)
                            {
                                //Debug.Log($"Clip finished! Time: {currentTime} / Duration: {duration}");
                                animatorComp.Reset();
                                animatorOverride.Clear();
                            }
                        }
                    }
                }
            }
        }

        [MonoPInvokeCallback(typeof(ChangeClipDelegate))]
        private static void ChangeClip(ref HybridAnimator animator, in UntypedWeakReferenceId clipId, float speed)
        {
            animator.ChangeClip(new WeakObjectReference<AnimationClip>(clipId), speed);
        }
    }
}
