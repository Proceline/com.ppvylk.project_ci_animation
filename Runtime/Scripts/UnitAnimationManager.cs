using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using ProjectCI_Animation.Runtime.Interface;
using System.Threading;
using System;
using System.Collections;
using UnityEngine.Events;

namespace ProjectCI_Animation.Runtime
{
    internal struct AnimationParams
    {
        public int m_Index;
        public bool m_bIsLoop;
        public float m_TransitDuration;
        public float[] m_BreakPoints;

        public static AnimationParams Default(int index, bool isLoop, float transitDuration, float[] breakPoints)
        {
            return new AnimationParams()
            {
                m_Index = index,
                m_bIsLoop = isLoop,
                m_TransitDuration = transitDuration,
                m_BreakPoints = breakPoints
            };
        }
    }

    [RequireComponent(typeof(Animator))]
    public class UnitAnimationManager : MonoBehaviour
    {
        public AnimationPlayableSupportBase animationPlayableSupport;
        
        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationMixerPlayable _mixerPlayable;
        private Animator _animator;

        private int _idleIndex = 0;
        private readonly Dictionary<string, AnimationParams> _clipPlayableMap = new();
        private readonly List<AnimationClipPlayable> _clipsPlayable = new();
        private Coroutine _playNonLoopAnimationCoroutine;

        public UnityEvent<int> OnIndexModified;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playableGraph = PlayableGraph.Create("UnitAnimationGraph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);
            
            AddPlayableClips(animationPlayableSupport.GetDefaultAnimationClipInfos());

            if (_mixerPlayable.IsValid())
            {
                _playableOutput.SetSourcePlayable(_mixerPlayable);
            }

            _playableGraph.Play();
            
            int initIndex = animationPlayableSupport.GetAnimationIndex(AnimationIndexName.Idle);
            PlayLoopAnimation(AnimationIndexName.Idle);
            _idleIndex = initIndex;
        }

        public void ForcePlayAnimation(IAnimationClipInfo clipInfo)
        {
            if (_playNonLoopAnimationCoroutine != null)
            {
                StopCoroutine(_playNonLoopAnimationCoroutine);
            }
            var clipPlayable = GetClipPlayable(clipInfo, out int index);
            _playNonLoopAnimationCoroutine = 
                StartCoroutine(PlayNonLoopAnimation(clipPlayable, index, clipInfo.TransitDuration));
        }

        public void ForcePlayAnimation(AnimationIndexName indexName)
            => ForcePlayAnimation(animationPlayableSupport.GetAnimationIndex(indexName));


        public void ForcePlayAnimation(string indexName)
            => ForcePlayAnimation(animationPlayableSupport.GetAnimationIndex(indexName));
        

        private void ForcePlayAnimation(int index)
        {
            if (_playNonLoopAnimationCoroutine != null)
            {
                StopCoroutine(_playNonLoopAnimationCoroutine);
            }

            var clipPlayable = _clipsPlayable[index];
            if (_clipPlayableMap.TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                _playNonLoopAnimationCoroutine =
                    StartCoroutine(PlayNonLoopAnimation(clipPlayable, index, clipParams.m_TransitDuration));
            }
        }

        private AnimationClipPlayable GetClipPlayable(IAnimationClipInfo clipInfo, out int index)
        {
            if (_clipPlayableMap.TryGetValue(clipInfo.Clip.name, out var clipParams))
            {
                index = clipParams.m_Index;
                return _clipsPlayable[clipParams.m_Index];
            }
            AddClipPlayable(clipInfo);
            index = _clipsPlayable.Count - 1;
            return _clipsPlayable[index];
        }

        public float GetPresetAnimationDuration(AnimationIndexName indexName)
        {
            var clipPlayable = _clipsPlayable[animationPlayableSupport.GetAnimationIndex(indexName)];
            if (_clipPlayableMap.TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                return clipParams.m_TransitDuration;
            }
            return 0;
        }
        
        public float GetPresetAnimationDuration(string indexName)
        {
            var clipPlayable = _clipsPlayable[animationPlayableSupport.GetAnimationIndex(indexName)];
            if (_clipPlayableMap.TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                return clipParams.m_TransitDuration;
            }
            return 0;
        }

        public float[] GetPresetAnimationBreakPoints(AnimationIndexName indexName)
        {
            var clipPlayable = _clipsPlayable[animationPlayableSupport.GetAnimationIndex(indexName)];
            return GetPresetAnimationBreakPoints(clipPlayable);
        }

        public float[] GetPresetAnimationBreakPoints(string indexName)
        {
            var clipPlayable = _clipsPlayable[animationPlayableSupport.GetAnimationIndex(indexName)];
            return GetPresetAnimationBreakPoints(clipPlayable);
        }
        
        private float[] GetPresetAnimationBreakPoints(AnimationClipPlayable clipPlayable)
        {
            if (_clipPlayableMap.TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                return clipParams.m_BreakPoints;
            }
            return Array.Empty<float>();
        }

        private void AddClipPlayable(IAnimationClipInfo clipInfo)
        {
            var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipInfo.Clip);
            int index = _clipsPlayable.Count;
            _clipsPlayable.Add(clipPlayable);
            
            _clipPlayableMap.Add(clipInfo.Clip.name, AnimationParams.Default(index, clipInfo.Clip.isLooping, 
                clipInfo.TransitDuration, clipInfo.BreakPoints));

            RebuildMixer();
        }

        private void AddPlayableClips(IAnimationClipInfo[] clipInfos)
        {
            for (int i = 0; i < clipInfos.Length; i++)
            {
                var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipInfos[i].Clip);
                _clipsPlayable.Add(clipPlayable);

                _clipPlayableMap.TryAdd(clipInfos[i].Clip.name, AnimationParams.Default(i, clipInfos[i].Clip.isLooping, 
                    clipInfos[i].TransitDuration, clipInfos[i].BreakPoints));
            }

            RebuildMixer();
        }

        private void RebuildMixer()
        {
            if (_mixerPlayable.IsValid())
            {
                _mixerPlayable.Destroy();
            }

            int totalInputCount =_clipsPlayable.Count;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, totalInputCount);

            for (int i = 0; i < totalInputCount; i++)
            {
                var clipPlayable = _clipsPlayable[i];
                if (clipPlayable.IsValid())
                    _playableGraph.Connect(clipPlayable, 0, _mixerPlayable, i);
            }
            
            _playableOutput.SetSourcePlayable(_mixerPlayable);
        }
             

        public void PlayLoopAnimation(AnimationIndexName indexName)
        {
            int index = animationPlayableSupport.GetAnimationIndex(indexName);
            PlayLoopAnimation(index);
        }

        private void PlayLoopAnimation(int index)
        {
            if (index < _clipsPlayable.Count)
            {
                var clipPlayable = _clipsPlayable[index];
                var clipName = clipPlayable.GetAnimationClip().name;
                if (_clipPlayableMap.TryGetValue(clipName, out var clipParams))
                {
                    if (clipParams.m_bIsLoop && clipPlayable.IsValid())
                    {
                        PlayTargetClipPlayable(clipPlayable, index, true);
                        _idleIndex = index;
                    }
                    else
                    {
                        Debug.LogError($"Animation {clipName} is not loopable");
                    }
                }
                else
                {
                    Debug.LogError($"Animation {clipName} not found");
                }
            }
        }

        private IEnumerator PlayNonLoopAnimation(AnimationClipPlayable clipPlayable, int index, float transitDuration)
        {
            if (clipPlayable.IsValid())
            {
                PlayTargetClipPlayable(clipPlayable, index, false);
                float realDuration = clipPlayable.GetAnimationClip().length - transitDuration;

                yield return Awaitable.WaitForSecondsAsync(realDuration);

                float startTime = 0;
                while (startTime <= transitDuration)
                {
                    startTime += Time.deltaTime;
                    float blendWeight = transitDuration > 0 ? 
                        Mathf.Clamp01(startTime / transitDuration) : 1f;
                    _mixerPlayable.SetInputWeight(_idleIndex, blendWeight);
                    _mixerPlayable.SetInputWeight(index, 1f - blendWeight);
                    if (blendWeight >= 1f)
                    {
                        break;
                    }
                }
                AssignAnimationIndex(_idleIndex);
            }
        }

        private void PlayTargetClipPlayable(AnimationClipPlayable clipPlayable, int index, bool isLoop)
        {
            clipPlayable.SetTime(0);
            clipPlayable.SetDone(false);
            if (!isLoop)
            {
                clipPlayable.SetDuration(clipPlayable.GetAnimationClip().length);
            }
            for (int i = 0; i < _mixerPlayable.GetInputCount(); i++)
            {
                _mixerPlayable.SetInputWeight(i, 0f);
            }
            _mixerPlayable.SetInputWeight(index, 1f);

            AssignAnimationIndex(index);
        }

        private void AssignAnimationIndex(int index)
        {
            OnIndexModified.Invoke(index);
        }

        private void OnDestroy()
        {
            if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }
        }
    }
} 