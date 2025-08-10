using System.Collections.Generic;
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
        Defend = 5,
        EndMarkDontUse = 6
    }

    public abstract class AnimationPlayableSupportBase : ScriptableObject
    {
        protected abstract IAnimationIndexAddon AnimationIndexAddon { get; }
        
        internal abstract IAnimationClipInfo[] GetDefaultAnimationClipInfos();

        internal int GetAnimationIndex(AnimationIndexName indexName)
        {
            return (int)indexName;
        }

        internal int GetAnimationIndex(string indexName)
        {
            int originalIndex = AnimationIndexAddon.GetOriginalIndexByName(indexName);
            return originalIndex + (int)AnimationIndexName.EndMarkDontUse;
        }
    }

    public abstract class AnimationPlayableSupportBase<T> : AnimationPlayableSupportBase
        where T : ScriptableObject, IAnimationClipInfo
    {
        [SerializeField] protected T[] defaultAnimationClipInfos;
        
        internal override IAnimationClipInfo[] GetDefaultAnimationClipInfos()
        {
            return defaultAnimationClipInfos;
        }
    }
}