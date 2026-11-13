using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 배경음악과 효과음을 재생한다. 처음 쓰일 때 스스로 오브젝트를 만들고 씬이 바뀌어도 유지된다.
    // 사운드 파일은 Resources/Audio/Music/<이름>, Resources/Audio/SFX/<이름> 에서 읽는다 (Tools/AudioGen으로 생성).
    // 같은 이름의 파일(wav/ogg/mp3)로 바꾸면 소리만 교체된다. 파일이 없으면 조용히 넘어간다.
    public class GameAudio : MonoBehaviour
    {
        public static float MusicVolume = 0.45f;
        public static float SfxVolume = 0.5f;

        private const int SfxVoices = 12;
        private const float SameClipCooldown = 0.05f; // 같은 소리가 한꺼번에 겹쳐 터지는 것을 막는다
        private const float MusicFadeTime = 0.8f;
        private const float MusicCutTime = 0.1f; // SilenceMusic: 이만큼 만에 뚝 줄인다 (0이면 딸깍 소리가 난다)

        private static GameAudio instance;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicA;
        private AudioSource musicB;
        private AudioSource currentMusic;
        private string currentMusicName;
        private float musicPitch = 1f; // 지금 곡의 목표 빠르기 (곡을 바꾸면 1로 돌아간다)
        private float musicDuck = 1f; // SetMusicDuck
        private float silentUntil = -1f; // SilenceMusic: 이 실제 시각까지 모든 곡을 0으로
        private const float MusicPitchSpeed = 0.5f; // 초당 바뀌는 빠르기 (1 → 1.08이 0.16초)

        private static GameAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameAudio");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameAudio>();
                }

                return instance;
            }
        }

        // 배경음악을 바꾼다. 이미 같은 곡이 나오고 있으면 그대로 둔다.
        public static void PlayMusic(string name) => Instance.SwitchMusic(name);

        // 배경음악을 줄여서 끈다 (결과 징글 동안). 다음 PlayMusic이 다시 튼다.
        public static void StopMusic()
        {
            if (instance == null) return;
            instance.currentMusic = null;
            instance.currentMusicName = null;
        }

        // 지금 곡을 조금 빠르게/느리게 (예: 중간보스 짝이 쓰러져 남은 기사가 광폭화하면 1.08). 다음 곡으로 바뀌면 1로 돌아간다.
        public static void SetMusicPitch(float pitch)
        {
            if (instance == null) return;
            instance.musicPitch = Mathf.Clamp(pitch, 0.5f, 2f);
        }

        // 배경음악을 잠시 작게 (예: 보스 방 바로 앞 방에서 0.55 — 폭풍 전 고요). 1이면 원래 크기. 곡을 바꿔도 유지되므로 쓴 쪽이 1로 되돌린다.
        public static void SetMusicDuck(float scale)
        {
            if (instance == null && scale >= 1f) return; // 아직 소리를 안 냈으면 되돌릴 것도 없다 (종료 중 오브젝트를 새로 만들지 않게)
            Instance.musicDuck = Mathf.Clamp01(scale);
        }

        // 배경음악을 seconds초(실제 시간) 동안 뚝 끊는다 (예: 보스 격파 순간의 정적). 그사이 곡이 바뀌어도 끝나면 새 곡이 평소처럼 차오른다.
        // 크기 조절(SetMusicDuck)과 따로 움직이므로 다른 쪽이 정한 크기를 건드리지 않는다.
        public static void SilenceMusic(float seconds)
        {
            if (instance == null) return; // 아직 곡이 없으면 끊을 것도 없다
            instance.silentUntil = Mathf.Max(instance.silentUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }

        // 지금 배경음악 이름 (아직 아무것도 안 틀었거나 종료 중이면 null). 보스 곡에서 돌아갈 때 확인용.
        public static string CurrentMusic => instance != null ? instance.currentMusicName : null;

        // 효과음 한 번. pitchJitter만큼 음높이를 무작위로 흔들어 반복해도 덜 단조롭게 한다.
        public static void PlaySfx(string name, float volume = 1f, float pitchJitter = 0.06f) =>
            Instance.PlayOneShot(name, volume, pitchJitter, 0f);

        // 기본 음높이를 바꿔 한 번 (마을 NPC 말소리처럼 같은 소리를 목소리마다 다르게).
        public static void PlaySfxPitched(string name, float pitch, float volume = 1f, float pitchJitter = 0.03f) =>
            Instance.PlayOneShot(name, volume, pitchJitter, 0f, pitch);

        // delay초 뒤에 울리는 효과음 (오디오 시계 기준이라 일시정지·timeScale과 상관없다). 앞 소리에 이어 붙일 때.
        public static void PlaySfxDelayed(string name, float delay, float volume = 1f, float pitchJitter = 0f) =>
            Instance.PlayOneShot(name, volume, pitchJitter, Mathf.Max(0f, delay));

        private void Awake()
        {
            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++) voices[i] = CreateSource(false);
            musicA = CreateSource(true);
            musicB = CreateSource(true);

            // 메뉴 화면에는 아직 카메라가 없어서 소리를 들을 귀가 없다. 없을 때만 여기에 하나 둔다.
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D 게임이라 거리 감쇠 없이 그대로 들린다
            return source;
        }

        private AudioClip Load(string path)
        {
            if (clips.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);
            clips[path] = clip;
            return clip;
        }

        private void PlayOneShot(string name, float volume, float pitchJitter, float delay, float basePitch = 1f)
        {
            var clip = Load($"Audio/SFX/{name}");
            if (clip == null) return;

            if (lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < SameClipCooldown) return;
            lastPlayed[name] = Time.unscaledTime;

            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.Stop();
            source.clip = clip;
            source.volume = SfxVolume * volume;
            source.pitch = basePitch + Random.Range(-pitchJitter, pitchJitter);
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        private void SwitchMusic(string name)
        {
            if (currentMusicName == name) return;

            var clip = Load($"Audio/Music/{name}");
            currentMusicName = name;
            musicPitch = 1f; // 곡 파일이 없어도 이전 곡 빠르기는 원래대로
            if (clip == null) return;

            var next = currentMusic == musicA ? musicB : musicA;
            next.clip = clip;
            next.volume = 0f;
            next.pitch = 1f;
            musicPitch = 1f;
            next.Play();
            currentMusic = next;
        }

        private void Update()
        {
            // 지금 곡은 MusicVolume까지 올리고, 이전 곡은 줄여서 끈다.
            bool silent = Time.unscaledTime < silentUntil;
            float step = Time.unscaledDeltaTime / (silent ? MusicCutTime : MusicFadeTime) * Mathf.Max(MusicVolume, 0.1f); // 볼륨이 0이어도 이전 곡은 꺼지게
            Fade(musicA, step, silent);
            Fade(musicB, step, silent);
            if (currentMusic != null) currentMusic.pitch = Mathf.MoveTowards(currentMusic.pitch, musicPitch, Time.unscaledDeltaTime * MusicPitchSpeed);
        }

        private void Fade(AudioSource source, float step, bool silent)
        {
            if (!source.isPlaying) return;
            float target = source == currentMusic && !silent ? MusicVolume * musicDuck : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, step);
            if (source != currentMusic && source.volume <= 0f) source.Stop();
        }
    }
}
