using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ProjectCI_Animation.Runtime
{
    public class UnitAnimationPlayable : PlayableBehaviour
    {
        private AnimationClipPlayable clipPlayable;
        private bool isPlaying;

        public void SetClip(PlayableGraph graph, AnimationClip clip)
        {
            if (clipPlayable.IsValid())
            {
                clipPlayable.Destroy();
            }

            if (clip != null)
            {
                clipPlayable = AnimationClipPlayable.Create(graph, clip);
                isPlaying = true;
            }
            else
            {
                isPlaying = false;
            }
        }

        public override void OnPlayableCreate(Playable playable)
        {
            isPlaying = false;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!isPlaying || !clipPlayable.IsValid()) return;
            
            // Connect the clip playable to our playable
            if (!playable.GetInput(0).IsValid())
            {
                playable.ConnectInput(0, clipPlayable, 0, 1.0f);
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (clipPlayable.IsValid())
            {
                clipPlayable.Destroy();
            }
            isPlaying = false;
        }
    }
} 