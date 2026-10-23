using FairyGUI;
using Spine.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DIY
{
    public partial class UI_Main
    {
        /// <summary>当前预览模型的正面 SkeletonAnimation。</summary>
        private SkeletonAnimation _previewFront;
        /// <summary>当前预览模型的背面 SkeletonAnimation（部分单位没有背面模型）。</summary>
        private SkeletonAnimation _previewBack;
        /// <summary>当前正在预览的动画名。</summary>
        private string _currentAnimationName;
        /// <summary>动画是否处于暂停状态。</summary>
        private bool _animationPaused;

        /// <summary>当前单位模型可用的全部动画名。</summary>
        public List<string> animationNames = new List<string>();

        private void InitAnimationControls()
        {
            // 默认循环播放，便于在编辑器中持续观察动作
            if (m_AnimationLoop != null)
            {
                m_AnimationLoop.selected = true;
                m_AnimationLoop.onChanged.Add(OnAnimationLoopChanged);
            }

            if (m_PlayBtn != null)
                m_PlayBtn.onClick.Add(PlayCurrentAnimation);

            if (m_PauseBtn != null)
                m_PauseBtn.onClick.Add(PauseCurrentAnimation);

            if (m_AnimationList != null)
                m_AnimationList.onClickItem.Add(OnAnimationItemClick);
        }

        /// <summary>
        /// 绑定当前预览模型的正面/背面 SkeletonAnimation，并刷新可用动画列表。
        /// </summary>
        private void SetupPreviewAnimations(GameObject model)
        {
            _previewFront = FindPreviewSkeleton(model, "model_forward", "model");
            _previewBack = FindPreviewSkeleton(model, "model_back");

            // 部分单位只有单一模型，兜底取第一个 SkeletonAnimation
            if (_previewFront == null && model != null)
            {
                var allSkeletons = model.GetComponentsInChildren<SkeletonAnimation>(true);
                _previewFront = allSkeletons.FirstOrDefault(x => x != _previewBack);
            }

            animationNames.Clear();
            if (model != null)
            {
                var allSkeletons = model.GetComponentsInChildren<SkeletonAnimation>(true);
                foreach (var skeleton in allSkeletons)
                {
                    var animations = skeleton?.Skeleton?.Data?.Animations;
                    if (animations == null) continue;

                    foreach (var animation in animations)
                    {
                        if (animation == null || string.IsNullOrEmpty(animation.Name)) continue;
                        if (!animationNames.Contains(animation.Name))
                            animationNames.Add(animation.Name);
                    }
                }
            }

            if (_previewBack != null)
                _previewBack.gameObject.SetActive(false);

            RefreshAnimationList();

            _animationPaused = false;
            _currentAnimationName = null;

            if (animationNames.Count == 0)
            {
                if (m_tip != null)
                    m_tip.text = m_tip.text + "\n当前模型没有可用的 Spine 动画";
                return;
            }

            PlayAnimation(FindDefaultAnimationName());
        }

        /// <summary>
        /// 按名称查找子节点上的 SkeletonAnimation，找不到时返回 null。
        /// </summary>
        private static SkeletonAnimation FindPreviewSkeleton(GameObject model, params string[] childNames)
        {
            if (model == null) return null;

            foreach (var childName in childNames)
            {
                var child = model.transform.Find(childName);
                if (child == null) continue;

                var skeleton = child.GetComponent<SkeletonAnimation>();
                if (skeleton != null) return skeleton;
            }

            return null;
        }

        /// <summary>
        /// 优先选择 Idle/Default，其次使用模型中的第一个动画。
        /// </summary>
        private string FindDefaultAnimationName()
        {
            if (animationNames.Count == 0) return null;

            string[] preferredNames = { "Idle", "Default" };
            foreach (var preferred in preferredNames)
            {
                var match = animationNames.FirstOrDefault(
                    x => string.Equals(x, preferred, System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match)) return match;
            }

            return animationNames[0];
        }

        /// <summary>
        /// 将动画名列表同步到界面的 AnimationList。
        /// </summary>
        private void RefreshAnimationList()
        {
            if (m_AnimationList == null) return;

            m_AnimationList.RemoveChildrenToPool();
            foreach (var animationName in animationNames)
            {
                GButton item = m_AnimationList.AddItemFromPool().asButton;
                if (item == null) continue;

                item.title = animationName;
                item.data = animationName;
                item.selected = false;
            }
        }

        /// <summary>
        /// 播放指定动画。若当前模型不存在该动画则直接忽略。
        /// </summary>
        private void PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;
            if (_previewFront == null && _previewBack == null) return;

            bool loop = m_AnimationLoop == null || m_AnimationLoop.selected;
            bool played = TrySetSkeletonAnimation(_previewFront, animationName, loop);

            if (_previewBack != null && _previewBack != _previewFront)
                played |= TrySetSkeletonAnimation(_previewBack, animationName, loop);

            if (!played) return;

            _currentAnimationName = animationName;
            _animationPaused = false;
            SetPreviewTimeScale(1f);
            SelectAnimationListItem(animationName);
        }

        private static bool TrySetSkeletonAnimation(SkeletonAnimation skeleton, string animationName, bool loop)
        {
            if (skeleton == null) return false;

            var skeletonData = skeleton.Skeleton?.Data;
            if (skeletonData == null) return false;

            var animation = skeletonData.FindAnimation(animationName);
            if (animation == null) return false;

            // 同步 loop 字段，保证后续通过 AnimationName 重新设置时仍使用当前循环状态
            skeleton.loop = loop;
            skeleton.timeScale = 1f;

            var state = skeleton.AnimationState;
            if (state == null) return false;

            var entry = state.SetAnimation(0, animation, loop);
            if (entry != null) entry.Loop = loop;
            return true;
        }

        /// <summary>
        /// 点击 PlayBtn：恢复暂停；如果动画已播放完（非循环），则从头重播。
        /// </summary>
        private void PlayCurrentAnimation()
        {
            if (_previewFront == null) return;

            if (string.IsNullOrEmpty(_currentAnimationName))
            {
                if (animationNames.Count > 0)
                    PlayAnimation(animationNames[0]);
                return;
            }

            if (_animationPaused)
            {
                _animationPaused = false;
                SetPreviewTimeScale(1f);
                return;
            }

            var entry = _previewFront.AnimationState?.GetCurrent(0);
            if (entry?.Animation != null && !entry.Loop &&
                entry.TrackTime >= entry.Animation.Duration)
            {
                PlayAnimation(_currentAnimationName);
                return;
            }

            SetPreviewTimeScale(1f);
        }

        /// <summary>
        /// 点击 PauseBtn：冻结当前动画。
        /// </summary>
        private void PauseCurrentAnimation()
        {
            if (_previewFront == null) return;

            _animationPaused = true;
            SetPreviewTimeScale(0f);
        }

        private void SetPreviewTimeScale(float timeScale)
        {
            if (_previewFront != null)
                _previewFront.timeScale = timeScale;

            if (_previewBack != null && _previewBack != _previewFront)
                _previewBack.timeScale = timeScale;
        }

        /// <summary>
        /// AnimationLoop 勾选状态变化时，同步到当前动画轨道。
        /// </summary>
        private void OnAnimationLoopChanged()
        {
            bool loop = m_AnimationLoop != null && m_AnimationLoop.selected;

            ApplyLoopToSkeleton(_previewFront, loop);
            ApplyLoopToSkeleton(_previewBack, loop);

            // 从“不循环”切到“循环”时，如果动画已经停在末帧，则重新播放
            if (loop && !string.IsNullOrEmpty(_currentAnimationName))
            {
                var entry = _previewFront?.AnimationState?.GetCurrent(0);
                if (entry?.Animation != null && !entry.Loop &&
                    entry.TrackTime >= entry.Animation.Duration)
                {
                    PlayAnimation(_currentAnimationName);
                }
            }
        }

        private static void ApplyLoopToSkeleton(SkeletonAnimation skeleton, bool loop)
        {
            if (skeleton == null) return;

            skeleton.loop = loop;
            var entry = skeleton.AnimationState?.GetCurrent(0);
            if (entry != null) entry.Loop = loop;
        }

        /// <summary>
        /// 点击动画列表项时立即播放该动画。
        /// </summary>
        private void OnAnimationItemClick(EventContext context)
        {
            var item = context.data as GObject;
            var animationName = item?.data as string;
            if (!string.IsNullOrEmpty(animationName))
                PlayAnimation(animationName);
        }

        /// <summary>
        /// 让列表中正在播放的动画项保持选中状态。
        /// </summary>
        private void SelectAnimationListItem(string animationName)
        {
            if (m_AnimationList == null) return;

            for (int i = 0; i < m_AnimationList.numItems; i++)
            {
                var item = m_AnimationList.GetChildAt(i).asButton;
                if (item == null) continue;

                item.selected = string.Equals(
                    item.data as string,
                    animationName,
                    System.StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ClearPreviewAnimations()
        {
            _previewFront = null;
            _previewBack = null;
            _currentAnimationName = null;
            _animationPaused = false;
            animationNames.Clear();
            RefreshAnimationList();
        }
    }
}
