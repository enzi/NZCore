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
        public Playable RootPlayable;
        
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

        public void ChangePlayable<T>(T playable, float speed = 1.0f)
            where T : struct, IPlayable
        {
            Graph.Disconnect(Mixer, 1);
            
            playable.SetSpeed(speed);
            Graph.Connect(playable, 0, Mixer, 1);
            
            TransitionTo = 1;
        }

        public void Reset()
        {
            TransitionTo = -1;
            //Mixer.SetInputWeight(0, 1.0f);
            //Mixer.SetInputWeight(1, 0.0f);
        }
        
        public void SetTime(float normalizedTime)
        {
            Mixer.SetInputWeight(1, 1.0f);
            SetTimeRecursively(Mixer, normalizedTime);
            Graph.Evaluate(0);
            //Debug.Log($"{HybridAnimator.Graph.GetRootPlayable(0).GetTime()}");
        }

        public void SetTimeRecursively(double time)
        {
            SetTimeRecursively(RootPlayable, time);
        }
        
        public void SetTimeRecursively(Playable playable, double time)
        {
            playable.SetTime(time);

            // Recursively set time for all connected inputs
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                var input = playable.GetInput(i);
                if (input.IsValid())
                {
                    SetTimeRecursively(input, time);
                }
            }
        }
        
        public void SetTimeRecursivelyOutput(Playable playable, double time)
        {
            playable.SetTime(time);

            // Recursively set time for all connected inputs
            for (int i = 0; i < playable.GetOutputCount(); i++)
            {
                var output = playable.GetOutput(i);
                if (output.IsValid())
                {
                    SetTimeRecursively(output, time);
                }
            }
        }
    }
}