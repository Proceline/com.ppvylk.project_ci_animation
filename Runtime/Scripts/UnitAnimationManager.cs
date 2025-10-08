using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using ProjectCI_Animation.Runtime.Interface;
using System;
using System.Collections;
using UnityEngine.Events;

namespace ProjectCI_Animation.Runtime
{
    internal struct AnimationParams
    {
        public int AnimIndex;
        public bool IsLoop;
        public float TransitDuration;
        public float[] BreakPoints;

        public static AnimationParams Default(int index, bool isLoop, float transitDuration, float[] breakPoints)
        {
            return new AnimationParams()
            {
                AnimIndex = index,
                IsLoop = isLoop,
                TransitDuration = transitDuration,
                BreakPoints = breakPoints
            };
        }
    }

    [RequireComponent(typeof(Animator))]
    public class UnitAnimationManager : MonoBehaviour
    {
        public AnimationPlayableSupportBase animationPlayableSupport;

        [NonSerialized] private AnimationPlayableSupportBase _currentPlayableSupport;
        
        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationMixerPlayable _mixerPlayable;
        private Animator _animator;

        private int _idleIndex = 0;
        private readonly Dictionary<string, AnimationParams> _clipPlayableMap = new();
        private readonly List<AnimationClipPlayable> _clipsPlayable = new();
        private Coroutine _playNonLoopAnimationCoroutine;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playableGraph = PlayableGraph.Create("UnitAnimationGraph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);

            SetupAnimationGraphDetails(animationPlayableSupport);
        }

        public void SetupAnimationGraphDetails(AnimationPlayableSupportBase support, bool firstTimeUpdate = true)
        {
            if (!_animator)
            {
                return;
            }

            if (!firstTimeUpdate)
            {
                _playableGraph.Stop();
                _clipsPlayable.Clear();
                _clipPlayableMap.Clear();
            }

            _currentPlayableSupport = support;
            AddPlayableClips(_currentPlayableSupport.GetDefaultAnimationClipInfos());
            _playableGraph.Play();
            
            var initIndex = _currentPlayableSupport.GetAnimationIndex(AnimationIndexName.Idle);
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
            => ForcePlayAnimation(_currentPlayableSupport.GetAnimationIndex(indexName));


        public void ForcePlayAnimation(string indexName)
            => ForcePlayAnimation(_currentPlayableSupport.GetAnimationIndex(indexName));
        

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
                    StartCoroutine(PlayNonLoopAnimation(clipPlayable, index, clipParams.TransitDuration));
            }
        }
        
        public void ForceStayOnAnimation(AnimationIndexName indexName)
            => ForceStayOnAnimation(_currentPlayableSupport.GetAnimationIndex(indexName));


        public void ForceStayOnAnimation(string indexName)
            => ForceStayOnAnimation(_currentPlayableSupport.GetAnimationIndex(indexName));
        
        private void ForceStayOnAnimation(int index)
        {
            var clipPlayable = _clipsPlayable[index];
            if (clipPlayable.IsValid())
            {
                PlayTargetClipPlayable(clipPlayable, index, false);
            }
        }

        private AnimationClipPlayable GetClipPlayable(IAnimationClipInfo clipInfo, out int index)
        {
            if (_clipPlayableMap.TryGetValue(clipInfo.Clip.name, out var clipParams))
            {
                index = clipParams.AnimIndex;
                return _clipsPlayable[clipParams.AnimIndex];
            }
            AddClipPlayable(clipInfo);
            index = _clipsPlayable.Count - 1;
            return _clipsPlayable[index];
        }

        public float GetPresetAnimationDuration(AnimationIndexName indexName)
        {
            var clipPlayable = _clipsPlayable[_currentPlayableSupport.GetAnimationIndex(indexName)];
            var animationClip = clipPlayable.GetAnimationClip();
            return animationClip ? animationClip.length : 0;
        }
        
        public float GetPresetAnimationDuration(string indexName)
        {
            var clipPlayable = _clipsPlayable[_currentPlayableSupport.GetAnimationIndex(indexName)];
            var animationClip = clipPlayable.GetAnimationClip();
            return animationClip ? animationClip.length : 0;
        }

        public float[] GetPresetAnimationBreakPoints(AnimationIndexName indexName)
        {
            var clipPlayable = _clipsPlayable[_currentPlayableSupport.GetAnimationIndex(indexName)];
            return GetPresetAnimationBreakPoints(clipPlayable);
        }

        public float[] GetPresetAnimationBreakPoints(string indexName)
        {
            var clipPlayable = _clipsPlayable[_currentPlayableSupport.GetAnimationIndex(indexName)];
            return GetPresetAnimationBreakPoints(clipPlayable);
        }
        
        private float[] GetPresetAnimationBreakPoints(AnimationClipPlayable clipPlayable)
        {
            if (_clipPlayableMap.TryGetValue(clipPlayable.GetAnimationClip().name, out var clipParams))
            {
                return clipParams.BreakPoints;
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
            int index = _currentPlayableSupport.GetAnimationIndex(indexName);
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
                    if (clipParams.IsLoop && clipPlayable.IsValid())
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