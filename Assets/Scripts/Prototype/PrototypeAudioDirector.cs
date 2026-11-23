using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PrototypeAudioDirector : MonoBehaviour
{
    private enum MusicTrack { None, Title, Mission, Combat, Ending }

    private static PrototypeAudioDirector instance;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource musicSource;
    private AudioSource uiSource;
    private MusicTrack currentTrack;
    private Coroutine musicFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateDirector()
    {
        EnsureInstance();
    }

    private static PrototypeAudioDirector EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject audioObject = new GameObject("Ashline Audio Director");
        instance = audioObject.AddComponent<PrototypeAudioDirector>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
    }

    public static void PlayTitleMusic() => EnsureInstance().SetMusic(MusicTrack.Title);
    public static void PlayMissionMusic() => EnsureInstance().SetMusic(MusicTrack.Mission);
    public static void PlayCombatMusic() => EnsureInstance().SetMusic(MusicTrack.Combat);
    public static void PlayEndingMusic() => EnsureInstance().SetMusic(MusicTrack.Ending);

    public static void PlayUiClick() => EnsureInstance().PlayUi("Audio/External/hover", 0.34f);
    public static void PlayUiConfirm() => EnsureInstance().PlayUi("Audio/KenneySciFi/Audio/computerNoise_003", 0.48f);
    public static void PlayUiCancel() => EnsureInstance().PlayUi("Audio/KenneySciFi/Audio/doorClose_001", 0.38f);
    public static void PlayTerminal() => EnsureInstance().PlayUi("Audio/KenneySciFi/Audio/computerNoise_002", 0.55f);
    public static void PlayRevive() => EnsureInstance().PlayUi("Audio/KenneySciFi/Audio/forceField_001", 0.58f);

    public static void PlayBurst(int stage)
    {
        PrototypeAudioDirector audio = EnsureInstance();
        if (stage == 0) audio.PlayUi("Audio/KenneySciFi/Audio/forceField_000", 0.62f);
        else if (stage == 1) audio.PlayUi("Audio/KenneySciFi/Audio/forceField_003", 0.62f);
        else audio.PlayUi("Audio/KenneySciFi/Audio/laserLarge_004", 0.8f);
    }

    public static void PlayFocusedFire(Vector3 position) => EnsureInstance().PlayWorld("Audio/KenneySciFi/Audio/laserLarge_002", position, 0.68f);
    public static void PlayReload(int slot)
    {
        string clip = slot == 0 ? "doorOpen_000" : slot == 1 ? "doorClose_000" : "doorOpen_001";
        EnsureInstance().PlayUi("Audio/KenneySciFi/Audio/" + clip, 0.3f);
    }

    public static void PlayEnemyAttack(bool heavy, Vector3 position)
    {
        EnsureInstance().PlayWorld(heavy ? "Audio/KenneySciFi/Audio/explosionCrunch_002"
            : "Audio/KenneySciFi/Audio/laserLarge_003", position, heavy ? 0.62f : 0.42f);
    }

    public static void PlayShot(Vector3 position, int slot)
    {
        string clip = slot == 0 ? "laserLarge_000" : slot == 1 ? "laserSmall_003" : "laserSmall_000";
        EnsureInstance().PlayWorld("Audio/KenneySciFi/Audio/" + clip, position,
            slot == 0 ? 0.56f : 0.36f);
    }

    public static void PlayImpact(Vector3 position, bool critical)
    {
        string clip = critical ? "explosionCrunch_000" : "impactMetal_001";
        EnsureInstance().PlayWorld("Audio/KenneySciFi/Audio/" + clip, position,
            critical ? 0.48f : 0.26f);
    }

    public static void PlayMissionClear()
    {
        EnsureInstance().PlayUi("Audio/External/victory", 0.68f);
    }

    private void SetMusic(MusicTrack track)
    {
        if (currentTrack == track) return;
        currentTrack = track;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeToTrack(track));
    }

    private IEnumerator FadeToTrack(MusicTrack track)
    {
        float startVolume = musicSource.volume;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / 0.35f);
            yield return null;
        }

        musicSource.Stop();
        string path = track == MusicTrack.Title ? "Audio/External/title"
            : track == MusicTrack.Mission ? "Audio/External/sector"
            : track == MusicTrack.Combat ? "Audio/External/urgent"
            : "Audio/External/transmission";
        musicSource.clip = LoadClip(path);
        if (musicSource.clip != null)
        {
            musicSource.loop = true;
            musicSource.Play();
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(0f, 0.2f, t / 0.8f);
                yield return null;
            }
            musicSource.volume = 0.2f;
        }
        musicFade = null;
    }

    private void PlayUi(string resourcePath, float volume)
    {
        AudioClip clip = LoadClip(resourcePath);
        if (clip != null) uiSource.PlayOneShot(clip, volume);
    }

    private void PlayWorld(string resourcePath, Vector3 position, float volume)
    {
        AudioClip clip = LoadClip(resourcePath);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static AudioClip LoadClip(string resourcePath)
    {
        if (!clips.TryGetValue(resourcePath, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(resourcePath);
            clips.Add(resourcePath, clip);
        }
        return clip;
    }
}
