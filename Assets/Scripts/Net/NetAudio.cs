using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Net
{
    public enum AudioOp : byte { Sfx = 1, Music = 2, StopMusic = 3, MusicPitch = 4, MusicDuck = 5, Silence = 6, Muffle = 7 }

    // 소리는 이름으로 옮긴다: 두 컴퓨터가 같은 빌드라 Resources/Audio의 같은 파일을 갖고 있다.
    // 호스트: GameAudio가 소리를 낼 때마다 여기에 적고(Record*), NetHostStreamer가 프레임마다 한 메시지로 보낸다.
    // 친구: 받은 사건을 자기 GameAudio로 그대로 낸다 (크기는 친구 자신의 설정).
    public static class NetAudio
    {
        private static readonly NetWriter pending = new NetWriter(256);
        private static int pendingCount;
        private static bool replaying; // 친구 쪽에서 GameAudio를 부르는 동안 다시 적지 않게

        private static bool Recording => !replaying && NetSession.RemoteP2Active;

        public static void RecordSfx(string name, float volume, float pitchJitter, float delay, float basePitch)
        {
            if (!Recording) return;
            pending.WriteByte((byte)AudioOp.Sfx);
            pending.WriteString(name);
            pending.WriteFloat(volume);
            pending.WriteFloat(pitchJitter);
            pending.WriteFloat(delay);
            pending.WriteFloat(basePitch);
            pendingCount++;
        }

        public static void RecordMusic(string name)
        {
            if (!Recording) return;
            pending.WriteByte((byte)AudioOp.Music);
            pending.WriteString(name);
            pendingCount++;
        }

        public static void Record(AudioOp op, float value)
        {
            if (!Recording) return;
            pending.WriteByte((byte)op);
            pending.WriteFloat(value);
            pendingCount++;
        }

        public static void RecordStopMusic()
        {
            if (!Recording) return;
            pending.WriteByte((byte)AudioOp.StopMusic);
            pendingCount++;
        }

        public static void Clear()
        {
            pending.Clear();
            pendingCount = 0;
        }

        // 보낼 것이 있으면 Audio 메시지
        public static byte[] Take()
        {
            if (pendingCount == 0) return null;
            var w = new NetWriter(pending.Length + 8);
            w.WriteByte((byte)MsgType.Audio);
            w.WriteVarUInt((uint)pendingCount);
            w.WriteRaw(pending);
            Clear();
            return w.ToArray();
        }

        public static void Play(NetReader r)
        {
            int count = (int)r.ReadVarUInt();
            replaying = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var op = (AudioOp)r.ReadByte();
                    switch (op)
                    {
                        case AudioOp.Sfx:
                        {
                            string name = r.ReadString();
                            float volume = r.ReadFloat();
                            float jitter = r.ReadFloat();
                            float delay = r.ReadFloat();
                            float pitch = r.ReadFloat();
                            if (string.IsNullOrEmpty(name)) break;
                            if (delay > 0f) GameAudio.PlaySfxDelayed(name, delay, volume, jitter);
                            else if (!Mathf.Approximately(pitch, 1f)) GameAudio.PlaySfxPitched(name, pitch, volume, jitter);
                            else GameAudio.PlaySfx(name, volume, jitter);
                            break;
                        }
                        case AudioOp.Music:
                        {
                            string name = r.ReadString();
                            if (!string.IsNullOrEmpty(name)) GameAudio.PlayMusic(name);
                            break;
                        }
                        case AudioOp.StopMusic: GameAudio.StopMusic(); break;
                        case AudioOp.MusicPitch: GameAudio.SetMusicPitch(r.ReadFloat()); break;
                        case AudioOp.MusicDuck: GameAudio.SetMusicDuck(r.ReadFloat()); break;
                        case AudioOp.Silence: GameAudio.SilenceMusic(r.ReadFloat()); break;
                        case AudioOp.Muffle: GameAudio.SetMusicMuffled(r.ReadFloat() > 0.5f); break;
                        default: throw new System.FormatException("모르는 소리 사건 " + op);
                    }
                }
            }
            finally
            {
                replaying = false;
            }
        }
    }
}
