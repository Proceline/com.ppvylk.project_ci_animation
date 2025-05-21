using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using ProjectCI_Animation.Runtime.Interface;
using System.Threading;
using System;

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
        private readonly List<AnimationClipPlayable> _clipPlayables = new();
        private CancellationTokenSource _cancelTokenSource;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playableGraph = PlayableGraph.Create("UnitAnimationGraph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);
            
            AddClipPlayables(animationPlayableSupport.GetDefaultAnimationClipInfos());

            if (_mixerPlayable.IsValid())
            {
                _playableOutput.SetSourcePlayable(_mixerPlayable);
            }

            _playableGraph.Play();
            
            int initIndex = animationPlayableSupport.GetAnimationIndex(AnimationIndexName.Idle);
            PlayLoopAnimation(initIndex);
            _idleIndex = initIndex;
        }

        public async void ForcePlayAnimation(IAnimationClipInfo clipInfo)
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = new CancellationTokenSource();
            var clipPlayable = GetClipPlayable(clipInfo, out int index);
            await PlayNonLoopAnimation(clipPlayable, index, 
                clipInfo.TransitDuration, _cancelTokenSource);
        }

        public async void ForcePlayAnimation(AnimationIndexName indexName)
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _cancelTokenSource = new CancellationTokenSource();
            var clipPlayable = GetClipPlayable(indexName, out int index);
            if (_clipPlayableMap.
                TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                await PlayNonLoopAnimation(clipPlayable, index, 
                    clipParams.m_TransitDuration, _cancelTokenSource);
            }
        }

        public AnimationClipPlayable GetClipPlayable(IAnimationClipInfo clipInfo, out int index)
        {
            if (_clipPlayableMap.TryGetValue(clipInfo.Clip.name, out var clipParams))
            {
                index = clipParams.m_Index;
                return _clipPlayables[clipParams.m_Index];
            }
            AddClipPlayable(clipInfo);
            index = _clipPlayables.Count - 1;
            return _clipPlayables[index];
        }

        public AnimationClipPlayable GetClipPlayable(AnimationIndexName indexName, out int index)
        {
            index = animationPlayableSupport.GetAnimationIndex(indexName);
            return _clipPlayables[index];
        }

        public AnimationClipPlayable AddClipPlayable(IAnimationClipInfo clipInfo)
        {
            var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipInfo.Clip);
            int index = _clipPlayables.Count;
            _clipPlayables.Add(clipPlayable);
            
            _clipPlayableMap.Add(clipInfo.Clip.name, AnimationParams.Default(index, clipInfo.Clip.isLooping, 
                clipInfo.TransitDuration, clipInfo.BreakPoints));

            RebuildMixer();

            return clipPlayable;
        }

        private void AddClipPlayables(IAnimationClipInfo[] clipInfos)
        {
            for (int i = 0; i < clipInfos.Length; i++)
            {
                var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipInfos[i].Clip);
                _clipPlayables.Add(clipPlayable);

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

            int totalInputCount =_clipPlayables.Count;
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, totalInputCount);

            for (int i = 0; i < totalInputCount; i++)
            {
                var clipPlayable = _clipPlayables[i];
                if (clipPlayable.IsValid())
                    _playableGraph.Connect(clipPlayable, 0, _mixerPlayable, i);
            }
            
            _playableOutput.SetSourcePlayable(_mixerPlayable);
        }
             

        private void PlayLoopAnimation(int index)
        {
            if (index < _clipPlayables.Count)
            {
                var clipPlayable = _clipPlayables[index];
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

        private async Awaitable PlayNonLoopAnimation(AnimationClipPlayable clipPlayable, int index,
            float transitDuration, CancellationTokenSource tokenSource)
        {

            if (clipPlayable.IsValid())
            {
                PlayTargetClipPlayable(clipPlayable, index, false);

                float realDuration = clipPlayable.GetAnimationClip().length - transitDuration;

                try 
                {
                    await Awaitable.WaitForSecondsAsync(realDuration, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                float startTime = 0;
                while (startTime <= transitDuration && !tokenSource.IsCancellationRequested)
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
                    
                    try
                    {
                        await Awaitable.EndOfFrameAsync(tokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
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
            _mixerPlayable.SetInputWeight(_idleIndex, 0f);
            _mixerPlayable.SetInputWeight(index, 1f);
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