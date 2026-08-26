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

    private void Awake()
    {
        PS = GetComponentsInChildren<ParticleSystem>();
        TR = GetComponentsInChildren<TrailRenderer>();
    }

    /// <summary>
    /// 回收前重置特效状态，避免对象池复用后残留上一轮数据。
    /// </summary>
    public void ResetEffect()
    {
        StopAllCoroutines();
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
        if (!gameObject.activeInHierarchy && !IsHide)
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
        if (!gameObject.activeInHierarchy && !IsHide)
        {
            gameObject.SetActive(true);
        }

        foreach (var p in PS)
        {
            if (p != null)
                p.Play();
        }
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

        Play();
        if (!gameObject.activeInHierarchy && !IsHide)
        {
            gameObject.SetActive(true);
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

        Play();
        if (!gameObject.activeInHierarchy && !IsHide)
        {
            gameObject.SetActive(true);
        }

        transform.position = target.Position;
        _isBulletEffect = true;
        _bullet = target;
    }
}
