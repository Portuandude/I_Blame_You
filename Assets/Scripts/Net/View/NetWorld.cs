using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Net
{
    // 게임 세상의 "그림 조각"(SpriteRenderer·LineRenderer·TrailRenderer·TextMesh)과 카메라를 친구 화면에 옮긴다.
    // 이 게임은 파티클·타일맵·3D 모델 없이 이 넷으로만 그린다 (2026-11-17 조사). 새 종류를 쓰게 되면 여기에 더한다.
    // 호스트는 매 프레임 바뀐 조각만 보내고(지난번 보낸 바이트와 비교), 사라진 조각 번호를 보낸다.
    public enum WorldPieceKind : byte { Sprite = 1, Line = 2, Text = 3 }

    public sealed class NetWorldCapture
    {
        private readonly NetAssetSender assets;
        private readonly Dictionary<int, byte[]> lastSent = new Dictionary<int, byte[]>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private readonly NetWriter message = new NetWriter(16 * 1024);
        private readonly NetWriter piece = new NetWriter(256);
        private readonly NetWriter body = new NetWriter(8 * 1024); // 바뀐 조각 모음 (Capture마다 Clear)
        private Vector3[] points = new Vector3[64];
        private int changedCount;

        public NetWorldCapture(NetAssetSender assets)
        {
            this.assets = assets;
        }

        // 이 아래의 그림은 보내지 않는다 (EditMode 테스트에서 같은 씬에 만든 친구 화면 조각)
        public Transform Ignore;

        public void Reset() => lastSent.Clear();

        // 보낼 것이 있으면 World 메시지, 없으면 null (카메라는 늘 넣으므로 사실상 늘 보낸다)
        public byte[] Capture()
        {
            message.Clear();
            message.WriteByte((byte)MsgType.World);
            WriteCamera();

            seen.Clear();
            changedCount = 0;
            body.Clear(); // 프레임마다 새로 만들지 않고 다시 쓴다 (호스트 쓰레기 줄이기)

            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.sprite == null || Ignored(r.transform)) continue;
                piece.Clear();
                WriteSprite(r);
                Emit(r.GetInstanceID(), WorldPieceKind.Sprite, body);
            }
            foreach (var r in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.positionCount < 2 || Ignored(r.transform)) continue;
                piece.Clear();
                WriteLine(r);
                Emit(r.GetInstanceID(), WorldPieceKind.Line, body);
            }
            foreach (var r in Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.positionCount < 2 || Ignored(r.transform)) continue;
                piece.Clear();
                WriteTrail(r);
                Emit(r.GetInstanceID(), WorldPieceKind.Line, body);
            }
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || string.IsNullOrEmpty(t.text) || Ignored(t.transform)) continue;
                piece.Clear();
                WriteText(t, mr);
                Emit(t.GetInstanceID(), WorldPieceKind.Text, body);
            }

            removed.Clear();
            foreach (var id in lastSent.Keys)
                if (!seen.Contains(id)) removed.Add(id);
            foreach (var id in removed) lastSent.Remove(id);

            message.WriteVarUInt((uint)changedCount);
            message.WriteRaw(body);
            message.WriteVarUInt((uint)removed.Count);
            foreach (var id in removed) message.WriteVarInt(id);
            return message.ToArray();
        }

        private bool Ignored(Transform t) => Ignore != null && t.IsChildOf(Ignore);

        private void Emit(int id, WorldPieceKind kind, NetWriter body)
        {
            seen.Add(id);
            if (lastSent.TryGetValue(id, out var previous) && piece.SameAs(previous)) return;
            lastSent[id] = piece.ToArray(previous); // 길이가 같으면 지난 배열에 덮어쓴다 (움직이는 조각은 매 프레임 바뀐다)
            body.WriteVarInt(id);
            body.WriteByte((byte)kind);
            body.WriteRaw(piece);
            changedCount++;
        }

        private void WriteCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                message.WriteBool(false);
                return;
            }
            message.WriteBool(true);
            var p = cam.transform.position;
            message.WriteFloat(p.x);
            message.WriteFloat(p.y);
            message.WriteFloat(p.z);
            message.WriteFloat(cam.orthographicSize);
            WriteColor(message, cam.backgroundColor);
        }

        private static void WriteColor(NetWriter w, Color c)
        {
            Color32 c32 = c;
            w.WriteByte(c32.r);
            w.WriteByte(c32.g);
            w.WriteByte(c32.b);
            w.WriteByte(c32.a);
        }

        private void WriteTransform(Transform t)
        {
            var p = t.position;
            piece.WriteFloat(p.x);
            piece.WriteFloat(p.y);
            piece.WriteFloat(p.z);
            piece.WriteFloat(t.eulerAngles.z);
            var s = t.lossyScale;
            piece.WriteFloat(s.x);
            piece.WriteFloat(s.y);
        }

        private void WriteSorting(Renderer r)
        {
            piece.WriteVarInt(r.sortingLayerID);
            piece.WriteVarInt(r.sortingOrder);
        }

        private void WriteSprite(SpriteRenderer r)
        {
            piece.WriteVarUInt((uint)assets.SpriteId(r.sprite));
            WriteTransform(r.transform);
            var material = r.sharedMaterial;
            bool flash = material != null && material.HasProperty(FlashAmount);
            bool sized = r.drawMode != SpriteDrawMode.Simple;
            byte flags = 0;
            if (r.flipX) flags |= 1;
            if (r.flipY) flags |= 2;
            if (sized) flags |= 4;
            if (r.drawMode == SpriteDrawMode.Tiled) flags |= 8;
            if (flash) flags |= 16;
            piece.WriteByte(flags);
            WriteColor(piece, r.color);
            WriteSorting(r);
            if (sized)
            {
                piece.WriteFloat(r.size.x);
                piece.WriteFloat(r.size.y);
            }
            if (flash)
            {
                piece.WriteFloat(material.GetFloat(FlashAmount));
                WriteColor(piece, material.GetColor(FlashColor));
            }
        }

        private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        private void WritePoints(int count, bool worldSpace, Transform t)
        {
            piece.WriteVarUInt((uint)count);
            for (int i = 0; i < count; i++)
            {
                var p = worldSpace ? points[i] : t.TransformPoint(points[i]);
                piece.WriteFloat(p.x);
                piece.WriteFloat(p.y);
                piece.WriteFloat(p.z);
            }
        }

        private void EnsurePoints(int count)
        {
            if (points.Length < count) points = new Vector3[Mathf.NextPowerOfTwo(count)];
        }

        private void WriteLine(LineRenderer r)
        {
            int count = r.positionCount;
            EnsurePoints(count);
            r.GetPositions(points);
            WritePoints(count, r.useWorldSpace, r.transform);
            float m = r.widthMultiplier;
            piece.WriteFloat(r.widthCurve.Evaluate(0f) * m);
            piece.WriteFloat(r.widthCurve.Evaluate(1f) * m);
            WriteColor(piece, r.startColor);
            WriteColor(piece, r.endColor);
            piece.WriteBool(r.loop);
            piece.WriteByte((byte)Mathf.Clamp(r.numCapVertices, 0, 255));
            piece.WriteByte((byte)Mathf.Clamp(r.numCornerVertices, 0, 255));
            WriteSorting(r);
        }

        private void WriteTrail(TrailRenderer r)
        {
            int count = r.positionCount;
            EnsurePoints(count);
            count = r.GetPositions(points);
            WritePoints(count, true, r.transform);
            float m = r.widthMultiplier;
            // 꼬리는 오래된 점(앞)부터 저장된다: 머리(마지막 점)가 startWidth 쪽
            piece.WriteFloat(r.widthCurve.Evaluate(1f) * m);
            piece.WriteFloat(r.widthCurve.Evaluate(0f) * m);
            WriteColor(piece, r.colorGradient.Evaluate(1f));
            WriteColor(piece, r.colorGradient.Evaluate(0f));
            piece.WriteBool(false);
            piece.WriteByte((byte)Mathf.Clamp(r.numCapVertices, 0, 255));
            piece.WriteByte((byte)Mathf.Clamp(r.numCornerVertices, 0, 255));
            WriteSorting(r);
        }

        private void WriteText(TextMesh t, MeshRenderer r)
        {
            WriteTransform(t.transform);
            piece.WriteString(t.text);
            piece.WriteVarUInt((uint)Mathf.Max(0, t.fontSize));
            piece.WriteFloat(t.characterSize);
            piece.WriteByte((byte)t.anchor);
            piece.WriteByte((byte)t.alignment);
            piece.WriteByte((byte)t.fontStyle);
            piece.WriteFloat(t.lineSpacing);
            piece.WriteBool(t.richText);
            WriteColor(piece, t.color);
            WriteSorting(r);
        }
    }

    // 친구 쪽: World 메시지를 받아 같은 모양의 조각들을 만들고 옮긴다. 카메라도 호스트 자리로.
    public sealed class NetWorldView
    {
        private sealed class Piece
        {
            public GameObject Go;
            public WorldPieceKind Kind;
            public SpriteRenderer Sprite;
            public LineRenderer Line;
            public TextMesh Text;
            public MeshRenderer TextRenderer;
            public Vector3[] Points;
        }

        private readonly NetAssetReceiver assets;
        private readonly Dictionary<int, Piece> pieces = new Dictionary<int, Piece>();
        private readonly Transform root;
        private readonly Camera camera;
        private Material lineMaterial;
        private Material flashMaterial;
        private Font font;
        private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public Camera Camera => camera;
        public int PieceCount => pieces.Count;

        // 테스트용: 만들어진 조각 오브젝트들
        internal IEnumerable<GameObject> PieceObjects
        {
            get { foreach (var p in pieces.Values) yield return p.Go; }
        }

        public NetWorldView(NetAssetReceiver assets, Transform root, Camera camera)
        {
            this.assets = assets;
            this.root = root;
            this.camera = camera;
        }

        public void Clear()
        {
            foreach (var p in pieces.Values) if (p.Go != null) NetUnity.Destroy(p.Go);
            pieces.Clear();
            // 조각과 함께 공용 재질도 치운다 (다시 그릴 때 새로 만든다). 전에는 친구 창을 열고 닫을 때마다 재질 2개가 남았다
            NetUnity.Destroy(lineMaterial);
            NetUnity.Destroy(flashMaterial);
            lineMaterial = null;
            flashMaterial = null;
        }

        public void Read(NetReader r)
        {
            if (r.ReadBool())
            {
                var pos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                float size = r.ReadFloat();
                var bg = ReadColor(r);
                camera.transform.position = pos;
                camera.orthographicSize = size;
                camera.backgroundColor = bg;
            }

            int changed = (int)r.ReadVarUInt();
            for (int i = 0; i < changed; i++)
            {
                int id = r.ReadVarInt();
                var kind = (WorldPieceKind)r.ReadByte();
                var piece = Get(id, kind);
                switch (kind)
                {
                    case WorldPieceKind.Sprite: ReadSprite(r, piece); break;
                    case WorldPieceKind.Line: ReadLine(r, piece); break;
                    case WorldPieceKind.Text: ReadText(r, piece); break;
                    default: throw new System.FormatException("모르는 그림 조각 " + kind);
                }
            }

            int removed = (int)r.ReadVarUInt();
            for (int i = 0; i < removed; i++)
            {
                int id = r.ReadVarInt();
                if (!pieces.TryGetValue(id, out var p)) continue;
                if (p.Go != null) NetUnity.Destroy(p.Go);
                pieces.Remove(id);
            }
        }

        private static Color32 ReadColor(NetReader r) => new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());

        private Piece Get(int id, WorldPieceKind kind)
        {
            if (pieces.TryGetValue(id, out var p))
            {
                if (p.Kind == kind) return p;
                if (p.Go != null) NetUnity.Destroy(p.Go); // 같은 번호가 다른 종류로 (거의 없지만)
            }

            p = new Piece { Kind = kind, Go = new GameObject("Net " + kind + " " + id) };
            p.Go.transform.SetParent(root, false);
            switch (kind)
            {
                case WorldPieceKind.Sprite:
                    p.Sprite = p.Go.AddComponent<SpriteRenderer>();
                    break;
                case WorldPieceKind.Line:
                    p.Line = p.Go.AddComponent<LineRenderer>();
                    p.Line.useWorldSpace = true;
                    if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
                    p.Line.sharedMaterial = lineMaterial;
                    break;
                case WorldPieceKind.Text:
                    p.Text = p.Go.AddComponent<TextMesh>();
                    p.TextRenderer = p.Go.GetComponent<MeshRenderer>();
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font != null)
                    {
                        p.Text.font = font;
                        p.TextRenderer.sharedMaterial = font.material;
                    }
                    break;
            }
            pieces[id] = p;
            return p;
        }

        private static void ReadTransform(NetReader r, Transform t)
        {
            t.position = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            t.rotation = Quaternion.Euler(0f, 0f, r.ReadFloat());
            t.localScale = new Vector3(r.ReadFloat(), r.ReadFloat(), 1f);
        }

        private void ReadSprite(NetReader r, Piece p)
        {
            var s = p.Sprite;
            s.sprite = assets.Sprite((int)r.ReadVarUInt());
            ReadTransform(r, p.Go.transform);
            byte flags = r.ReadByte();
            s.flipX = (flags & 1) != 0;
            s.flipY = (flags & 2) != 0;
            s.color = ReadColor(r);
            s.sortingLayerID = r.ReadVarInt();
            s.sortingOrder = r.ReadVarInt();
            if ((flags & 4) != 0)
            {
                s.drawMode = (flags & 8) != 0 ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
                s.size = new Vector2(r.ReadFloat(), r.ReadFloat());
            }
            else s.drawMode = SpriteDrawMode.Simple;

            if ((flags & 16) != 0)
            {
                float amount = r.ReadFloat();
                Color flashColor = ReadColor(r);
                if (flashMaterial == null)
                {
                    var shader = Shader.Find("IBlameYou/SpriteFlash");
                    if (shader != null && shader.isSupported) flashMaterial = new Material(shader);
                }
                if (flashMaterial != null)
                {
                    s.sharedMaterial = flashMaterial;
                    s.GetPropertyBlock(block);
                    block.SetFloat(FlashAmount, amount);
                    block.SetColor(FlashColor, flashColor);
                    s.SetPropertyBlock(block);
                }
            }
            else if (flashMaterial != null && s.sharedMaterial == flashMaterial)
            {
                s.SetPropertyBlock(null);
                s.sharedMaterial = SpriteDefault();
            }
        }

        private static Material spriteDefault;

        private static Material SpriteDefault()
        {
            if (spriteDefault == null)
            {
                var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
                spriteDefault = probe.sharedMaterial;
                NetUnity.Destroy(probe.gameObject);
            }
            return spriteDefault;
        }

        private void ReadLine(NetReader r, Piece p)
        {
            int count = (int)r.ReadVarUInt();
            if (p.Points == null || p.Points.Length != count) p.Points = new Vector3[count]; // SetPositions가 배열 길이를 쓰므로 딱 맞게
            for (int i = 0; i < count; i++) p.Points[i] = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            var line = p.Line;
            line.positionCount = count;
            line.SetPositions(p.Points);
            line.widthMultiplier = 1f;
            line.startWidth = r.ReadFloat();
            line.endWidth = r.ReadFloat();
            line.startColor = ReadColor(r);
            line.endColor = ReadColor(r);
            line.loop = r.ReadBool();
            line.numCapVertices = r.ReadByte();
            line.numCornerVertices = r.ReadByte();
            line.sortingLayerID = r.ReadVarInt();
            line.sortingOrder = r.ReadVarInt();
        }

        private static void ReadText(NetReader r, Piece p)
        {
            ReadTransform(r, p.Go.transform);
            var t = p.Text;
            t.text = r.ReadString();
            t.fontSize = (int)r.ReadVarUInt();
            t.characterSize = r.ReadFloat();
            t.anchor = (TextAnchor)r.ReadByte();
            t.alignment = (TextAlignment)r.ReadByte();
            t.fontStyle = (FontStyle)r.ReadByte();
            t.lineSpacing = r.ReadFloat();
            t.richText = r.ReadBool();
            t.color = ReadColor(r);
            p.TextRenderer.sortingLayerID = r.ReadVarInt();
            p.TextRenderer.sortingOrder = r.ReadVarInt();
        }
    }
}
