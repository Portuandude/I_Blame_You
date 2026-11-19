using System;
using System.Collections.Generic;

namespace IBlameYou.Net
{
    // TCP 같은 흐름(stream) 위에서 메시지 경계를 지킨다: [길이 4바이트][내용]. 순수 C#.
    public sealed class FrameAssembler
    {
        public const int MaxFrameBytes = 32 * 1024 * 1024; // 그림 하나가 커도 이 안 (넘으면 연결이 깨진 것으로 본다)

        private byte[] buffer = new byte[64 * 1024];
        private int count;

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
                if (size < 0 || size > MaxFrameBytes) throw new FormatException("메시지 길이가 이상하다: " + size);
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
        }
    }
}
