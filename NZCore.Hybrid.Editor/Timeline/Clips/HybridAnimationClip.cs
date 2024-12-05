// <copyright project="NZCore.Timeline.Authoring" file="DOTSAnimationPlayableAsset.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Timeline.Authoring.Editor;
using NZCore.Timeline.Data;
using NZCore.UI.Editor;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace NZCore.Timeline.Authoring.Clips
{
    public partial class HybridAnimationClip : AnimationPlayableAsset, IDOTSClip, ITimelineClipAsset
    {
        public ClipCaps clipCaps => ClipCaps.Blending;
        
        public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
        {
            // Debug.Log("DOTSAnimationPlayableAsset CreatePlayable");
            // var playable = ScriptPlayable<HybridAnimationClipVisualizer>.Create(graph);
            // //var behaviour = playable.GetBehaviour();
            // //behaviour.HybridAnimator = ;
            // //behaviour.Root = owner.transform; 
            // return playable;
            
            return base.CreatePlayable(graph, go);
        }

        public Entity CreateClipEntity(BakingContext context)
        {
            return context.CreateClipEntity();
        }

        public void Bake(Entity clipEntity, BakingContext context)
        {
        }

        public void BakeBlob(BakingContext context, ref BlobBuilder builder, ref TimelineClipBlob clip)
        {
        }
    }

    public class HybridAnimationClipVisualizer : ClipVisualizerBase
    {
        //public HybridAnimator HybridAnimator;
        
        protected override void OnUpdate(Playable playable)
        {
            Debug.Log("DOTSAnimationPlayableAssetVisualizer OnUpdate");
            
            if (PreviewWindow.Instance == null)
                return;
            
            PreviewWindow.Instance.HybridAnimator.ChangePlayable(playable);
        }
    }
}