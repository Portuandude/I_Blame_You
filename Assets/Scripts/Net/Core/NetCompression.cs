using System.IO;
using System.IO.Compression;

namespace IBlameYou.Net
{
    // 큰 메시지(HUD 글자, 그림 조각 목록)를 줄여 보낸다: [Compressed][원래 메시지를 deflate한 바이트].
    public static class NetCompression
    {
        public const int Threshold = 1024; // 이보다 작으면 줄이는 값이 별로 없다
        // 풀었을 때 이보다 크면 깨진(또는 일부러 만든 "압축 폭탄") 메시지로 본다 (버그 점검 2026-11-22: 수십 KB가 수 GB로 풀려 멈춤)
        public const int MaxUnwrappedBytes = FrameAssembler.MaxFrameBytes * 2;

        public static byte[] MaybeCompress(byte[] message)
        {
            if (message == null || message.Length < Threshold) return message;
            using (var output = new MemoryStream(message.Length / 2 + 16))
            {
                output.WriteByte((byte)MsgType.Compressed);
                using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
                    deflate.Write(message, 0, message.Length);
                return output.Length < message.Length ? output.ToArray() : message;
            }
        }

        // 첫 바이트가 Compressed이면 풀어서, 아니면 그대로
        public static byte[] Unwrap(byte[] message) => Unwrap(message, MaxUnwrappedBytes);

        public static byte[] Unwrap(byte[] message, int maxBytes)
        {
            if (message == null || message.Length == 0 || message[0] != (byte)MsgType.Compressed) return message;
            using (var input = new MemoryStream(message, 1, message.Length - 1))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(message.Length * 4))
            {
                var chunk = new byte[16 * 1024];
                int read;
                while ((read = deflate.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (output.Length + read > maxBytes) throw new System.FormatException("풀린 메시지가 너무 크다");
                    output.Write(chunk, 0, read);
                }
                return output.ToArray();
            }
        }
    }
}
