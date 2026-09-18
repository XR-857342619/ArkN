using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 音频通道类型，供分通道音量接口使用。
/// </summary>
public enum AudioChannel
{
    /// <summary>背景音乐。</summary>
    Background,
    /// <summary>对白 / 语音。</summary>
    Voice,
    /// <summary>音效。</summary>
    SoundEffect,
}

/// <summary>
/// 全局音频管理器（常驻）。
/// 音量模型：最终音量 = 主音量（GameData.Bgm）× 通道音量（GameData.BgmVolume / VoiceVolume / SoundEffectVolume）。
/// 资源通过 ResHelper 的引用计数异步接口加载，切换/播放结束后成对释放，避免主线程阻塞与句柄泄漏。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioSource Background, Dialogue, SoundEffect;

    // 当前持有的音频资源路径（仅字符串接口会赋值），用于切换时释放上一个引用。
    private string _currentBackgroundPath;
    private string _currentDialoguePath;

    // 请求序号：只让最后一次异步加载请求生效，避免快速切歌时旧请求覆盖新请求。
    private int _backgroundRequestId;
    private int _dialogueRequestId;

    // 预加载并长期持有的音效路径（如战斗音效），避免首次播放时异步加载卡顿。
    private readonly HashSet<string> _pinnedSfx = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 不销毁 gameObject：它与 Init 共用一个常驻物体
            Debug.LogWarning("[AudioManager] 检测到重复的 AudioManager 实例，已忽略。");
            return;
        }

        Instance = this;
        Background.loop = true;
        RefreshVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        ReleaseOwned(ref _currentBackgroundPath);
        ReleaseOwned(ref _currentDialoguePath);
        ReleasePreloadedSoundEffects();
    }

    // ========================= 音量（主音量 + 分通道）=========================

    /// <summary>主音量（0~1），对应设置面板的 m_mainVolume 滑条（GameData.Bgm）。</summary>
    public float MasterVolume => Mathf.Clamp01(GameData.Instance.Bgm);

    /// <summary>读取某个通道的独立音量（0~1）。</summary>
    public float GetChannelVolume(AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Background: return Mathf.Clamp01(GameData.Instance.BgmVolume);
            case AudioChannel.Voice: return Mathf.Clamp01(GameData.Instance.VoiceVolume);
            case AudioChannel.SoundEffect: return Mathf.Clamp01(GameData.Instance.SoundEffectVolume);
            default: return 1f;
        }
    }

    /// <summary>设置主音量（0~1）并立即生效。设置面板 m_mainVolume 滑条调用。</summary>
    public void SetMasterVolume(float volume)
    {
        GameData.Instance.Bgm = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>设置某个通道的独立音量（0~1）并立即生效。设置面板 m_bgmVolume / m_SEVolume 滑条调用。</summary>
    public void SetChannelVolume(AudioChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);
        switch (channel)
        {
            case AudioChannel.Background: GameData.Instance.BgmVolume = volume; break;
            case AudioChannel.Voice: GameData.Instance.VoiceVolume = volume; break;
            case AudioChannel.SoundEffect: GameData.Instance.SoundEffectVolume = volume; break;
        }
        ApplyVolumes();
    }

    // 分通道便捷接口
    public void SetBackgroundVolume(float volume) => SetChannelVolume(AudioChannel.Background, volume);
    public void SetVoiceVolume(float volume) => SetChannelVolume(AudioChannel.Voice, volume);
    public void SetSoundEffectVolume(float volume) => SetChannelVolume(AudioChannel.SoundEffect, volume);

    /// <summary>从 GameData 重新读取音量并应用（例如存档加载完成后调用）。</summary>
    public void RefreshVolumes() => ApplyVolumes();

    /// <summary>最终音量 = 主音量 × 通道音量。</summary>
    private void ApplyVolumes()
    {
        float master = MasterVolume;
        if (Background != null) Background.volume = master * GetChannelVolume(AudioChannel.Background);
        if (Dialogue != null) Dialogue.volume = master * GetChannelVolume(AudioChannel.Voice);
        if (SoundEffect != null) SoundEffect.volume = master * GetChannelVolume(AudioChannel.SoundEffect);
    }

    // ========================= 背景音乐 =========================

    /// <summary>播放背景音乐（直接传入 AudioClip）。注意：此重载不做引用管理，调用方自行负责资源生命周期。</summary>
    public void PlayBackgroundAudio(AudioClip clip)
    {
        if (clip == null) { Debug.LogWarning("[AudioManager] BGM clip 为空"); return; }

        ReleaseOwned(ref _currentBackgroundPath);
        Background.clip = clip;
        ApplyVolumes();
        Background.Play();
    }

    /// <summary>按名称播放背景音乐（Assets/Bundles/Audio/&lt;clipName&gt;）。</summary>
    public void PlayBackgroundAudio(string clipName)
    {
        _ = PlayBackgroundAudioAsync(clipName);
    }

    /// <summary>按名称异步播放背景音乐，可 await 以确保加载/切换完成。</summary>
    public async Task PlayBackgroundAudioAsync(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) { Debug.LogWarning("[AudioManager] BGM 名称为空"); return; }

        int requestId = ++_backgroundRequestId;
        string path = PathHelper.AudioPath + clipName;

        // 同一首正在播放：只刷新音量，避免重复加载与从头重播
        if (_currentBackgroundPath == path && Background != null && Background.isPlaying)
        {
            ApplyVolumes();
            return;
        }

        AudioClip clip;
        try
        {
            clip = await ResHelper.GetAssetRefAsync<AudioClip>(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioManager] 加载 BGM 失败: {path}\n{e}");
            return;
        }

        // 已被更新的请求取代：归还本次引用
        if (requestId != _backgroundRequestId)
        {
            ResHelper.ReleaseAsset(path);
            return;
        }

        // 引用归位：路径相同则归还本次多占的引用；路径不同则释放上一首
        if (_currentBackgroundPath == path)
            ResHelper.ReleaseAsset(path);
        else
            ReleaseOwned(ref _currentBackgroundPath);
        _currentBackgroundPath = path;

        Background.clip = clip;
        ApplyVolumes();
        Background.Play();
    }

    // ========================= 对白 / 语音 =========================

    /// <summary>播放对白（直接传入 AudioClip）。注意：此重载不做引用管理。</summary>
    public void PlayDialogue(AudioClip clip)
    {
        if (clip == null) { Debug.LogWarning("[AudioManager] 对白 clip 为空"); return; }

        ReleaseOwned(ref _currentDialoguePath);
        Dialogue.clip = clip;
        ApplyVolumes();
        Dialogue.Play();
    }

    /// <summary>按名称播放对白（Assets/Bundles/Audio/&lt;clipName&gt;）。</summary>
    public void PlayDialogue(string clipName)
    {
        _ = PlayDialogueAsync(clipName);
    }

    /// <summary>按名称异步播放对白，可 await 以确保加载/切换完成。</summary>
    public async Task PlayDialogueAsync(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) { Debug.LogWarning("[AudioManager] 对白名称为空"); return; }

        int requestId = ++_dialogueRequestId;
        string path = PathHelper.AudioPath + clipName;

        if (_currentDialoguePath == path && Dialogue != null && Dialogue.isPlaying)
        {
            ApplyVolumes();
            return;
        }

        AudioClip clip;
        try
        {
            clip = await ResHelper.GetAssetRefAsync<AudioClip>(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioManager] 加载对白失败: {path}\n{e}");
            return;
        }

        if (requestId != _dialogueRequestId)
        {
            ResHelper.ReleaseAsset(path);
            return;
        }

        if (_currentDialoguePath == path)
            ResHelper.ReleaseAsset(path);
        else
            ReleaseOwned(ref _currentDialoguePath);
        _currentDialoguePath = path;

        Dialogue.clip = clip;
        ApplyVolumes();
        Dialogue.Play();
    }

    // 兼容旧拼写
    [Obsolete("拼写错误，请使用 PlayDialogue")]
    public void PlayDialougue(AudioClip clip) => PlayDialogue(clip);

    [Obsolete("拼写错误，请使用 PlayDialogue")]
    public void PlayDialougue(string clipName) => PlayDialogue(clipName);

    // ========================= 音效 =========================

    /// <summary>播放音效（直接传入 AudioClip）。注意：此重载不做引用管理。</summary>
    public void PlaySoundEffectAudio(AudioClip clip)
    {
        if (clip == null) return;
        ApplyVolumes();
        SoundEffect.PlayOneShot(clip);
    }

    /// <summary>按名称播放音效（Assets/Bundles/Audio/&lt;clipName&gt;）。</summary>
    public void PlaySoundEffectAudio(string clipName)
    {
        _ = PlaySoundEffectAudioAsync(clipName);
    }

    /// <summary>按名称异步播放音效，可 await 以确保资源加载完成（播放本身仍是一次的）。</summary>
    public async Task PlaySoundEffectAudioAsync(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) { Debug.LogWarning("[AudioManager] 音效名称为空"); return; }

        string path = PathHelper.AudioPath + clipName;
        AudioClip clip;
        try
        {
            clip = await ResHelper.GetAssetRefAsync<AudioClip>(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioManager] 加载音效失败: {path}\n{e}");
            return;
        }

        ApplyVolumes();
        SoundEffect.PlayOneShot(clip);

        // 音效是一次性的：播完后归还本次引用，避免反复播放导致引用计数不断累积
        float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(SoundEffect.pitch));
        StartCoroutine(ReleaseAfter(path, duration));
    }

    private IEnumerator ReleaseAfter(string path, float delay)
    {
        // 多留一点余量，确保 PlayOneShot 确实播完再释放句柄
        yield return new WaitForSecondsRealtime(delay + 0.1f);
        ResHelper.ReleaseAsset(path);
    }

    /// <summary>
    /// 预加载音效并长期持有引用，避免战斗中首次播放时异步加载造成卡顿。
    /// 使用完毕后必须调用 <see cref="ReleasePreloadedSoundEffects"/> 释放，否则引用不会归还。
    /// </summary>
    public async Task PreloadSoundEffects(params string[] clipNames)
    {
        if (clipNames == null) return;

        foreach (var clipName in clipNames)
        {
            if (string.IsNullOrEmpty(clipName)) continue;

            string path = PathHelper.AudioPath + clipName;
            if (!_pinnedSfx.Add(path)) continue; // 已经预加载过，不重复持有

            try
            {
                await ResHelper.GetAssetRefAsync<AudioClip>(path);
            }
            catch (Exception e)
            {
                _pinnedSfx.Remove(path);
                Debug.LogError($"[AudioManager] 预加载音效失败: {path}\n{e}");
            }
        }
    }

    /// <summary>释放所有预加载音效的引用（战斗/关卡结束时调用）。</summary>
    public void ReleasePreloadedSoundEffects()
    {
        if (_pinnedSfx.Count == 0) return;

        foreach (var path in _pinnedSfx)
            ResHelper.ReleaseAsset(path);
        _pinnedSfx.Clear();
    }

    // ========================= 停止 / 工具 =========================

    public void StopBackground()
    {
        if (Background != null) Background.Stop();
        ReleaseOwned(ref _currentBackgroundPath);
    }

    public void StopDialogue()
    {
        if (Dialogue != null) Dialogue.Stop();
        ReleaseOwned(ref _currentDialoguePath);
    }

    public void StopAll()
    {
        StopBackground();
        StopDialogue();
        if (SoundEffect != null) SoundEffect.Stop();
    }

    private void ReleaseOwned(ref string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        ResHelper.ReleaseAsset(path);
        path = null;
    }
}
