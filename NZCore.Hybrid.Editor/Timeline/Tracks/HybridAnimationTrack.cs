// <copyright project="NZCore.Timeline.Authoring" file="HybridAnimationTrack.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.ComponentModel;
using NZCore.Timeline.Authoring.Clips;
using NZCore.UI.Editor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace NZCore.Timeline.Authoring.Tracks
{

    [Serializable]
    [TrackColor(0, 0.25f, 0)]
    [DisplayName("DOTS/Hybrid Animation")]
    //[TrackClipType(typeof(AnimationPlayableAsset), false)]
    [TrackClipType(typeof(HybridAnimationClip), false)]
    public class HybridAnimationTrack : DOTSAnimationTrack
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            //Debug.Log("Creating Track Mixer for HybridAnimationTrack");
            //return ScriptPlayable<HybridAnimationMixerBehaviour>.Create(graph, inputCount);
            
            return base.CreateTrackMixer(graph, go, inputCount);
        }

        public override void Bake(BakingContext context)
        {
        }

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            if (PreviewWindow.Instance != null && 
                PreviewWindow.Instance.HybridAnimator != null && 
                PreviewWindow.Instance.HybridAnimator.Animator != null)
            {
                director.SetGenericBinding(this, PreviewWindow.Instance.HybridAnimator.Animator);
            }

            base.GatherProperties(director, driver);
        }
    }
}