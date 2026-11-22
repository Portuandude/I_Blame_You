using System;

namespace IBlameYou.Net
{
    // P2(친구)가 쓰는 키. 마우스 말고는 숫자줄(공격 스킬)·Space(스토리 넘기기)·Esc(취소)뿐이다.
    public enum P2Key
    {
        Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Alpha0,
        Space, Escape, Return,
    }

    // 클라이언트가 매 프레임 보내는 P2 입력 상태. 누름/뗌은 "지금까지 누른 횟수"로 보내서,
    // 두 메시지 사이에 짧게 톡 누른 것도 호스트가 놓치지 않는다 (P2InputTracker).
    public sealed class P2InputState
    {
        public const int ButtonCount = 3; // 왼쪽·오른쪽·가운데
        public static readonly int KeyCount = Enum.GetValues(typeof(P2Key)).Length;

        public uint Sequence;
        public float MouseX; // 0~1, 왼쪽 아래가 0 (Input.mousePosition을 화면 크기로 나눈 값)
        public float MouseY;
        public bool MouseInside = true; // 클라이언트 창 안에 커서가 있는지
        public readonly bool[] ButtonHeld = new bool[ButtonCount];
        public readonly ushort[] ButtonPresses = new ushort[ButtonCount];
        public readonly ushort[] ButtonReleases = new ushort[ButtonCount];
        public float WheelTotal; // 지금까지 굴린 휠 합 (위 +)
        public readonly bool[] KeyHeld = new bool[KeyCount];
        public readonly ushort[] KeyPresses = new ushort[KeyCount];

        public void CopyFrom(P2InputState other)
        {
            Sequence = other.Sequence;
            MouseX = other.MouseX;
            MouseY = other.MouseY;
            MouseInside = other.MouseInside;
            Array.Copy(other.ButtonHeld, ButtonHeld, ButtonCount);
            Array.Copy(other.ButtonPresses, ButtonPresses, ButtonCount);
            Array.Copy(other.ButtonReleases, ButtonReleases, ButtonCount);
            WheelTotal = other.WheelTotal;
            Array.Copy(other.KeyHeld, KeyHeld, KeyCount);
            Array.Copy(other.KeyPresses, KeyPresses, KeyCount);
        }

        public byte[] Write()
        {
            var w = new NetWriter(64);
            w.WriteByte((byte)MsgType.P2Input);
            w.WriteVarUInt(Sequence);
            w.WriteFloat(MouseX);
            w.WriteFloat(MouseY);
            byte flags = (byte)(MouseInside ? 1 : 0);
            for (int i = 0; i < ButtonCount; i++)
                if (ButtonHeld[i]) flags |= (byte)(2 << i);
            w.WriteByte(flags);
            for (int i = 0; i < ButtonCount; i++)
            {
                w.WriteUInt16(ButtonPresses[i]);
                w.WriteUInt16(ButtonReleases[i]);
            }
            w.WriteFloat(WheelTotal);
            uint keyBits = 0;
            for (int i = 0; i < KeyCount; i++)
                if (KeyHeld[i]) keyBits |= 1u << i;
            w.WriteVarUInt(keyBits);
            for (int i = 0; i < KeyCount; i++) w.WriteUInt16(KeyPresses[i]);
            return w.ToArray();
        }

        // 이상한 값이면 앞의 값을 그대로 쓴다
        private static float Finite(float value, float previous) =>
            float.IsNaN(value) || float.IsInfinity(value) ? (float.IsNaN(previous) || float.IsInfinity(previous) ? 0f : previous) : value;

        // 첫 바이트(MsgType)는 이미 읽은 reader
        public void Read(NetReader r)
        {
            Sequence = r.ReadVarUInt();
            // 깨졌거나 일부러 만든 NaN·무한대가 조준·카메라 계산으로 번지지 않게 (버그 점검 2026-11-22)
            MouseX = Finite(r.ReadFloat(), MouseX);
            MouseY = Finite(r.ReadFloat(), MouseY);
            byte flags = r.ReadByte();
            MouseInside = (flags & 1) != 0;
            for (int i = 0; i < ButtonCount; i++) ButtonHeld[i] = (flags & (2 << i)) != 0;
            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonPresses[i] = r.ReadUInt16();
                ButtonReleases[i] = r.ReadUInt16();
            }
            WheelTotal = Finite(r.ReadFloat(), WheelTotal);
            uint keyBits = r.ReadVarUInt();
            for (int i = 0; i < KeyCount; i++) KeyHeld[i] = (keyBits & (1u << i)) != 0;
            for (int i = 0; i < KeyCount; i++) KeyPresses[i] = r.ReadUInt16();
        }
    }

    // 호스트 쪽: 받은 P2 입력 상태를 호스트의 프레임 단위 "이번 프레임에 눌림/뗌"으로 바꾼다.
    // Step()을 호스트 프레임마다 한 번 (게임 코드가 읽기 전에) 부른다.
    public sealed class P2InputTracker
    {
        private readonly P2InputState latest = new P2InputState();
        private readonly ushort[] seenPresses = new ushort[P2InputState.ButtonCount];
        private readonly ushort[] seenReleases = new ushort[P2InputState.ButtonCount];
        private readonly ushort[] seenKeyPresses = new ushort[P2InputState.KeyCount];
        private float seenWheel;
        private bool hasState;
        private bool primed;

        public readonly bool[] ButtonDown = new bool[P2InputState.ButtonCount];
        public readonly bool[] ButtonUp = new bool[P2InputState.ButtonCount];
        public readonly bool[] KeyDown = new bool[P2InputState.KeyCount];
        public float WheelDelta { get; private set; }

        public bool HasState => hasState;
        public P2InputState Latest => latest;

        public void Reset()
        {
            hasState = false;
            primed = false;
            ClearEdges();
        }

        public void Receive(P2InputState state)
        {
            // TCP라 순서는 지켜지지만, 다른 전송(스팀 비신뢰)에 대비해 오래된 것은 버린다.
            if (hasState && (int)(state.Sequence - latest.Sequence) <= 0) return;
            latest.CopyFrom(state);
            hasState = true;
        }

        private void ClearEdges()
        {
            Array.Clear(ButtonDown, 0, ButtonDown.Length);
            Array.Clear(ButtonUp, 0, ButtonUp.Length);
            Array.Clear(KeyDown, 0, KeyDown.Length);
            WheelDelta = 0f;
        }

        public void Step()
        {
            ClearEdges();
            if (!hasState) return;
            if (!primed)
            {
                // 접속하자마자 예전 횟수 차이로 클릭이 튀어나오지 않게 지금 값을 기준으로 삼는다.
                Array.Copy(latest.ButtonPresses, seenPresses, seenPresses.Length);
                Array.Copy(latest.ButtonReleases, seenReleases, seenReleases.Length);
                Array.Copy(latest.KeyPresses, seenKeyPresses, seenKeyPresses.Length);
                seenWheel = latest.WheelTotal;
                primed = true;
                return;
            }

            for (int i = 0; i < P2InputState.ButtonCount; i++)
            {
                ButtonDown[i] = latest.ButtonPresses[i] != seenPresses[i];
                ButtonUp[i] = latest.ButtonReleases[i] != seenReleases[i];
                seenPresses[i] = latest.ButtonPresses[i];
                seenReleases[i] = latest.ButtonReleases[i];
            }
            for (int i = 0; i < P2InputState.KeyCount; i++)
            {
                KeyDown[i] = latest.KeyPresses[i] != seenKeyPresses[i];
                seenKeyPresses[i] = latest.KeyPresses[i];
            }
            WheelDelta = latest.WheelTotal - seenWheel;
            seenWheel = latest.WheelTotal;
        }

        public bool Held(int button) => hasState && latest.ButtonHeld[button];
        public bool KeyHeld(P2Key key) => hasState && latest.KeyHeld[(int)key];
    }
}
