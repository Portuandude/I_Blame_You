using System;
using System.Collections.Generic;

namespace IBlameYou.Net
{
    // TCP 같은 흐름(stream) 위에서 메시지 경계를 지킨다: [길이 4바이트][내용]. 순수 C#.
    public sealed class FrameAssembler
    {
        public const int MaxFrameBytes = 32 * 1024 * 1024; // 그림 하나가 커도 이 안 (넘으면 연결이 깨진 것으로 본다)
        // 호스트가 받는 메시지(Hello·Pong·P2 입력)는 작다: 낯선 접속이 32MB 길이를 적고 조금씩 흘려 메모리를 잡아 두지 못하게 (2026-11-22)
        public const int HostMaxFrameBytes = 256 * 1024;
        private const int InitialBuffer = 64 * 1024;
        private const int ShrinkAbove = 1024 * 1024;

        private readonly int maxFrame;
        private byte[] buffer = new byte[InitialBuffer];
        private int count;

        public FrameAssembler() : this(MaxFrameBytes) { }
        public FrameAssembler(int maxFrameBytes) { maxFrame = maxFrameBytes; }

        public static byte[] Frame(byte[] payload) => Frame(payload, 0, payload.Length);

        public static byte[] Frame(byte[] payload, int offset, int length)
        {
            var framed = new byte[length + 4];
            framed[0] = (byte)length;
            framed[1] = (byte)(length >> 8);
            framed[2] = (byte)(length >> 16);
            framed[3] = (byte)(length >> 24);
            Buffer.BlockCopy(payload, offset, framed, 4, length);
            return framed;
        }

        // 받은 바이트를 넣고, 다 모인 메시지들을 꺼낸다.
        public void Push(byte[] bytes, int offset, int length, List<byte[]> completed)
        {
            if (count + length > buffer.Length)
            {
                int size = buffer.Length;
                while (size < count + length) size *= 2;
                Array.Resize(ref buffer, size);
            }
            Buffer.BlockCopy(bytes, offset, buffer, count, length);
            count += length;

            int start = 0;
            while (count - start >= 4)
            {
                int size = buffer[start] | (buffer[start + 1] << 8) | (buffer[start + 2] << 16) | (buffer[start + 3] << 24);
                if (size < 0 || size > maxFrame) throw new FormatException("메시지 길이가 이상하다: " + size);
                if (count - start - 4 < size) break;
                var frame = new byte[size];
                Buffer.BlockCopy(buffer, start + 4, frame, 0, size);
                completed.Add(frame);
                start += 4 + size;
            }

            if (start > 0)
            {
                Buffer.BlockCopy(buffer, start, buffer, 0, count - start);
                count -= start;
            }
            // 큰 그림 하나 때문에 커진 버퍼는 다 비우면 줄인다 (한 번 커지면 계속 차지하던 것)
            if (buffer.Length > ShrinkAbove && count <= InitialBuffer)
            {
                var small = new byte[InitialBuffer];
                Buffer.BlockCopy(buffer, 0, small, 0, count);
                buffer = small;
            }
        }
    }
}
