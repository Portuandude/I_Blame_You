using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 유물 상자 창에서 고른 유물 아이콘이 카드 자리에서 화면 왼쪽 위 유물 목록(DrawRelicList) 칸으로 날아간다.
    // 창이 닫혀 게임이 다시 움직여도 화면 좌표·실제 시간(unscaledTime)으로 RelicFlyTime 동안 위로 휘는 곡선을 그리며 36 → 18픽셀로 줄어든다.
    // 날아가는 동안 목록의 그 칸은 비워 두고, 도착하면 칸 둘레에 금색 고리가 퍼지며 "칭"(RelicSlot).
    // 목록 칸 위치는 DrawRelicList가 Repaint마다 relicListSlots에 적는다. 칸을 모르면(목록이 안 그려짐) 날아가지 않고 그 자리에서 사라진다.
    public partial class SkillHUD
    {
        private const float RelicFlyTime = 0.45f;
        private const float RelicGlintTime = 0.45f;
        private const float RelicFlyArc = 80f; // 곡선 꼭대기가 출발·도착점 중 높은 쪽보다 이만큼 위

        private readonly Dictionary<RelicId, Rect> relicListSlots = new Dictionary<RelicId, Rect>();
        private readonly Rect[] relicCardIcons = new Rect[4]; // 창에 그린 카드별 큰 아이콘 자리 (출발점)
        private bool relicFlying;
        private RelicId relicFlyId;
        private Rect relicFlyFrom;
        private float relicFlyStart;
        private bool relicGlinting;
        private RelicId relicGlintId;
        private float relicGlintStart;

        private void StartRelicFly(RelicId id, int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= relicCardIcons.Length || RelicIcon(id) == null) return;
            relicFlying = true;
            relicFlyId = id;
            relicFlyFrom = relicCardIcons[cardIndex];
            relicFlyStart = Time.unscaledTime;
            relicGlinting = false;
        }

        // DrawRelicList: 날아오는 중인 유물 칸은 비워 둔다.
        private bool RelicSlotHidden(RelicId id) => relicFlying && id == relicFlyId;

        private void RecordRelicSlot(RelicId id, Rect rect)
        {
            if (Event.current.type == EventType.Repaint) relicListSlots[id] = rect;
        }

        private void DrawRelicFly()
        {
            if (relicFlying)
            {
                float t = (Time.unscaledTime - relicFlyStart) / RelicFlyTime;
                bool hasSlot = relicListSlots.TryGetValue(relicFlyId, out var to);
                if (t >= 1f)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        relicFlying = false;
                        if (hasSlot)
                        {
                            relicGlinting = true;
                            relicGlintId = relicFlyId;
                            relicGlintStart = Time.unscaledTime;
                            GameAudio.PlaySfx("RelicSlot", 0.6f, 0.03f);
                        }
                    }
                }
                else if (Event.current.type == EventType.Repaint)
                {
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                    var icon = RelicIcon(relicFlyId);
                    var previous = GUI.color;
                    if (hasSlot)
                    {
                        // 2차 베지어: 출발 → (가운데, 높은 쪽 - RelicFlyArc) → 도착
                        Vector2 a = relicFlyFrom.center, c = to.center;
                        var control = new Vector2((a.x + c.x) * 0.5f, Mathf.Min(a.y, c.y) - RelicFlyArc);
                        float u = 1f - e;
                        Vector2 p = u * u * a + 2f * u * e * control + e * e * c;
                        float size = Mathf.Lerp(relicFlyFrom.width, to.width, e);
                        // 꼬리: 조금 전 위치에 옅은 잔상 두 개
                        for (int k = 2; k >= 1; k--)
                        {
                            float eb = Mathf.Max(0f, e - 0.06f * k);
                            float ub = 1f - eb;
                            Vector2 pb = ub * ub * a + 2f * ub * eb * control + eb * eb * c;
                            GUI.color = new Color(1f, 0.9f, 0.5f, 0.25f / k);
                            GUI.DrawTexture(new Rect(pb.x - size * 0.5f, pb.y - size * 0.5f, size, size), icon, ScaleMode.ScaleToFit);
                        }
                        GUI.color = Color.white;
                        GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), icon, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.color = new Color(1f, 1f, 1f, 1f - e); // 목록 칸을 모르면 그 자리에서 옅어진다
                        GUI.DrawTexture(relicFlyFrom, icon, ScaleMode.ScaleToFit);
                    }
                    GUI.color = previous;
                }
            }

            if (!relicGlinting) return;
            float g = (Time.unscaledTime - relicGlintStart) / RelicGlintTime;
            if (g >= 1f || !relicListSlots.TryGetValue(relicGlintId, out var slot)) { relicGlinting = false; return; }
            if (Event.current.type != EventType.Repaint) return;
            var before = GUI.color;
            // 칸이 잠깐 하얗게 빛났다가 사라지고, 금색 고리가 바깥으로 퍼진다
            GUI.color = new Color(1f, 1f, 1f, 0.45f * (1f - g));
            GUI.DrawTexture(slot, Texture2D.whiteTexture);
            float grow = 2f + 10f * g;
            const float thick = 2f;
            var ring = new Rect(slot.x - grow, slot.y - grow, slot.width + grow * 2f, slot.height + grow * 2f);
            GUI.color = new Color(1f, 0.85f, 0.3f, 1f - g);
            GUI.DrawTexture(new Rect(ring.x, ring.y, ring.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(ring.x, ring.yMax - thick, ring.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(ring.x, ring.y, thick, ring.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(ring.xMax - thick, ring.y, thick, ring.height), Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
