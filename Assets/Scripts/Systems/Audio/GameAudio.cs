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
        public static float SfxVolume = 0.8f;

        private const int SfxVoices = 12;
        private const float SameClipCooldown = 0.05f; // 같은 소리가 한꺼번에 겹쳐 터지는 것을 막는다
        private const float MusicFadeTime = 0.8f;

        private static GameAudio instance;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicA;
        private AudioSource musicB;
        private AudioSource currentMusic;
        private string currentMusicName;

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

        // 효과음 한 번. pitchJitter만큼 음높이를 무작위로 흔들어 반복해도 덜 단조롭게 한다.
        public static void PlaySfx(string name, float volume = 1f, float pitchJitter = 0.06f) =>
            Instance.PlayOneShot(name, volume, pitchJitter);

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

        private void PlayOneShot(string name, float volume, float pitchJitter)
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
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        private void SwitchMusic(string name)
        {
            if (currentMusicName == name) return;

            var clip = Load($"Audio/Music/{name}");
            currentMusicName = name;
            if (clip == null) return;

            var next = currentMusic == musicA ? musicB : musicA;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            currentMusic = next;
        }

        private void Update()
        {
            // 지금 곡은 MusicVolume까지 올리고, 이전 곡은 줄여서 끈다.
            float step = Time.unscaledDeltaTime / MusicFadeTime * MusicVolume;
            Fade(musicA, step);
            Fade(musicB, step);
        }

        private void Fade(AudioSource source, float step)
        {
            if (!source.isPlaying) return;
            float target = source == currentMusic ? MusicVolume : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, step);
            if (source != currentMusic && source.volume <= 0f) source.Stop();
        }
    }
}
