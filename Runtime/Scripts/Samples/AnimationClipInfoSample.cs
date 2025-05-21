using UnityEngine;
using ProjectCI_Animation.Runtime.Interface;

namespace ProjectCI_Animation.Runtime.Samples
{
    [CreateAssetMenu(fileName = "AnimationClipInfoSample", menuName = "ProjectCI/Animation/Samples/Animation Clip Info Sample")]
    public class AnimationClipInfoSample : ScriptableObject, IAnimationClipInfo
    {
        public AnimationClip Clip => clip;
        public float TransitDuration => transitDuration;
        public float[] BreakPoints => breakPoints;

        [SerializeField] private AnimationClip clip;
        [SerializeField] private float transitDuration;
        [SerializeField] private float[] breakPoints;
    }
} 