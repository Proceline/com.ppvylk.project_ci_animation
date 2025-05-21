using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ProjectCI_Animation.Runtime
{
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

        // 混合相关
        private bool isBlending = false;
        private float blendTimer = 0f;
        private bool isTestAnimation2Playing = false;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playableGraph = PlayableGraph.Create("UnitAnimationGraph");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 创建两个clipPlayable
            if (testAnimation != null)
                clipPlayable1 = AnimationClipPlayable.Create(playableGraph, testAnimation);
            if (testAnimation2 != null)
            {
                clipPlayable2 = AnimationClipPlayable.Create(playableGraph, testAnimation2);
            }

            // 创建mixer
            mixerPlayable = AnimationMixerPlayable.Create(playableGraph, 2);
            if (clipPlayable1.IsValid())
                playableGraph.Connect(clipPlayable1, 0, mixerPlayable, 0);
            if (clipPlayable2.IsValid())
                playableGraph.Connect(clipPlayable2, 0, mixerPlayable, 1);

            // 默认只播放testAnimation
            mixerPlayable.SetInputWeight(0, 1f);
            mixerPlayable.SetInputWeight(1, 0f);

            playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            playableOutput.SetSourcePlayable(mixerPlayable);

            playableGraph.Play();
        }

        private void Update()
        {
            // 空格只切到testAnimation2，不能切回
            if (Input.GetKeyDown(KeyCode.Space) && !isTestAnimation2Playing && !isBlending)
            {
                PlayAnimation2();
            }

            // 自动blend回testAnimation
            if (isTestAnimation2Playing && testAnimation2 != null && clipPlayable2.IsValid())
            {
                if (clipPlayable2.IsDone() && !isBlending)
                {
                    StartBlendToFirst();
                }
            }

            if (isBlending)
            {
                blendTimer += Time.deltaTime;
                float blendWeight = Mathf.Clamp01(blendTimer / blendDuration);
                mixerPlayable.SetInputWeight(0, blendWeight);
                mixerPlayable.SetInputWeight(1, 1f - blendWeight);
                if (blendWeight >= 1f)
                {
                    isBlending = false;
                    isTestAnimation2Playing = false;
                    // 重置testAnimation2时间
                    clipPlayable2.SetTime(0);
                    clipPlayable2.SetDone(false);
                }
            }
        }

        private void PlayAnimation2()
        {
            if (clipPlayable2.IsValid())
            {
                clipPlayable2.SetTime(0);
                clipPlayable2.SetDone(false);
                clipPlayable2.SetDuration(testAnimation2.length);
                mixerPlayable.SetInputWeight(0, 0f);
                mixerPlayable.SetInputWeight(1, 1f);
                isTestAnimation2Playing = true;
                isBlending = false;
                blendTimer = 0f;
            }
        }

        private void StartBlendToFirst()
        {
            isBlending = true;
            blendTimer = 0f;
        }

        public void PlayAnimation(AnimationClip clip)
        {
            // 可选：实现动态切换动画片段
        }

        public void StopAnimation()
        {
            // 可选：实现停止动画
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