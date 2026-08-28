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
    public string Color;
    public float Alpha;
    public float Size;
    private float _size;
    public string texturePath;

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
        Color = unit.UnitData.Ablititys.GetStr("Color");
        Alpha = unit.UnitData.Ablititys.GetFloat("Alpha");
        Size = unit.UnitData.Ablititys.GetFloat("Size", 1);
        _size = Size != 1 ? Size : (unit.UnitData.ModelScale == 0 ? 1 : unit.UnitData.ModelScale);
        texturePath = unit.UnitData.Ablititys.GetStr("TexturePath");
        //Debug.Log(Color + "," + Alpha);
        //gameObject.SetActive(false);
        int texturePropertyID = Shader.PropertyToID("_MainTex");

        if (!string.IsNullOrEmpty(texturePath) && meshRenderer != null && materialInstance != null)
        {
            // 先把材质实例挂到 Renderer 上，否则只设置材质纹理但未赋值给 meshRenderer 时不会生效
            meshRenderer.material = materialInstance;

            ExtextureLoader.Instance.LoadTexture2D(texturePath, texture =>
            {
                if (materialInstance == null)
                    return;

                materialInstance.SetTexture(texturePropertyID, texture);
            });
        }

        Animator?.Play(Unit.AnimationName[0]);
        if (Color is not null)
        {
            meshRenderer.material = materialInstance;
            SetColorFromHex(Color);
            SetAlpha(Alpha);
        }

        transform.localScale *= _size;
        // AlignHeight 统一由 Unit.CreateModel / 单位位置变更时调用
    }


    private void LateUpdate()
    {
        if (Unit == null) return;
        transform.position = Unit.Position + Vector3.up * Unit.Height + Unit.UnitData.ModelOffset;
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
