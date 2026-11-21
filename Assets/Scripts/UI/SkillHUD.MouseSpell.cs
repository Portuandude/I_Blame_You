using IBlameYou.Core;
using IBlameYou.Net;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.UI
{
    // P2가 휠로 고른 공격 스킬을 마우스 커서 옆에 작게 보여 준다 (RoleInput.MouseSpell, 우클릭하면 이 스킬의 마법진이 열림).
    // P2는 조준하느라 커서를 보고 있으니 커서 옆이 가장 잘 보인다. 2026-11-15 2인 점검: 숫자줄 대신 마우스만으로 스킬 고르기.
    public partial class SkillHUD
    {
        private GUIStyle mouseSpellStyle;

        private void DrawMouseSpellTag()
        {
            if (GameSession.DevMode || caster == null || control == null) return;
            if (caster.CircleOpen || control.Locked || health.IsDead || deathHandled || stageCleared) return;
            if (paused || traitWindowOpen || relicWindowOpen || equipCardOpen || villageOpen) return; // 창 위에는 안 띄운다
            if (RoleInput.Focus == ControlFocus.A) return; // 기능 테스트 A만 조작 중이면 B 표시는 뺀다
            if (RoleInput.Dial.IsOpen) return; // 다이얼이 열리면 다이얼이 대신 보여 준다 (SkillHUD.SpellDial.cs)

            var spell = SpellBook.Get(RoleInput.MouseSpell, RoleInput.SelectedSlot(RoleInput.MouseSpell));
            if (spell == null) return;
            if (mouseSpellStyle == null)
                mouseSpellStyle = new GUIStyle(small) { wordWrap = false, fontStyle = FontStyle.Bold };

            Vector2 mouse = P2Input.mousePosition; // 온라인이면 친구 커서 자리
            var pos = new Vector2(mouse.x + 16f, Screen.height - mouse.y + 14f); // GUI 좌표는 위가 0
            var content = new GUIContent($"우클릭 {spell.Name}");
            Vector2 size = mouseSpellStyle.CalcSize(content);
            pos.x = Mathf.Min(pos.x, Screen.width - size.x - 4f);
            pos.y = Mathf.Min(pos.y, Screen.height - size.y - 4f);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f); // 글자 뒤 그림자 (배경이 밝아도 읽히게)
            GUI.Label(new Rect(pos.x + 1f, pos.y + 1f, size.x, size.y), content, mouseSpellStyle);
            GUI.color = ElementInfo.Color(spell.Element);
            GUI.Label(new Rect(pos.x, pos.y, size.x, size.y), content, mouseSpellStyle);
            GUI.color = previous;
        }
    }
}
