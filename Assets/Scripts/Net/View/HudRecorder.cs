using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Net
{
    public enum HudOp : byte
    {
        Matrix = 1, Color = 2, ContentColor = 3, BackgroundColor = 4, Enabled = 5,
        Label = 10, Box = 11, StyleDraw = 12, Texture = 13, TexCoords = 14, Slider = 15,
        Group = 20, EndGroup = 21, Hide = 22,
        Style = 30,
    }

    // 호스트 쪽: 한 프레임 동안 NetGUI를 거친 그리기 명령을 모아 Hud 메시지로 만든다.
    // Repaint 이벤트 때만 기록한다 (OnGUI는 한 프레임에 여러 번 불리지만 실제로 그리는 것은 Repaint).
    // GUIStyle은 번호를 붙이고 모양이 바뀔 때만 다시 보낸다 (Style 명령, 같은 프레임 안에서 순서대로 적용).
    public static class HudRecorder
    {
        public static bool Recording;          // NetHostStreamer가 프레임마다 정한다
        public static NetAssetSender Assets;
        internal static bool ForceRepaintForTests; // EditMode 테스트는 OnGUI 밖이라 Repaint 이벤트가 없다

        private static readonly NetWriter frame = new NetWriter(32 * 1024);
        private static readonly NetWriter scratch = new NetWriter(256);
        private static readonly List<byte[]> completed = new List<byte[]>();
        private static int frameNumber = -1;
        private static int headerLength;

        private static readonly Dictionary<GUIStyle, int> styleIds = new Dictionary<GUIStyle, int>();
        private static readonly Dictionary<int, byte[]> styleBytes = new Dictionary<int, byte[]>();
        private static int nextStyle = 1;

        private static Matrix4x4 lastMatrix;
        private static Color lastColor, lastContentColor, lastBackgroundColor;
        private static bool lastEnabled;
        private static bool stateKnown;

        public static int LastRecordedFrame { get; private set; } = -1;

        public static void ResetTables()
        {
            styleIds.Clear();
            styleBytes.Clear();
            nextStyle = 1;
            completed.Clear();
            frameNumber = -1;
            frame.Clear();
        }

        // 지금 그리는 중이고 기록해야 하나
        public static bool Active
        {
            get
            {
                if (!Recording || Assets == null) return false;
                if (ForceRepaintForTests) return true;
                var e = Event.current;
                return e != null && e.type == EventType.Repaint;
            }
        }

        private static bool Begin()
        {
            if (!Active) return false;
            if (Time.frameCount != frameNumber)
            {
                Finish();
                frameNumber = Time.frameCount;
                frame.Clear();
                frame.WriteByte((byte)MsgType.Hud);
                frame.WriteVarUInt((uint)Screen.width);
                frame.WriteVarUInt((uint)Screen.height);
                headerLength = frame.Length;
                stateKnown = false;
            }
            SyncState();
            return true;
        }

        private static void Finish()
        {
            if (frameNumber < 0) return;
            completed.Add(frame.ToArray());
            LastRecordedFrame = frameNumber;
            frameNumber = -1;
        }

        // 지난 프레임까지 다 기록된 Hud 메시지들 (이번 프레임 것은 아직 그리는 중이면 남긴다). includeCurrent: 테스트용으로 지금 것까지
        public static void TakeCompleted(List<byte[]> into, bool includeCurrent = false)
        {
            if (frameNumber >= 0 && (includeCurrent || frameNumber < Time.frameCount)) Finish();
            into.AddRange(completed);
            completed.Clear();
        }

        private static void SyncState()
        {
            var m = GUI.matrix;
            if (!stateKnown || m != lastMatrix)
            {
                lastMatrix = m;
                frame.WriteByte((byte)HudOp.Matrix);
                frame.WriteFloat(m.m00); frame.WriteFloat(m.m01); frame.WriteFloat(m.m03);
                frame.WriteFloat(m.m10); frame.WriteFloat(m.m11); frame.WriteFloat(m.m13);
            }
            SyncColor(HudOp.Color, GUI.color, ref lastColor);
            SyncColor(HudOp.ContentColor, GUI.contentColor, ref lastContentColor);
            SyncColor(HudOp.BackgroundColor, GUI.backgroundColor, ref lastBackgroundColor);
            bool en = GUI.enabled;
            if (!stateKnown || en != lastEnabled)
            {
                lastEnabled = en;
                frame.WriteByte((byte)HudOp.Enabled);
                frame.WriteBool(en);
            }
            stateKnown = true;
        }

        private static void SyncColor(HudOp op, Color c, ref Color last)
        {
            if (stateKnown && c == last) return;
            last = c;
            frame.WriteByte((byte)op);
            WriteColor(frame, c);
        }

        internal static void WriteColor(NetWriter w, Color c)
        {
            // HUD는 알파를 미세하게 움직이는 연출이 많아 float 그대로 (색당 16바이트지만 바뀔 때만 보낸다)
            w.WriteFloat(c.r); w.WriteFloat(c.g); w.WriteFloat(c.b); w.WriteFloat(c.a);
        }

        private static void WriteRect(NetWriter w, Rect r)
        {
            w.WriteFloat(r.x); w.WriteFloat(r.y); w.WriteFloat(r.width); w.WriteFloat(r.height);
        }

        public static byte StateFlags(bool hover, bool active, bool on, bool focus) =>
            (byte)((hover ? 1 : 0) | (active ? 2 : 0) | (on ? 4 : 0) | (focus ? 8 : 0));

        public static void Content(HudOp op, Rect rect, GUIContent content, GUIStyle style, byte flags)
        {
            if (!Begin()) return;
            int styleId = StyleId(style);
            int image = content != null ? Assets.TextureId(content.image) : 0;
            frame.WriteByte((byte)op);
            WriteRect(frame, rect);
            frame.WriteString(content != null ? content.text : null);
            frame.WriteVarUInt((uint)image);
            frame.WriteVarUInt((uint)styleId);
            if (op == HudOp.StyleDraw) frame.WriteByte(flags);
        }

        public static void Texture(Rect rect, Texture image, ScaleMode scaleMode, bool alphaBlend, float aspect, bool extra, Color color, Vector4 borders, Vector4 radii)
        {
            if (image == null || !Begin()) return;
            int id = Assets.TextureId(image);
            frame.WriteByte((byte)HudOp.Texture);
            WriteRect(frame, rect);
            frame.WriteVarUInt((uint)id);
            frame.WriteByte((byte)((int)scaleMode | (alphaBlend ? 8 : 0) | (extra ? 16 : 0)));
            frame.WriteFloat(aspect);
            if (!extra) return;
            WriteColor(frame, color);
            frame.WriteFloat(borders.x); frame.WriteFloat(borders.y); frame.WriteFloat(borders.z); frame.WriteFloat(borders.w);
            frame.WriteFloat(radii.x); frame.WriteFloat(radii.y); frame.WriteFloat(radii.z); frame.WriteFloat(radii.w);
        }

        public static void TexCoords(Rect rect, Texture image, Rect coords, bool alphaBlend)
        {
            if (image == null || !Begin()) return;
            int id = Assets.TextureId(image);
            frame.WriteByte((byte)HudOp.TexCoords);
            WriteRect(frame, rect);
            frame.WriteVarUInt((uint)id);
            WriteRect(frame, coords);
            frame.WriteBool(alphaBlend);
        }

        public static void Slider(Rect rect, float value, float left, float right, GUIStyle slider, GUIStyle thumb)
        {
            if (!Begin()) return;
            int s = StyleId(slider);
            int t = StyleId(thumb);
            frame.WriteByte((byte)HudOp.Slider);
            WriteRect(frame, rect);
            frame.WriteFloat(value);
            frame.WriteFloat(left);
            frame.WriteFloat(right);
            frame.WriteVarUInt((uint)s);
            frame.WriteVarUInt((uint)t);
        }

        public static void Group(Rect rect)
        {
            if (!Begin()) return;
            frame.WriteByte((byte)HudOp.Group);
            WriteRect(frame, rect);
        }

        // 자리를 나중에 아는 묶음 (GUILayout 스크롤 칸: 칸 자리는 안을 다 그린 뒤 GetLastRect로 안다).
        // 돌려준 표를 PatchGroup에 넘기면 자리를 채운다. 기록 중이 아니면 -1.
        public static long BeginDeferredGroup()
        {
            if (!Begin()) return -1;
            frame.WriteByte((byte)HudOp.Group);
            int position = frame.Length;
            WriteRect(frame, new Rect(0f, 0f, 0f, 0f));
            return ((long)frameNumber << 32) | (uint)position;
        }

        public static void PatchGroup(long token, Rect rect)
        {
            if (token < 0) return;
            int frameOfToken = (int)(token >> 32);
            int position = (int)(token & 0xffffffffL);
            if (frameOfToken != frameNumber || position + 16 > frame.Length) return; // 그 사이 프레임이 넘어갔다
            frame.PatchFloat(position, rect.x);
            frame.PatchFloat(position + 4, rect.y);
            frame.PatchFloat(position + 8, rect.width);
            frame.PatchFloat(position + 12, rect.height);
        }

        public static void EndGroup()
        {
            if (!Begin()) return;
            frame.WriteByte((byte)HudOp.EndGroup);
        }

        public static void Hide(bool hide)
        {
            if (!Begin()) return;
            frame.WriteByte((byte)HudOp.Hide);
            frame.WriteBool(hide);
        }

        // 스타일 번호. 처음이거나 모양이 바뀌었으면 Style 명령을 프레임에 먼저 넣는다.
        private static int StyleId(GUIStyle style)
        {
            if (style == null) return 0;
            scratch.Clear();
            WriteStyle(scratch, style);
            if (!styleIds.TryGetValue(style, out int id))
            {
                id = nextStyle++;
                styleIds[style] = id;
            }
            else if (styleBytes.TryGetValue(id, out var previous) && scratch.SameAs(previous)) return id;

            styleBytes[id] = scratch.ToArray();
            frame.WriteByte((byte)HudOp.Style);
            frame.WriteVarUInt((uint)id);
            frame.WriteRaw(scratch);
            return id;
        }

        private static void WriteStyle(NetWriter w, GUIStyle s)
        {
            w.WriteVarInt(s.fontSize);
            w.WriteByte((byte)s.fontStyle);
            w.WriteByte((byte)s.alignment);
            w.WriteByte((byte)((s.wordWrap ? 1 : 0) | (s.richText ? 2 : 0) | (s.clipping == TextClipping.Clip ? 4 : 0) | (s.stretchWidth ? 8 : 0) | (s.stretchHeight ? 16 : 0)));
            w.WriteByte((byte)s.imagePosition);
            w.WriteFloat(s.contentOffset.x);
            w.WriteFloat(s.contentOffset.y);
            w.WriteFloat(s.fixedWidth);
            w.WriteFloat(s.fixedHeight);
            WriteOffset(w, s.padding);
            WriteOffset(w, s.border);
            WriteOffset(w, s.overflow);
            WriteState(w, s.normal);
            WriteState(w, s.hover);
            WriteState(w, s.active);
            WriteState(w, s.focused);
            WriteState(w, s.onNormal);
            WriteState(w, s.onHover);
            WriteState(w, s.onActive);
        }

        private static void WriteOffset(NetWriter w, RectOffset o)
        {
            w.WriteVarInt(o.left); w.WriteVarInt(o.right); w.WriteVarInt(o.top); w.WriteVarInt(o.bottom);
        }

        private static void WriteState(NetWriter w, GUIStyleState state)
        {
            w.WriteVarUInt((uint)Assets.TextureId(state.background));
            Color32 c = state.textColor;
            w.WriteByte(c.r); w.WriteByte(c.g); w.WriteByte(c.b); w.WriteByte(c.a);
        }
    }

    // 친구 쪽: 받은 Hud 메시지를 명령 목록으로 풀어 두고, OnGUI Repaint 때마다 다시 그린다.
    public sealed class HudReplay
    {
        private abstract class Cmd { }
        private sealed class MatrixCmd : Cmd { public Matrix4x4 M; }
        private sealed class ColorCmd : Cmd { public HudOp Op; public Color C; }
        private sealed class EnabledCmd : Cmd { public bool On; }
        private sealed class ContentCmd : Cmd { public HudOp Op; public Rect R; public GUIContent Content; public GUIStyle Style; public byte Flags; }
        private sealed class TextureCmd : Cmd
        {
            public Rect R; public Texture Tex; public ScaleMode Mode; public bool Alpha; public float Aspect;
            public bool Extra; public Color Tint; public Vector4 Borders; public Vector4 Radii;
        }
        private sealed class TexCoordsCmd : Cmd { public Rect R; public Texture Tex; public Rect Coords; public bool Alpha; }
        private sealed class SliderCmd : Cmd { public Rect R; public float Value, Left, Right; public GUIStyle Slider, Thumb; }
        private sealed class GroupCmd : Cmd { public Rect R; }
        private sealed class EndGroupCmd : Cmd { }
        private sealed class HideCmd : Cmd { public bool Hide; }

        private readonly NetAssetReceiver assets;
        private readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private List<Cmd> commands = new List<Cmd>();
        private int hostWidth = 1920;
        private int hostHeight = 1080;

        public HudReplay(NetAssetReceiver assets)
        {
            this.assets = assets;
        }

        public int CommandCount => commands.Count;
        internal int HostWidth => hostWidth;
        internal int HostHeight => hostHeight;
        internal GUIStyle StyleById(int id) => styles.TryGetValue(id, out var s) ? s : null;

        // 테스트용: n번째 글자 명령의 글자
        internal string TextAt(int index)
        {
            int seen = 0;
            foreach (var c in commands)
            {
                if (!(c is ContentCmd content)) continue;
                if (seen++ == index) return content.Content.text;
            }
            return null;
        }

        public void Clear()
        {
            styles.Clear();
            commands = new List<Cmd>();
        }

        public void Read(NetReader r)
        {
            hostWidth = Mathf.Max(1, (int)r.ReadVarUInt());
            hostHeight = Mathf.Max(1, (int)r.ReadVarUInt());
            var list = new List<Cmd>();
            while (!r.AtEnd)
            {
                var op = (HudOp)r.ReadByte();
                switch (op)
                {
                    case HudOp.Matrix:
                        var m = Matrix4x4.identity;
                        m.m00 = r.ReadFloat(); m.m01 = r.ReadFloat(); m.m03 = r.ReadFloat();
                        m.m10 = r.ReadFloat(); m.m11 = r.ReadFloat(); m.m13 = r.ReadFloat();
                        list.Add(new MatrixCmd { M = m });
                        break;
                    case HudOp.Color:
                    case HudOp.ContentColor:
                    case HudOp.BackgroundColor:
                        list.Add(new ColorCmd { Op = op, C = ReadColor(r) });
                        break;
                    case HudOp.Enabled:
                        list.Add(new EnabledCmd { On = r.ReadBool() });
                        break;
                    case HudOp.Label:
                    case HudOp.Box:
                    case HudOp.StyleDraw:
                    {
                        var rect = ReadRect(r);
                        string text = r.ReadString();
                        var image = assets.Texture((int)r.ReadVarUInt());
                        var style = Style((int)r.ReadVarUInt());
                        byte flags = op == HudOp.StyleDraw ? r.ReadByte() : (byte)0;
                        list.Add(new ContentCmd { Op = op, R = rect, Content = new GUIContent(text, image), Style = style, Flags = flags });
                        break;
                    }
                    case HudOp.Texture:
                    {
                        var cmd = new TextureCmd { R = ReadRect(r), Tex = assets.Texture((int)r.ReadVarUInt()) };
                        byte bits = r.ReadByte();
                        cmd.Mode = (ScaleMode)(bits & 7);
                        cmd.Alpha = (bits & 8) != 0;
                        cmd.Extra = (bits & 16) != 0;
                        cmd.Aspect = r.ReadFloat();
                        if (cmd.Extra)
                        {
                            cmd.Tint = ReadColor(r);
                            cmd.Borders = new Vector4(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                            cmd.Radii = new Vector4(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                        }
                        list.Add(cmd);
                        break;
                    }
                    case HudOp.TexCoords:
                        list.Add(new TexCoordsCmd { R = ReadRect(r), Tex = assets.Texture((int)r.ReadVarUInt()), Coords = ReadRect(r), Alpha = r.ReadBool() });
                        break;
                    case HudOp.Slider:
                        list.Add(new SliderCmd { R = ReadRect(r), Value = r.ReadFloat(), Left = r.ReadFloat(), Right = r.ReadFloat(), Slider = Style((int)r.ReadVarUInt()), Thumb = Style((int)r.ReadVarUInt()) });
                        break;
                    case HudOp.Group:
                        list.Add(new GroupCmd { R = ReadRect(r) });
                        break;
                    case HudOp.EndGroup:
                        list.Add(new EndGroupCmd());
                        break;
                    case HudOp.Hide:
                        list.Add(new HideCmd { Hide = r.ReadBool() });
                        break;
                    case HudOp.Style:
                    {
                        int id = (int)r.ReadVarUInt();
                        styles[id] = ReadStyle(r); // 새 객체: 앞서 푼 명령은 예전 모양 그대로
                        break;
                    }
                    default:
                        throw new System.FormatException("모르는 HUD 명령 " + op);
                }
            }
            commands = list;
        }

        private GUIStyle Style(int id) => id != 0 && styles.TryGetValue(id, out var s) ? s : GUIStyle.none;

        private static Rect ReadRect(NetReader r) => new Rect(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());

        private static Color ReadColor(NetReader r) => new Color(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());

        private GUIStyle ReadStyle(NetReader r)
        {
            var s = new GUIStyle
            {
                fontSize = r.ReadVarInt(),
                fontStyle = (FontStyle)r.ReadByte(),
                alignment = (TextAnchor)r.ReadByte(),
            };
            byte bits = r.ReadByte();
            s.wordWrap = (bits & 1) != 0;
            s.richText = (bits & 2) != 0;
            s.clipping = (bits & 4) != 0 ? TextClipping.Clip : TextClipping.Overflow;
            s.stretchWidth = (bits & 8) != 0;
            s.stretchHeight = (bits & 16) != 0;
            s.imagePosition = (ImagePosition)r.ReadByte();
            s.contentOffset = new Vector2(r.ReadFloat(), r.ReadFloat());
            s.fixedWidth = r.ReadFloat();
            s.fixedHeight = r.ReadFloat();
            s.padding = ReadOffset(r);
            s.border = ReadOffset(r);
            s.overflow = ReadOffset(r);
            ReadState(r, s.normal);
            ReadState(r, s.hover);
            ReadState(r, s.active);
            ReadState(r, s.focused);
            ReadState(r, s.onNormal);
            ReadState(r, s.onHover);
            ReadState(r, s.onActive);
            return s;
        }

        private static RectOffset ReadOffset(NetReader r) => new RectOffset(r.ReadVarInt(), r.ReadVarInt(), r.ReadVarInt(), r.ReadVarInt());

        private void ReadState(NetReader r, GUIStyleState state)
        {
            state.background = assets.Texture((int)r.ReadVarUInt()) as Texture2D;
            state.textColor = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
        }

        // OnGUI 안에서 (Repaint 때만 그린다). 호스트 창 크기를 친구 창 크기로 늘이거나 줄인다.
        public void Draw()
        {
            if (Event.current.type != EventType.Repaint || commands.Count == 0) return;
            var scale = Matrix4x4.Scale(new Vector3(Screen.width / (float)hostWidth, Screen.height / (float)hostHeight, 1f));
            var savedMatrix = GUI.matrix;
            var savedColor = GUI.color;
            var savedContent = GUI.contentColor;
            var savedBackground = GUI.backgroundColor;
            bool savedEnabled = GUI.enabled;
            GUI.matrix = scale;
            int groups = 0;
            int hidden = 0;

            foreach (var cmd in commands)
            {
                switch (cmd)
                {
                    case MatrixCmd m: GUI.matrix = scale * m.M; break;
                    case ColorCmd c:
                        if (c.Op == HudOp.Color) GUI.color = c.C;
                        else if (c.Op == HudOp.ContentColor) GUI.contentColor = c.C;
                        else GUI.backgroundColor = c.C;
                        break;
                    case EnabledCmd e: GUI.enabled = e.On; break;
                    case HideCmd h: hidden = Mathf.Max(0, hidden + (h.Hide ? 1 : -1)); break;
                    case GroupCmd g:
                        GUI.BeginGroup(g.R);
                        groups++;
                        break;
                    case EndGroupCmd _:
                        if (groups > 0)
                        {
                            GUI.EndGroup();
                            groups--;
                        }
                        break;
                    default:
                        if (hidden == 0) DrawOne(cmd);
                        break;
                }
            }

            while (groups-- > 0) GUI.EndGroup();
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
            GUI.contentColor = savedContent;
            GUI.backgroundColor = savedBackground;
            GUI.enabled = savedEnabled;
        }

        private static void DrawOne(Cmd cmd)
        {
            switch (cmd)
            {
                case ContentCmd c:
                    if (c.Op == HudOp.Label) GUI.Label(c.R, c.Content, c.Style);
                    else if (c.Op == HudOp.Box) GUI.Box(c.R, c.Content, c.Style);
                    else c.Style.Draw(c.R, c.Content, (c.Flags & 1) != 0, (c.Flags & 2) != 0, (c.Flags & 4) != 0, (c.Flags & 8) != 0);
                    break;
                case TextureCmd t:
                    if (t.Tex == null) break;
                    if (t.Extra) GUI.DrawTexture(t.R, t.Tex, t.Mode, t.Alpha, t.Aspect, t.Tint, t.Borders, t.Radii);
                    else GUI.DrawTexture(t.R, t.Tex, t.Mode, t.Alpha, t.Aspect);
                    break;
                case TexCoordsCmd tc:
                    if (tc.Tex != null) GUI.DrawTextureWithTexCoords(tc.R, tc.Tex, tc.Coords, tc.Alpha);
                    break;
                case SliderCmd s:
                    s.Slider.Draw(s.R, GUIContent.none, false, false, false, false);
                    float k = Mathf.Approximately(s.Right, s.Left) ? 0f : Mathf.Clamp01((s.Value - s.Left) / (s.Right - s.Left));
                    float thumbWidth = s.Thumb.fixedWidth > 0f ? s.Thumb.fixedWidth : 12f;
                    float thumbHeight = s.Thumb.fixedHeight > 0f ? s.Thumb.fixedHeight : s.R.height;
                    var thumb = new Rect(s.R.x + (s.R.width - thumbWidth) * k, s.R.y + (s.R.height - thumbHeight) * 0.5f, thumbWidth, thumbHeight);
                    s.Thumb.Draw(thumb, GUIContent.none, false, false, false, false);
                    break;
            }
        }
    }
}
