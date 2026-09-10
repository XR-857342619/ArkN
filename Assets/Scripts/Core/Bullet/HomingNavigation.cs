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
    private const float DefaultMaxPitchRate = 3600f;

    /// <summary>
    /// 粗略计算当前速度/转向速率下的最小转弯半径。
    /// 角速度单位：度/秒；速度单位：世界单位/秒。
    /// </summary>
    public static float CalculateMinTurnRadius(float speed, float maxTurnRateDegrees)
    {
        if (speed <= 0f || maxTurnRateDegrees <= 0f)
            return 0f;

        return speed / (maxTurnRateDegrees * Mathf.Deg2Rad) + 0.1f;
    }

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
    /// <param name="minTurnRadius">最小转弯半径；小于该距离时退化为纯追踪，0 表示不启用</param>
    public static Vector3 GetNextPosition(Bullet bullet, float deltaTime, float navigationConstant, float maxTurnRateDegrees, float minTurnRadius = 0f)
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

        // 若当前没有“接近”目标（目标在侧面/后方/远离中），
        // 纯比例导引可能不产生转向力，导致命中后重选目标时继续直飞。
        // 此时退化为向目标当前位置的纯追踪方向，确保能重新转向索敌到的目标。
        Vector3 desiredVelocity;

        // 近距离时比例导引容易形成环绕，直接退化为纯追踪，优先尝试命中。
        bool forcePurePursuit = minTurnRadius > 0f && distance < minTurnRadius;

        // 换目标后的第一帧还没有有效目标速度，TargetVelocity 仍为 zero。
        // 此时直接走纯追踪方向，避免用零目标速度跑比例导引产生异常转向；
        // 随后仍然会经过 LimitRotation 做水平/垂直转向限制。
        if (!data.HasTargetVelocity || forcePurePursuit)
        {
            desiredVelocity = losDir * speed;
        }
        else if (closingSpeed > 0f)
        {
            Vector3 acceleration = navigationConstant * closingSpeed * Vector3.Cross(losRate, losDir);
            desiredVelocity = missileVelocity + acceleration * deltaTime;
            desiredVelocity = desiredVelocity.sqrMagnitude > 0.0001f
                ? desiredVelocity.normalized * speed
                : losDir * speed;
        }
        else
        {
            desiredVelocity = losDir * speed;
        }

        // 水平转向与垂直俯仰分开限制：
        // - 水平方向继续使用 maxTurnRateDegrees 控制；
        // - 垂直方向固定使用 3600°/s，避免在 Y 轴方向积累过多偏移，同时不瞬间突变。
        if (data.HasBulletVelocity)
        {
            float maxYawAngle = maxTurnRateDegrees > 0f
                ? maxTurnRateDegrees * Mathf.Deg2Rad * deltaTime
                : float.MaxValue;
            float maxPitchAngle = DefaultMaxPitchRate * Mathf.Deg2Rad * deltaTime;

            Vector3 newDirection = LimitRotation(
                missileVelocity.normalized,
                desiredVelocity.normalized,
                maxYawAngle,
                maxPitchAngle);
            desiredVelocity = newDirection * speed;
        }

        data.BulletVelocity = desiredVelocity;
        bullet.Direction = desiredVelocity.normalized;
        return bullet.Position + desiredVelocity * deltaTime;
    }

    private static Vector3 LimitRotation(Vector3 current, Vector3 desired, float maxYawAngle, float maxPitchAngle)
    {
        if (current.sqrMagnitude < 0.0001f || desired.sqrMagnitude < 0.0001f)
            return desired.normalized;

        current.Normalize();
        desired.Normalize();

        float curYaw = Mathf.Atan2(current.z, current.x);
        float desYaw = Mathf.Atan2(desired.z, desired.x);
        float yawDiff = Mathf.DeltaAngle(curYaw * Mathf.Rad2Deg, desYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        yawDiff = Mathf.Clamp(yawDiff, -maxYawAngle, maxYawAngle);
        float newYaw = curYaw + yawDiff;

        float curPitch = Mathf.Asin(Mathf.Clamp(current.y, -1f, 1f));
        float desPitch = Mathf.Asin(Mathf.Clamp(desired.y, -1f, 1f));
        float pitchDiff = Mathf.Clamp(desPitch - curPitch, -maxPitchAngle, maxPitchAngle);
        float newPitch = curPitch + pitchDiff;

        return new Vector3(
            Mathf.Cos(newPitch) * Mathf.Cos(newYaw),
            Mathf.Sin(newPitch),
            Mathf.Cos(newPitch) * Mathf.Sin(newYaw)
        ).normalized;
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
