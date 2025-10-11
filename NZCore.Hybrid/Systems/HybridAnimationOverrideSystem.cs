// <copyright project="NZCore" file="HybridAnimationOverrideSystem.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;

namespace NZCore.Hybrid
{
    public partial class HybridAnimationOverrideSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var assetLoader = SystemAPI.GetSingleton<WeakAssetLoaderSingleton>();

            foreach (var animatorOverrideStateRW
                     in SystemAPI.Query<RefRW<AnimatorOverrideState>>().WithChangeFilter<AnimatorOverride>())
            {
                animatorOverrideStateRW.ValueRW.Playing = 0;
            }
            
            foreach (var (hybridAnimatorRW, animatorOverrideRO, animatorOverrideStateRW)
                     in SystemAPI.Query<RefRW<HybridAnimator>, RefRO<AnimatorOverride>, RefRW<AnimatorOverrideState>>())
            {
                ref var state = ref animatorOverrideStateRW.ValueRW;
                ref var animatorComp = ref hybridAnimatorRW.ValueRW;

                if (!animatorComp.Graph.IsValid())
                {
                    continue;
                }

                if (animatorComp.TransitionTo != HybridAnimatorTransitionPhase.None)
                {
                    animatorComp.Weight = animatorComp.TransitionTo == HybridAnimatorTransitionPhase.ToCustom ? 
                        Mathf.Min(animatorComp.Weight + 0.1f, 1.0f) : 
                        Mathf.Max(animatorComp.Weight - 0.1f, 0.0f);
                    
                    if (Mathf.Approximately(animatorComp.Weight, 1.0f))
                    {
                        animatorComp.TransitionTo = HybridAnimatorTransitionPhase.None;
                    }
                    
                    animatorComp.Mixer.SetInputWeight(0, 1.0f - animatorComp.Weight);
                    animatorComp.Mixer.SetInputWeight(1, animatorComp.Weight);
                }
                
                if (state.Playing == 1)
                    continue;
                
                var clip = animatorOverrideRO.ValueRO.AnimationClip;

                if (!clip.IsReferenceValid)
                {
                    continue;
                }

                if (!assetLoader.Load(clip))
                {
                    continue;
                }
                
                if (!assetLoader.HasLoaded(clip))
                    continue;
                
                animatorComp.ChangeClip(clip.Result, animatorOverrideRO.ValueRO.Scale);
                animatorComp.Graph.Play();
                state.Playing = 1;
            }
        }
    }
}