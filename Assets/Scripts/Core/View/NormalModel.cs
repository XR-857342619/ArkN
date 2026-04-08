using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NormalModel : UnitModel
{
    Animator Animator;
    private MeshRenderer meshRenderer;
    private Material materialInstance;
    public GameObject Particices;
    public bool haspartic;
    private void Awake()
    {
        Animator = GetComponentInChildren<Animator>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer == null) return;
        // 创建材质实例（避免影响原材质）
        materialInstance = new Material(meshRenderer.material);
    }
    public override void Init(Unit unit)
    {
        this.Unit = unit;
        string Color = unit.UnitData.Ablititys.GetStr("Color");
        float Alpha = unit.UnitData.Ablititys.GetFloat("Alpha");
        //Debug.Log(Color + "," + Alpha);
        //gameObject.SetActive(false);
        Animator?.Play(Unit.AnimationName[0]);
        if (Color is not null)
        {
            meshRenderer.material = materialInstance;
            SetColorFromHex(Color);
            SetAlpha(Alpha);
        }
    }


    private void LateUpdate()
    {
        if (Unit == null) return;
        transform.position = Unit.Position;
        transform.localEulerAngles = new Vector3(0, Vector2.SignedAngle(Unit.Direction, Vector2.right), 0);
        if (Animator != null && Unit.AnimationName != null)
        {
            Animator.Play(Unit.AnimationName[0]);
            Animator.speed = Unit.AnimationSpeed;
        }
    }

    public override void BreakAnimation()
    {
        base.BreakAnimation();
        Animator?.Play(Unit.AnimationName[0], 0, 0);
    }

    public override float GetAnimationDuration(string animationName)
    {
        var result = Animator?.runtimeAnimatorController?.animationClips?.FirstOrDefault(x => x.name == animationName) ?? null;
        if (result == null) return 0;
        return result.length;
    }

    public override float GetSkillDelay(string[] animationName, string[] lastState, out float fullDuration, out float beginDuration)
    {
        var ani = Animator.runtimeAnimatorController.animationClips.FirstOrDefault(x => x.name == animationName[0]);
        fullDuration = ani.length;
        beginDuration = 0;
        if (haspartic)
        {
            Particices.GetComponent<ParticleSystem>().Play();
        }
        return fullDuration / 2;
        
    }

    /// <summary>
    /// 通过16进制颜色代码设置颜色（支持带#和不带#的格式，如"FF0000"或"#FF0000"）
    /// </summary>
    /// <param name="hexCode">16进制颜色代码（6位或8位，8位时最后两位控制透明度）</param>
    public void SetColorFromHex(string hexCode)
    {
        if (materialInstance == null || hexCode is null){
            //Debug.Log("材质实例为空");
            return;
        }

        // 尝试解析16进制颜色（支持6位RGB或8位RGBA）
        if (ColorUtility.TryParseHtmlString(hexCode, out Color newColor))
        {
            materialInstance.color = newColor;
        }
        else
        {
            Debug.LogError($"无效的16进制颜色代码：{hexCode}，请检查格式（如#FF0000或FF0000）");
        }
    }

    // 单独修改透明度
    public void SetAlpha(float alpha)
    {
        if (materialInstance == null || alpha == 0) {
            //Debug.Log("材质实例为空");
            return; 
        }
        Color current = materialInstance.color;
        materialInstance.color = new Color(current.r, current.g, current.b, alpha);
}
    void OnDestroy()
    {
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}