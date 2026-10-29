using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

public class Effect : MonoBehaviour
{
    public int Id;
    public EffectData EffectData => Database.Instance.Get<EffectData>(Id);
    public Unit Parent;
    ParticleSystem[] PS;
    public TrailRenderer[] TR;
    public bool IsHide = false;
    float LifeTime = 5f;

    PlayerUnitModel PlayerUnitModel;
    BoneFollower BoneFollower;
    bool forward;
    bool _isBulletEffect;
    Bullet _bullet;

    // —— 与 ArkFx 的启动时序对接 ——
    // 表驱动特效由 EffectManager/BulletManager 以「先入池、后定位」的方式创建：
    //   GetEffect -> 调用方设置位置/挂点/朝向 -> Init()/Play()
    // ArkFx 的发射器、FxDelay 必须在最终位置确定之后才能启动，否则：
    //   1) OnPlayEmitter 会在世界原点生成子特效；
    //   2) 随后的 Init->Play 会把 FxDelay 刚隐藏的根节点重新激活，延迟被抵消。
    // 因此这里把 ArkFxLoader.Kick 推迟到第一次 Play()，并用 _started 保证每个池化实例只 Kick 一次。
    [System.NonSerialized] public string PrefabName;
    ArkFxEffectHost _arkFxHost;
    bool _started;

    private void Awake()
    {
        // includeInactive=true：AB 特效里大量粒子节点初始未激活（由 FxDelay / 发射器点亮），
        // 只取 active 的会在回收时漏停这些粒子，导致对象池复用残留上一轮画面。
        PS = GetComponentsInChildren<ParticleSystem>(true);
        TR = GetComponentsInChildren<TrailRenderer>(true);
        _arkFxHost = GetComponent<ArkFxEffectHost>();
        // 切到 FxOverlay 层：由 SpineOverlayCamera 一并渲染，与 Spine 按深度排序（层未创建时内部自动跳过）
        SpineOverlayCameraUtil.ApplyFx(gameObject);
    }

    /// <summary>
    /// 回收前重置特效状态，避免对象池复用后残留上一轮数据。
    /// </summary>
    public void ResetEffect()
    {
        StopAllCoroutines();
        // 通知 ArkFx 生命周期宿主：回池了，下次 Play 要重新走一遍 OnPlay
        if (_arkFxHost == null) _arkFxHost = GetComponent<ArkFxEffectHost>();
        if (_arkFxHost != null) _arkFxHost.Recycle();
        _started = false;
        IsHide = false;
        Parent = null;
        _bullet = null;
        _isBulletEffect = false;
        PlayerUnitModel = null;
        forward = false;

        if (BoneFollower != null)
        {
            DestroyImmediate(BoneFollower);
            BoneFollower = null;
        }

        if (PS != null)
        {
            foreach (var p in PS)
            {
                if (p != null)
                    p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (TR != null)
        {
            foreach (var t in TR)
            {
                if (t != null)
                    t.Clear();
            }
        }

        transform.SetParent(null);
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        // 非 ArkFx 的工程内特效沿用旧行为；ArkFx 的激活状态由宿主 + FxDelay 管理，
        // 这里绝不能强行 SetActive(true)，否则会打断延迟播放。
        if (_arkFxHost == null && !gameObject.activeInHierarchy && !IsHide)
        {
            gameObject.SetActive(true);
        }

        if (EffectData.ParentFollow == 1 && !_isBulletEffect)
        {
            if (Parent != null)
            {
                float scaleX = Parent.ScaleX;
                if (Mathf.Abs(transform.localScale.x - scaleX) > 0.0001f)
                    transform.localScale = new Vector3(scaleX, 1, 1);
            }
        }

        if (EffectData.ParentFollow == 3 && !_isBulletEffect)
        {
            if (Parent != null)
                transform.position = Parent.Position + EffectData.Offset;
        }

        if (_isBulletEffect)
        {
            if (_bullet == null)
            {
                EffectManager.Instance.ReturnEffect(this);
                return;
            }
            transform.position = _bullet.Position + EffectData.Offset;
        }

        LifeTime -= Time.deltaTime;
        if (LifeTime < 0)
        {
            EffectManager.Instance.ReturnEffect(this);
            return;
        }

        if (BoneFollower != null)
        {
            if (PlayerUnitModel != null && forward != PlayerUnitModel.Forward)
                updateBoneFollow();
        }
    }

    public void SetLifeTime(float time)
    {
        LifeTime = time;
    }

    public void Play()
    {
        // AB 来源：首次 Play 才启动 ArkFx 宿主。此时调用方通常已完成 Init() 的定位/挂点/朝向。
        // Kick 内部会统一执行 OnPlay、播放粒子/Animator、挂缺失脚本兜底。
        if (_arkFxHost != null)
        {
            if (!_started)
            {
                _started = true;
                ArkFxLoader.Kick(gameObject, PrefabName);
            }

            // FxDelay 可能刚刚隐藏了根节点；不要在这里 SetActive(true) 抵消延迟。
            // 被隐藏时粒子会由 ArkFxDelay.Reveal 在延时结束后补播。
            ClearTrails();
            return;
        }

        // 工程内特效：保持原有激活 + 手动播放粒子的行为。
        if (!gameObject.activeInHierarchy && !IsHide)
        {
            gameObject.SetActive(true);
        }

        foreach (var p in PS)
        {
            if (p != null)
                p.Play();
        }
        ClearTrails();
    }

    void ClearTrails()
    {
        if (TR == null) return;
        foreach (var t in TR)
        {
            if (t != null)
                t.Clear();
        }
    }

    void updateBoneFollow()
    {
        if (PlayerUnitModel == null || PlayerUnitModel.SkeletonAnimation == null) return;

        this.forward = PlayerUnitModel.Forward;

        if (transform.childCount == 0) return;

        if (!forward && EffectData.ForwardOnly)
        {
            transform.GetChild(0).gameObject.SetActive(false);
            return;
        }
        else
        {
            transform.GetChild(0).gameObject.SetActive(true);
        }

        BoneFollower.boneName = (forward ? "F_" : "B_") + EffectData.BindPoint;
        if (PlayerUnitModel.SkeletonAnimation.skeleton.FindBone(BoneFollower.boneName) == null)
        {
            BoneFollower.boneName = EffectData.BindPoint;
        }

        var sr = PlayerUnitModel.SkeletonAnimation.GetComponent<SkeletonRenderer>();
        if (sr == null) return;

        BoneFollower.SkeletonRenderer = sr;
        transform.SetParent(null);
        transform.localScale = new Vector3(1, 1, 1);
        transform.SetParent(sr.transform);
    }

    public virtual void Init(Unit user, Unit target, Vector3 basePos, Vector3 direction, float speed = 1)
    {
        foreach (var p in PS)
        {
            if (p != null)
            {
                var m = p.main;
                m.simulationSpeed = speed;
            }
        }

        if (EffectData.ParentFollow == 2)
            this.Parent = user;
        else
            this.Parent = target;

        Vector3 bonePos = Vector3.zero;
        if (Parent != null && Parent.UnitModel != null)
        {
            bonePos = Parent.UnitModel.transform.position;
            if (!string.IsNullOrEmpty(EffectData.BindPoint))
            {
                basePos = Parent.UnitModel.GetPoint(EffectData.BindPoint);
            }
        }

        if (EffectData.ParentFollow == 1 && Parent != null && Parent.UnitModel != null)
        {
            transform.parent = Parent.UnitModel.transform;
        }

        if (EffectData.BoneFollow)
        {
            PlayerUnitModel = Parent != null ? Parent.UnitModel as PlayerUnitModel : null;
            if (PlayerUnitModel != null)
            {
                if (BoneFollower == null)
                    BoneFollower = gameObject.AddComponent<BoneFollower>();
                updateBoneFollow();
            }
        }

        if (EffectData.StartPos == 0)
        {
            transform.position = basePos + EffectData.Offset;
        }
        else
        {
            transform.position = bonePos + EffectData.Offset;
        }

        if (EffectData.ScaleXFollow == 1 && target != null)
        {
            transform.localScale = new Vector3(target.ScaleX, 1, 1);
        }
        else if (EffectData.ScaleXFollow == 2 && user != null)
        {
            transform.localScale = new Vector3(user.ScaleX, 1, 1);
        }

        float angleX = EffectData.FaceCamera;
        float angleY = 0;
        float angleZ = 0;
        if (EffectData.ForwordDirection == 1)
        {
            var pm = Parent != null ? Parent.UnitModel as PlayerUnitModel : null;
            if (pm != null && !pm.Forward)
            {
                angleZ = Parent.ScaleX * 90;
            }
        }
        if (EffectData.ForwordDirection == 2 && user != null)
        {
            angleY = Vector2.SignedAngle(user.Direction, Vector2.right);
        }
        transform.eulerAngles = new Vector3(angleX, angleY, angleZ);

        // 位置/父节点/缩放/朝向全部确定后再启动；ArkFx 发射器和 FxDelay 都依赖此时机。
        Play();
    }

    public virtual void Init(Bullet target, float speed = 1)
    {
        foreach (var p in PS)
        {
            if (p != null)
            {
                var m = p.main;
                m.simulationSpeed = speed;
            }
        }

        transform.position = target.Position;
        _isBulletEffect = true;
        _bullet = target;

        // 先定位再启动，保证弹道特效的发射器在正确位置生成。
        Play();
    }
}
