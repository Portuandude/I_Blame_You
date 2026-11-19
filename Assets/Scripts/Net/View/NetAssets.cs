using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Net
{
    // 호스트 쪽: 그림(텍스처)·스프라이트에 번호를 붙이고, 처음 쓰일 때 한 번만 친구에게 보낸다.
    // 게임은 많은 그림을 코드로 만든다(Sprite.Create)라서 친구 컴퓨터가 같은 그림을 스스로 찾을 수 없다. 그래서 PNG로 보낸다.
    // 번호 0 = 없음, 1 = 흰 텍스처 (Texture2D.whiteTexture, 보내지 않음).
    public sealed class NetAssetSender
    {
        public const int NoTexture = 0;
        public const int WhiteTexture = 1;

        private readonly System.Action<byte[]> send; // 메시지를 친구에게 보내는 길 (NetSession.Send, 테스트는 목록에 모으기)
        private readonly Dictionary<Texture, int> textureIds = new Dictionary<Texture, int>();
        private readonly Dictionary<Sprite, int> spriteIds = new Dictionary<Sprite, int>();
        private int nextTexture = 2;
        private int nextSprite = 1;

        public int SentTextureBytes { get; private set; }

        public NetAssetSender(System.Action<byte[]> send)
        {
            this.send = send;
        }

        public void Reset()
        {
            textureIds.Clear();
            spriteIds.Clear();
            nextTexture = 2;
            nextSprite = 1;
            SentTextureBytes = 0;
        }

        public int TextureId(Texture texture)
        {
            if (texture == null) return NoTexture;
            if (texture == Texture2D.whiteTexture) return WhiteTexture;
            if (textureIds.TryGetValue(texture, out int id)) return id;

            id = nextTexture++;
            textureIds[texture] = id;
            byte[] png = EncodePng(texture);
            var w = new NetWriter((png != null ? png.Length : 0) + 32);
            w.WriteByte((byte)MsgType.Texture);
            w.WriteVarUInt((uint)id);
            w.WriteVarUInt((uint)texture.width);
            w.WriteVarUInt((uint)texture.height);
            w.WriteByte((byte)texture.filterMode);
            w.WriteByte((byte)texture.wrapMode);
            w.WriteBytes(png);
            send(w.ToArray());
            SentTextureBytes += png != null ? png.Length : 0;
            return id;
        }

        public int SpriteId(Sprite sprite)
        {
            if (sprite == null) return 0;
            if (spriteIds.TryGetValue(sprite, out int id)) return id;

            int texture = TextureId(sprite.texture);
            id = nextSprite++;
            spriteIds[sprite] = id;
            var rect = sprite.rect;
            var w = new NetWriter(64);
            w.WriteByte((byte)MsgType.Sprite);
            w.WriteVarUInt((uint)id);
            w.WriteVarUInt((uint)texture);
            w.WriteFloat(rect.x);
            w.WriteFloat(rect.y);
            w.WriteFloat(rect.width);
            w.WriteFloat(rect.height);
            w.WriteFloat(sprite.pivot.x);
            w.WriteFloat(sprite.pivot.y);
            w.WriteFloat(sprite.pixelsPerUnit);
            var border = sprite.border;
            w.WriteFloat(border.x);
            w.WriteFloat(border.y);
            w.WriteFloat(border.z);
            w.WriteFloat(border.w);
            send(w.ToArray());
            return id;
        }

        // 읽기 가능한 그림은 바로, 아니면 GPU에서 복사해서 PNG로 만든다.
        private static byte[] EncodePng(Texture texture)
        {
            if (texture is Texture2D t2 && t2.isReadable)
            {
                try
                {
                    var bytes = t2.EncodeToPNG();
                    if (bytes != null) return bytes;
                }
                catch (System.Exception) { } // 압축 형식 등은 아래 길로
            }

            int width = Mathf.Max(1, texture.width);
            int height = Mathf.Max(1, texture.height);
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                return copy.EncodeToPNG();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetAssets] 그림 '{texture.name}'을 보내지 못함: {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null) NetUnity.Destroy(copy);
            }
        }
    }

    // 친구 쪽: 받은 그림·스프라이트를 번호로 찾는다.
    public sealed class NetAssetReceiver
    {
        private readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        public void Clear()
        {
            foreach (var s in sprites.Values) if (s != null) NetUnity.Destroy(s);
            foreach (var t in textures.Values) if (t != null) NetUnity.Destroy(t);
            sprites.Clear();
            textures.Clear();
        }

        public Texture Texture(int id)
        {
            if (id == NetAssetSender.NoTexture) return null;
            if (id == NetAssetSender.WhiteTexture) return Texture2D.whiteTexture;
            return textures.TryGetValue(id, out var t) ? t : null;
        }

        public Sprite Sprite(int id) => id != 0 && sprites.TryGetValue(id, out var s) ? s : null;

        public void ReadTexture(NetReader r)
        {
            int id = (int)r.ReadVarUInt();
            int width = (int)r.ReadVarUInt();
            int height = (int)r.ReadVarUInt();
            var filter = (FilterMode)r.ReadByte();
            var wrap = (TextureWrapMode)r.ReadByte();
            byte[] png = r.ReadBytes();
            var texture = new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height), TextureFormat.RGBA32, false);
            if (png != null) texture.LoadImage(png);
            texture.filterMode = filter;
            texture.wrapMode = wrap;
            if (textures.TryGetValue(id, out var old) && old != null) NetUnity.Destroy(old);
            textures[id] = texture;
        }

        public void ReadSprite(NetReader r)
        {
            int id = (int)r.ReadVarUInt();
            int textureId = (int)r.ReadVarUInt();
            var rect = new Rect(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            var pivot = new Vector2(r.ReadFloat(), r.ReadFloat());
            float ppu = r.ReadFloat();
            var border = new Vector4(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            var texture = Texture(textureId) as Texture2D;
            if (texture == null) return;
            // 스프라이트 영역이 그림 밖으로 나가면 Sprite.Create가 실패하므로 맞춘다 (보낸 PNG 크기가 다를 때)
            rect.width = Mathf.Min(rect.width, texture.width - rect.x);
            rect.height = Mathf.Min(rect.height, texture.height - rect.y);
            if (rect.width <= 0f || rect.height <= 0f) return;
            var normalizedPivot = new Vector2(pivot.x / rect.width, pivot.y / rect.height);
            var sprite = UnityEngine.Sprite.Create(texture, rect, normalizedPivot, ppu, 0, SpriteMeshType.FullRect, border);
            if (sprites.TryGetValue(id, out var old) && old != null) NetUnity.Destroy(old);
            sprites[id] = sprite;
        }
    }

    internal static class NetUnity
    {
        // 게임 중에는 Destroy, EditMode 테스트에서는 DestroyImmediate
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
