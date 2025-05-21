using UnityEngine;
using UnityEngine.Playables;

namespace ProjectCI_Animation.Runtime
{
    [CreateAssetMenu(fileName = "AnimationPlayableSupport", menuName = "ProjectCI/Animation/Animation Playable Support")]
    public class AnimationPlayableSupport : ScriptableObject
    {
        [SerializeField] private AnimationClip[] defaultClips;
        
        public void SetupAnimationForObject(GameObject target, AnimationClip clip)
        {
            var manager = target.GetComponent<UnitAnimationManager>();
            if (manager == null)
            {
                manager = target.AddComponent<UnitAnimationManager>();
            }
            
            manager.PlayAnimation(clip);
        }

        public void BlendAnimations(GameObject target, AnimationClip from, AnimationClip to, float duration)
        {
            // TODO: Implement animation blending
            // This will require additional setup in UnitAnimationPlayable and UnitAnimationManager
            Debug.LogWarning("Animation blending not implemented yet");
        }

        public AnimationClip GetDefaultClip(int index)
        {
            if (defaultClips != null && index >= 0 && index < defaultClips.Length)
            {
                return defaultClips[index];
            }
            return null;
        }
    }
} 