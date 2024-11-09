// <copyright project="NZCore.Hybrid" file="HybridAnimator.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NZCore.Hybrid
{
    public class HybridAnimator : IComponentData
    {
        public Animator Animator;
        public PlayableGraph Graph;
        public AnimationMixerPlayable Mixer;
        public float Weight;
        public sbyte TransitionTo;

        public void ChangeClip(AnimationClip clip, float speed = 1.0f)
        {
            Graph.Disconnect(Mixer, 1);

            var newClip = AnimationClipPlayable.Create(Graph, clip);
            newClip.SetSpeed(speed);
            
            Graph.Connect(newClip, 0, Mixer, 1);

            TransitionTo = 1;
            //Mixer.SetInputWeight(0, 0.0f);
            //Mixer.SetInputWeight(1, 1.0f);
        }

        public void Reset()
        {
            TransitionTo = -1;
            //Mixer.SetInputWeight(0, 1.0f);
            //Mixer.SetInputWeight(1, 0.0f);
        }
    }
}