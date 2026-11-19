using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // 마법진을 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.UI
{
    // P2 마법진 그리기 화면 (2026-11-16, Docs/MagicCircleDesign.md). 귀엽지만 화려하게.
    // - 판: 화면 가운데 어두운 원판 위에 속성 색으로 빛나는 마법진 (SpellCaster.BoardCenter/BoardRadius, 입력과 같은 자리)
    // - 흐린 띠 = 허용 폭, 하얀 점 = 시작, 빈 고리 = 끝, 선을 따라 도는 별똥별 = 그리는 방향
    // - P2가 그린 선은 하얗게 빛나고, 판정하면 일치율과 한 마디가 판 가운데에 잠깐 크게 뜬다
    // 게임은 멈추지 않으므로 판은 반투명이다 (뒤에서 P1이 움직이는 게 보이게).
    public partial class SkillHUD
    {
        private const float CircleResultShow = 1.3f;
        private static Texture2D softDot;   // 가운데가 진하고 가장자리로 흐려지는 원 (빛, 점)
        private static Texture2D ringTex;   // 속이 빈 고리 (끝점 표시)
        private GUIStyle circleTitle;
        private GUIStyle circleResult;

        private void DrawMagicCircle()
        {
            if (caster == null) return;
            bool open = caster.CircleOpen && caster.CirclePath != null;
            float sinceResult = Time.unscaledTime - caster.LastJudgedAt;
            if (!open && sinceResult > CircleResultShow) return;
            EnsureCircleAssets();

            var center = SpellCaster.BoardCenter;
            float r = SpellCaster.BoardRadius;
            bool repaint = Event.current.type == EventType.Repaint;

            if (open && repaint)
            {
                var spell = caster.PendingSpell;
                var main = ElementInfo.Color(spell.Element);
                var light = Color.Lerp(main, Color.white, 0.55f);
                float t = Time.unscaledTime - caster.CircleOpenedAt;
                float lw = Mathf.Max(3f, r * 0.022f);
                var path = caster.CirclePath;

                // 판: 어두운 원 + 속성 빛 + 장식 고리
                DrawDot(center, r * 2.25f, new Color(0.07f, 0.05f, 0.16f, 0.62f));
                DrawDot(center, r * 1.9f, new Color(main.r, main.g, main.b, 0.18f));
                DrawRing(center, r * 0.97f, Mathf.Max(1.5f, lw * 0.3f), new Color(main.r, main.g, main.b, 0.45f), 96);
                // 장식: 바깥을 천천히 도는 작은 빛 (단계가 높을수록 많이)
                int motifs = 6 + 3 * caster.CircleTier;
                for (int i = 0; i < motifs; i++)
                {
                    float a = t * 0.25f + i * Mathf.PI * 2f / motifs;
                    DrawDot(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.97f, lw * 2.6f, new Color(light.r, light.g, light.b, 0.75f));
                }

                // 허용 폭 띠 → 빛 → 심
                DrawPolyline(path, center, r, caster.CircleTolerance * 2f * r, new Color(main.r, main.g, main.b, 0.13f));
                DrawPolyline(path, center, r, lw * 2.4f, new Color(main.r, main.g, main.b, 0.35f));
                DrawPolyline(path, center, r, lw, light);

                // 시작점·끝점
                var start = ToGui(path[0], center, r);
                var end = ToGui(path[path.Count - 1], center, r);
                float pulse = 1f + 0.15f * Mathf.Sin(t * 6f);
                DrawDot(start, lw * 4.5f * pulse, new Color(1f, 1f, 1f, 0.45f));
                DrawDot(start, lw * 2.4f, Color.white);
                DrawTexture(ringTex, end, lw * 3f, Color.white);

                // 별똥별: 그리기 전에는 방향을 알려 주고, 그리는 중에는 흐리게
                float length = MagicCircleRules.Length(path);
                float speed = caster.CircleTier == 2 ? 0.35f : 0.55f;
                float head = Mathf.Repeat(t * speed, 1.15f) * length;
                float cometAlpha = caster.Drawing ? 0.25f : 1f;
                for (int k = 0; k < 10; k++)
                {
                    float d = head - k * length * 0.012f;
                    if (d < 0f || d > length) continue;
                    var p = ToGui(MagicCircleRules.PointAt(path, d), center, r);
                    var c = k == 0 ? Color.white : light;
                    c.a = (1f - k / 10f) * cometAlpha;
                    DrawDot(p, lw * (2.4f - k * 0.15f), c);
                }

                // P2가 그린 선
                if (caster.Drawn.Count > 1)
                {
                    DrawPolyline(caster.Drawn, center, r, lw * 2.6f, new Color(1f, 1f, 1f, 0.3f));
                    DrawPolyline(caster.Drawn, center, r, lw * 0.9f, Color.white);
                }
            }

            if (open)
            {
                var spell = caster.PendingSpell;
                // 위: 마법 이름 · 단계, 주문 한 줄 (읽을거리)
                float top = center.y - r - 64f;
                OutlinedLabel.Draw(new Rect(0f, top, Screen.width, 34f),
                    $"{spell.Name} · {MagicCircleRules.TierName(caster.CircleTier)}{(caster.TailwindActive ? " (순풍)" : "")}",
                    circleTitle, OutlinedLabel.Cream, ElementInfo.Color(spell.Element), 1.5f, 1.5f);
                if (!string.IsNullOrEmpty(spell.Chant))
                    GUI.Label(new Rect(0f, top + 34f, Screen.width, 22f), $"<i>{spell.Chant}</i>", smallCenter);

                // 아래: 남은 시간 캡슐 바 + 안내
                const float barWidth = 300f;
                float ratio = caster.CircleTimeLimit > 0f ? Mathf.Clamp01(caster.CircleTimeLeft / caster.CircleTimeLimit) : 0f;
                var timeColor = Color.Lerp(new Color(1f, 0.44f, 0.68f), new Color(0.45f, 0.9f, 0.4f), ratio);
                float barY = center.y + r + 14f;
                DrawCapsuleBar(new Rect(center.x - barWidth / 2f + BarGlow, barY, barWidth - 2f * BarGlow, BarHeight), ratio, 0f, timeColor, timeColor, null);
                GUI.Label(new Rect(0f, barY + BarHeight + 6f, Screen.width, 22f),
                    caster.Drawing
                        ? "떼지 말고 끝까지! (떼는 순간 판정)"
                        : $"<color=#ffa659>P2</color> 하얀 점에서 좌클릭 꾹 → 한 번에 따라 그리기 · 우클릭 취소 · 마나 {spell.ManaCost * (mana != null ? mana.TotalCostMultiplier : 1f):0}",
                    smallCenter);
            }

            // 판정 결과: 일치율과 한 마디 (실패는 berry, 완벽은 금색)
            if (sinceResult <= CircleResultShow && caster.LastPercent >= 0)
            {
                int pct = caster.LastPercent;
                float fade = 1f - Mathf.Clamp01((sinceResult - CircleResultShow * 0.6f) / (CircleResultShow * 0.4f));
                float pop = 1f + 0.25f * Mathf.Max(0f, 1f - sinceResult / 0.18f);
                var color = pct <= MagicCircleRules.FailAtOrBelow ? new Color(1f, 0.44f, 0.68f)
                    : pct >= MagicCircleRules.PerfectAt ? OutlinedLabel.Gold : Color.Lerp(ElementInfo.Color(caster.LastElement), Color.white, 0.4f);
                color.a = fade;
                var rim = new Color(0.1f, 0.05f, 0.2f, fade);
                var rect = new Rect(0f, center.y - 50f, Screen.width, 100f);
                var previous = GUI.matrix;
                GUIUtility.ScaleAroundPivot(Vector2.one * pop, rect.center);
                string power = pct <= MagicCircleRules.FailAtOrBelow ? "" : $"  ×{MagicCircleRules.DamageMultiplier(pct):0.00}";
                OutlinedLabel.Draw(rect, $"{pct}% {MagicCircleRules.Verdict(pct)}{power}", circleResult, color, rim, 2f, 2f);
                GUI.matrix = previous;
            }
        }

        private static Vector2 ToGui(CirclePoint p, Vector2 center, float r) => new Vector2(center.x + p.X * r, center.y + p.Y * r);

        // 꺾은선: 마디마다 둥근 점 + 마디 사이 회전한 막대 (둥근 이음새)
        private static void DrawPolyline(List<CirclePoint> path, Vector2 center, float r, float width, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            var a = ToGui(path[0], center, r);
            GUI.DrawTexture(new Rect(a.x - width / 2f, a.y - width / 2f, width, width), softDot);
            for (int i = 1; i < path.Count; i++)
            {
                var b = ToGui(path[i], center, r);
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
                GUI.DrawTexture(new Rect(a.x, a.y - width * 0.32f, len, width * 0.64f), Texture2D.whiteTexture);
                GUI.matrix = matrix;
                GUI.DrawTexture(new Rect(b.x - width / 2f, b.y - width / 2f, width, width), softDot);
                a = b;
            }
            GUI.color = previousColor;
        }

        private static void DrawRing(Vector2 center, float radius, float width, Color color, int segments)
        {
            var ring = new List<CirclePoint>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                ring.Add(new CirclePoint(Mathf.Cos(a), Mathf.Sin(a)));
            }
            DrawPolyline(ring, center, radius, width, color);
        }

        private static void DrawDot(Vector2 at, float size, Color color) => DrawTexture(softDot, at, size, color);

        private static void DrawTexture(Texture2D texture, Vector2 at, float size, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), texture);
            GUI.color = previous;
        }

        private void EnsureCircleAssets()
        {
            if (softDot == null) softDot = MakeRadial(64, false);
            if (ringTex == null) ringTex = MakeRadial(64, true);
            if (circleTitle == null)
            {
                circleTitle = new GUIStyle(big) { fontSize = 28 };
                circleResult = new GUIStyle(big) { fontSize = 46 };
            }
        }

        // 그림 파일 없이 런타임에 한 번 만든다. ring = 속이 빈 고리, 아니면 부드러운 원.
        private static Texture2D MakeRadial(int size, bool ring)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float half = (size - 1) / 2f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half; // 0 가운데 ~ 1 가장자리
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.16f)
                        : Mathf.Clamp01((1f - d) / 0.35f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
