using ProjectCI_Animation.Runtime.Interface;
using UnityEngine;

namespace ProjectCI_Animation.Runtime
{
    public enum AnimationIndexName
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Hit = 3,
        Death = 4,
        Defend = 5
    }

    public abstract class AnimationPlayableSupportBase : ScriptableObject
    {
        public abstract IAnimationClipInfo[] GetDefaultAnimationClipInfos();

        public int GetAnimationIndex(AnimationIndexName indexName)
        {
            return (int)indexName;
        }

        public IAnimationClipInfo GetAnimationClipInfo(AnimationIndexName indexName)
        {
            return GetDefaultAnimationClipInfos()[GetAnimationIndex(indexName)];
        }
    }

    public abstract class AnimationPlayableSupportBase<T> : AnimationPlayableSupportBase
        where T : ScriptableObject, IAnimationClipInfo
    {
        [SerializeField] protected T[] defaultAnimationClipInfos;
        
        public override IAnimationClipInfo[] GetDefaultAnimationClipInfos()
        {
            return defaultAnimationClipInfos;
        }
    }
}