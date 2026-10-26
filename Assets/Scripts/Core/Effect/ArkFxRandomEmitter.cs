// ============================================================
// ArkFxRandomEmitter - Torappu.Battle.Effects.RandomEmitter 的替身（P3）
//
// 原版：在 [_randomRangeFrom, _randomRangeTo] 的盒内随机取点，
//       每隔 _delayPerEffect 秒发射一个，共 _count 个。
// ============================================================
using System.Collections;
using UnityEngine;

public class ArkFxRandomEmitter : ArkFxEmitterBase
{
    [Tooltip("要发射的子特效名（原版 _effect）")]
    public string effect;

    [Tooltip("发射个数（原版 _count）")]
    public int count = 1;

    [Tooltip("每个之间的间隔秒数（原版 _delayPerEffect）")]
    public float delayPerEffect;

    [Tooltip("随机范围下限（原版 _randomRangeFrom）")]
    public Vector3 randomRangeFrom = new Vector3(-0.5f, -0.5f, -0.5f);

    [Tooltip("随机范围上限（原版 _randomRangeTo）")]
    public Vector3 randomRangeTo = new Vector3(0.5f, 0.5f, 0.5f);

    protected override void DoEmit()
    {
        StartCoroutine(EmitRoutine());
    }

    IEnumerator EmitRoutine()
    {
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
        {
            var p = transform.position + new Vector3(
                Random.Range(randomRangeFrom.x, randomRangeTo.x),
                Random.Range(randomRangeFrom.y, randomRangeTo.y),
                Random.Range(randomRangeFrom.z, randomRangeTo.z));
            Emit(effect, p, transform.rotation);

            if (delayPerEffect > 0f && i < n - 1)
                yield return new WaitForSeconds(delayPerEffect);
        }
    }
}
