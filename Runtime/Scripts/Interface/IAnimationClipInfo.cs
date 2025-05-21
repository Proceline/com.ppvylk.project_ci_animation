using UnityEngine;

namespace ProjectCI_Animation.Runtime.Interface
{
    public interface IAnimationClipInfo
    {
        AnimationClip Clip { get; }
        float TransitDuration { get; }
        float[] BreakPoints { get; }
    }
} 