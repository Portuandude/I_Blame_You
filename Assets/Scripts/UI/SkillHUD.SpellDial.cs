using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // P2 마법 고르기 2단계 원형 다이얼 그리기 (상태·규칙은 RoleInput.Dial / SpellDialRules).
    // 우클릭을 누르고 있으면 커서 둘레에 속성 4칸(위 불 · 오른쪽 물 · 아래 바람 · 왼쪽 땅), 배운 마법이 둘 이상인 속성을 고르면
    // 그 칸 자리에 마법 칸들이 펼쳐진다. 귀엽지만 화려하게: 둥근 속성 색 방울, 고른 칸은 커지고 하얀 테두리 + 반짝임.
    public partial class SkillHUD
    {
        private const float DialBubble = 26f;       // 칸 방울 반지름 (픽셀)
        private const float DialBubbleActive = 33f;  // 고른 칸
        private const int DialTextureSize = 64;
        private static Texture2D dialDisc;
        private GUIStyle dialName;
        private GUIStyle dialHint;

        private void DrawSpellDial()
        {
            var dial = RoleInput.Dial;
            if (!dial.IsOpen) return;
            if (GameSession.DevMode || caster == null || control == null) return;
            if (caster.ChantOpen || caster.WindingUp || control.Locked || health.IsDead || deathHandled || stageCleared) return;
            if (paused || traitWindowOpen || relicWindowOpen || equipCardOpen || villageOpen) return;
            if (dialName == null)
            {
                dialName = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontStyle = FontStyle.Bold, fontSize = 13 };
                dialHint = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontSize = 12 };
            }

            var previous = GUI.color;
            bool spellStage = dial.Stage == SpellDialStage.Spell;
            var elementCenter = ToGui(dial.ElementCenterX, dial.ElementCenterY);

            // 1단계: 속성 다이얼 (마법 다이얼이 열리면 옅게 남긴다)
            float elementAlpha = spellStage ? 0.35f : 1f;
            DrawDisc(elementCenter, SpellDialRules.Radius + DialBubble + 8f, new Color(0.08f, 0.05f, 0.15f, 0.35f * elementAlpha)); // 받침
            for (int i = 0; i < SpellDialRules.ElementCount; i++)
            {
                var element = (Element)i;
                SpellDialRules.SlotOffset(i, SpellDialRules.ElementCount, SpellDialRules.Radius, out float ox, out float oy);
                var at = elementCenter + new Vector2(ox, -oy); // GUI 좌표는 위가 0
                bool active = i == dial.ElementSlot;
                bool empty = dial.Count(i) == 0;
                string extra = dial.Count(i) > 1 ? $"\n<size=10>마법 {dial.Count(i)}</size>" : "";
                DrawBubble(at, element, ElementInfo.KoreanName(element) + extra, active && !spellStage, empty, elementAlpha);
            }

            if (!spellStage)
            {
                var chosen = SpellBook.Get((Element)dial.ElementSlot, dial.SpellSlot);
                if (chosen != null)
                    DrawShadowed(new Rect(elementCenter.x - 80f, elementCenter.y - 11f, 160f, 22f), chosen.Name, dialName, ElementInfo.Color(chosen.Element));
            }
            else
            {
                // 2단계: 고른 속성 칸 자리에 그 속성의 배운 마법들
                var center = ToGui(dial.CenterX, dial.CenterY);
                var element = (Element)dial.ElementSlot;
                int count = dial.Count(dial.ElementSlot);
                float radius = SpellDialRules.Radius;
                DrawDisc(center, radius + DialBubble + 8f, new Color(0.08f, 0.05f, 0.15f, 0.45f));
                for (int i = 0; i < count; i++)
                {
                    SpellDialRules.SlotOffset(i, count, radius, out float ox, out float oy);
                    var spell = SpellBook.Get(element, i);
                    DrawBubble(center + new Vector2(ox, -oy), element, spell != null ? spell.Name : "?", i == dial.SpellSlot, false, 1f);
                }
                var chosen = SpellBook.Get(element, dial.SpellSlot);
                if (chosen != null)
                    DrawShadowed(new Rect(center.x - 80f, center.y - 11f, 160f, 22f), chosen.Name, dialName, ElementInfo.Color(element));
            }

            DrawShadowed(new Rect(elementCenter.x - 120f, elementCenter.y + SpellDialRules.Radius + DialBubble + 10f, 240f, 18f),
                "떼면 영창 · 휠·커서로 고르기 · 좌클릭 취소", dialHint, new Color(1f, 1f, 1f, 0.85f));
            GUI.color = previous;
        }

        private void DrawBubble(Vector2 at, Element element, string text, bool active, bool empty, float alpha)
        {
            var color = ElementInfo.Color(element);
            float pulse = active ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 10f) : 1f; // 고른 칸은 살짝 통통 튄다
            float r = (active ? DialBubbleActive : DialBubble) * pulse;
            if (active) DrawDisc(at, r + 3f, new Color(1f, 1f, 1f, 0.95f * alpha)); // 하얀 테두리
            var fill = empty ? new Color(0.35f, 0.35f, 0.4f, 0.6f * alpha) : new Color(color.r, color.g, color.b, (active ? 1f : 0.75f) * alpha);
            DrawDisc(at, r, fill);
            DrawDisc(at + new Vector2(-r * 0.3f, -r * 0.35f), r * 0.28f, new Color(1f, 1f, 1f, 0.45f * alpha)); // 반짝임
            var label = new Rect(at.x - 50f, at.y - 16f, 100f, 32f);
            dialName.richText = true;
            DrawShadowed(label, text, dialName, new Color(1f, 1f, 1f, (empty ? 0.5f : 1f) * alpha));
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f * color.a);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
        }

        private static void DrawDisc(Vector2 center, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), DialDisc);
        }

        private static Vector2 ToGui(float x, float y) => new Vector2(x, Screen.height - y);

        // 가장자리가 부드러운 하얀 원 (런타임에 한 번 만든다)
        private static Texture2D DialDisc
        {
            get
            {
                if (dialDisc != null) return dialDisc;
                int size = DialTextureSize;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                        byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(half - d));
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                dialDisc = texture;
                return texture;
            }
        }
    }
}
