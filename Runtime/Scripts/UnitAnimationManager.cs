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
        public AnimationClip testAnimation;
        public AnimationClip testAnimation2;
        public float blendDuration = 0.5f;
        
        private PlayableGraph playableGraph;
        private AnimationPlayableOutput playableOutput;
        private AnimationMixerPlayable mixerPlayable;
        private AnimationClipPlayable clipPlayable1;
        private AnimationClipPlayable clipPlayable2;
        private Animator animator;

        private int _idleIndex = 0;
        private readonly Dictionary<string, AnimationParams> _clipPlayableMap = new();
        private readonly List<AnimationClipPlayable> _clipPlayables = new();
        private CancellationTokenSource _cancelTokenSource;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playableGraph = PlayableGraph.Create("UnitAnimationGraph");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 创建两个clipPlayable
            var clipPlayable1 = AddClipPlayable(testAnimation, 0, null);
            var clipPlayable2 = AddClipPlayable(testAnimation2, 0.1f, new float[] { 0.5f });

            // 创建mixer
            mixerPlayable = AnimationMixerPlayable.Create(playableGraph, 2);
            if (clipPlayable1.IsValid())
                playableGraph.Connect(clipPlayable1, 0, mixerPlayable, 0);
            if (clipPlayable2.IsValid())
                playableGraph.Connect(clipPlayable2, 0, mixerPlayable, 1);

            playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            playableOutput.SetSourcePlayable(mixerPlayable);

            playableGraph.Play();
            
            PlayLoopAnimation(0);
            _idleIndex = 0;
        }

        private async void Update()
        {
            // 空格只切到testAnimation2，不能切回
            if (Input.GetKeyDown(KeyCode.Space)) //&& !isTestAnimation2Playing && !isBlending)
            {
                _cancelTokenSource?.Cancel();
                _cancelTokenSource?.Dispose();
                _cancelTokenSource = new CancellationTokenSource();
                var clipPlayable = GetClipPlayable(testAnimation2);
                await PlayNonLoopAnimation(clipPlayable, 1, 0.1f, _cancelTokenSource);
            }
        }

        public AnimationClipPlayable GetClipPlayable(IAnimationClipInfo clipInfo)
        {
            if (_clipPlayableMap.TryGetValue(clipInfo.Clip.name, out var clipParams))
            {
                return _clipPlayables[clipParams.m_Index];
            }
            AddClipPlayable(clipInfo.Clip, clipInfo.TransitDuration, clipInfo.BreakPoints);
            return _clipPlayables[_clipPlayableMap[clipInfo.Clip.name].m_Index];
        }

        public AnimationClipPlayable GetClipPlayable(AnimationClip clip, float transitDuration = 0.1f)
        {
            if (_clipPlayableMap.TryGetValue(clip.name, out var clipParams))
            {
                return _clipPlayables[clipParams.m_Index];
            }
            AddClipPlayable(clip, transitDuration, new float[] { 0.5f });
            return _clipPlayables[_clipPlayableMap[clip.name].m_Index];
        }

        public AnimationClipPlayable AddClipPlayable(AnimationClip clip, float transitDuration, float[] breakPoints)
        {
            var clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            int index = _clipPlayables.Count;
            _clipPlayables.Add(clipPlayable);
            
            _clipPlayableMap.Add(clip.name, AnimationParams.Default(index, clip.isLooping, 
                transitDuration, breakPoints));
            return clipPlayable;
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
                    mixerPlayable.SetInputWeight(_idleIndex, blendWeight);
                    mixerPlayable.SetInputWeight(index, 1f - blendWeight);
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
                // clipPlayable.SetTime(0);
                // clipPlayable.SetDone(false);
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
            mixerPlayable.SetInputWeight(_idleIndex, 0f);
            mixerPlayable.SetInputWeight(index, 1f);
        }

        private void OnDestroy()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }
    }
} 