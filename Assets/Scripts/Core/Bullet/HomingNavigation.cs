using UnityEngine;

/// <summary>
/// 追踪子弹轨迹计算工具：使用比例导引法（Proportional Navigation）。
///
/// 目标统一使用 Unit 基类，因此既可以追踪 Units.敌人，也兼容 Units.干员、
/// Units.中立单位、Units.普通单位等所有 Unit 子类。
///
/// 每颗子弹的比例导引状态（目标速度估计、子弹速度等）缓存在 BulletManager 中，
/// 避免在 Bullet 基类中增加字段，也避免每帧重复分配临时对象。
/// </summary>
public static class HomingNavigation
{
    public const float DefaultNavigationConstant = 4f;

    /// <summary>
    /// 获取当前要追踪的点。会优先使用 Bullet.GetTargetPos（命中点/模型点），
    /// 目标存活但模型暂缺时回退到 Unit.Position，保证各类 Unit 都能兼容。
    /// </summary>
    public static Vector3 GetTargetPosition(Bullet bullet, Unit target)
    {
        if (target == null)
            return bullet != null ? bullet.TargetPos : Vector3.zero;

        if (target.Alive() && target.UnitModel != null && bullet != null)
            return bullet.GetTargetPos(target);

        return target.Position;
    }

    /// <summary>
    /// 使用默认导航常数计算子弹下一帧位置。
    /// 速度取 BulletData.Speed * Bullet.Speed（与现有子弹系统的实际移速一致）。
    /// </summary>
    public static Vector3 GetNextPosition(Bullet bullet, float deltaTime)
    {
        return GetNextPosition(bullet, deltaTime, DefaultNavigationConstant, 0f);
    }

    /// <summary>
    /// 计算子弹下一帧位置。
    /// </summary>
    /// <param name="bullet">当前子弹</param>
    /// <param name="deltaTime">帧时间</param>
    /// <param name="navigationConstant">比例导引常数，通常 3~5，默认 4</param>
    /// <param name="maxTurnRateDegrees">最大转向速率（度/秒），0 表示不限制</param>
    public static Vector3 GetNextPosition(Bullet bullet, float deltaTime, float navigationConstant, float maxTurnRateDegrees)
    {
        if (bullet == null)
            return Vector3.zero;

        Unit target = bullet.Target;
        float speed = bullet.BulletData.Speed * bullet.Speed;

        // 目标已消失时按原方向继续飞行，由具体子弹类型决定是否结束/命中地面。
        if (target == null || !target.Alive())
        {
            Vector3 forward = bullet.Direction.sqrMagnitude > 0.0001f
                ? bullet.Direction.normalized
                : Vector3.zero;
            return bullet.Position + forward * speed * deltaTime;
        }

        Vector3 targetPos = GetTargetPosition(bullet, target);
        if (targetPos.Equals(bullet.Position))
            return bullet.Position;

        HomingTrackingData data = BulletManager.Instance.GetOrCreateHomingData(bullet);
        if (data.Target != target)
        {
            data.Reset();
            data.Target = target;
        }

        // ---- 目标速度估计（通过历史位置差分得到）----
        if (data.HasTargetPosition)
        {
            Vector3 newTargetVelocity = (targetPos - data.LastTargetPosition) / Mathf.Max(deltaTime, 1e-4f);
            data.TargetVelocity = data.HasTargetVelocity
                ? Vector3.Lerp(data.TargetVelocity, newTargetVelocity, 0.5f)
                : newTargetVelocity;
            data.HasTargetVelocity = true;
        }
        data.LastTargetPosition = targetPos;
        data.HasTargetPosition = true;

        // ---- 子弹当前速度 ----
        Vector3 missileVelocity;
        if (data.HasBulletVelocity)
        {
            missileVelocity = data.BulletVelocity;
        }
        else
        {
            missileVelocity = bullet.Direction.sqrMagnitude > 0.0001f
                ? bullet.Direction.normalized * speed
                : (targetPos - bullet.Position).normalized * speed;
            data.HasBulletVelocity = true;
        }
        missileVelocity = missileVelocity.sqrMagnitude > 0.0001f
            ? missileVelocity.normalized * speed
            : (targetPos - bullet.Position).normalized * speed;

        // ---- 比例导引核心公式 ----
        Vector3 los = targetPos - bullet.Position;
        float distance = los.magnitude;
        if (distance < 0.0001f)
        {
            data.BulletVelocity = missileVelocity;
            bullet.Direction = missileVelocity.normalized;
            return bullet.Position;
        }

        Vector3 losDir = los / distance;
        Vector3 relativeVelocity = data.TargetVelocity - missileVelocity;
        Vector3 losRate = Vector3.Cross(losDir, relativeVelocity) / distance; // 视线角速度矢量
        float closingSpeed = -Vector3.Dot(los, relativeVelocity) / distance;   // 接近速度
        if (closingSpeed < 0f)
            closingSpeed = 0f;

        Vector3 acceleration = navigationConstant * closingSpeed * Vector3.Cross(losRate, losDir);
        Vector3 desiredVelocity = missileVelocity + acceleration * deltaTime;
        desiredVelocity = desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized * speed
            : losDir * speed;

        // 可选的最大转向角限制，避免轨迹过弯/抖动
        if (maxTurnRateDegrees > 0f && data.HasBulletVelocity)
        {
            float maxAngle = maxTurnRateDegrees * Mathf.Deg2Rad * deltaTime;
            Vector3 newDirection = Vector3.RotateTowards(
                missileVelocity.normalized,
                desiredVelocity.normalized,
                maxAngle,
                0f);
            desiredVelocity = newDirection * speed;
        }

        data.BulletVelocity = desiredVelocity;
        bullet.Direction = desiredVelocity.normalized;
        return bullet.Position + desiredVelocity * deltaTime;
    }
}

/// <summary>
/// 单颗追踪子弹的比例导引状态缓存。由 BulletManager 统一管理生命周期。
/// </summary>
public class HomingTrackingData
{
    public Unit Target;
    public Vector3 LastTargetPosition;
    public Vector3 TargetVelocity;
    public Vector3 BulletVelocity;
    public bool HasTargetPosition;
    public bool HasTargetVelocity;
    public bool HasBulletVelocity;

    public void Reset()
    {
        Target = null;
        LastTargetPosition = Vector3.zero;
        TargetVelocity = Vector3.zero;
        BulletVelocity = Vector3.zero;
        HasTargetPosition = false;
        HasTargetVelocity = false;
        HasBulletVelocity = false;
    }
}
