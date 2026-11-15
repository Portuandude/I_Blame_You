using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 커서 자리 마법(불꽃 비·물안개·운석)을 영창하는 동안(운석은 선딜 동안에도) 떨어질 자리에 범위 테두리를 그린다.
    // 자리는 SpellCaster.CursorLanding과 같은 계산 (사거리 밖이면 사거리 끝으로 줄인 자리, 불꽃 비·운석은 땅에 붙음).
    // 모양은 실제 판정과 같다: 물안개 = 원(MistZone), 불꽃 비 = 땅 위 상자(FireWall), 운석 = 땅 위 낮은 상자(GroundSpike).
    [RequireComponent(typeof(SpellCaster))]
    public class CursorSpellPreview : MonoBehaviour
    {
        private const int CirclePoints = 40;
        private const float LineWidth = 0.07f;
        private const float SpikeHeight = 1.6f; // GroundSpike 판정 상자 높이

        private static Material lineMaterial;

        private SpellCaster caster;
        private LineRenderer line;

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
            var go = new GameObject("CursorSpellPreview");
            go.transform.SetParent(transform, false);
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.material = lineMaterial;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.numCornerVertices = 2;
            line.sortingOrder = 20; // 땅·적 위
            line.enabled = false;
        }

        private void LateUpdate()
        {
            // 영창 중에는 커서를 따라가고, 운석 선딜 중에는 영창을 마친 순간 묶인 자리에 그대로 둔다
            bool windup = caster.WindingUp && caster.WindupSpell != null;
            var spell = windup ? caster.WindupSpell : caster.ChantOpen ? caster.PendingSpell : null;
            if (spell == null || !spell.AtCursor)
            {
                if (line.enabled) line.enabled = false;
                return;
            }

            Vector2 point = windup ? caster.WindupLanding : caster.CursorLanding(spell);
            if (spell.Shape == SpellShape.Mist) SetCircle(point, spell.Radius);
            else
            {
                float height = spell.Shape == SpellShape.FireWall ? (spell.Height > 0f ? spell.Height : FireWall.WallHeight) : SpikeHeight;
                SetBox(point, spell.Radius, height);
            }

            // 속성 색으로 천천히 깜빡임 (영창 중 게임이 느려져도 같은 빠르기)
            var color = ElementInfo.Color(spell.Element);
            color.a = 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            line.startColor = color;
            line.endColor = color;
            line.enabled = true;
        }

        private void SetCircle(Vector2 center, float radius)
        {
            line.positionCount = CirclePoints;
            for (int i = 0; i < CirclePoints; i++)
            {
                float angle = i * Mathf.PI * 2f / CirclePoints;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, 0f));
            }
        }

        // 바닥 가운데 point에서 좌우 halfWidth, 위로 height인 상자
        private void SetBox(Vector2 point, float halfWidth, float height)
        {
            line.positionCount = 4;
            line.SetPosition(0, new Vector3(point.x - halfWidth, point.y, 0f));
            line.SetPosition(1, new Vector3(point.x - halfWidth, point.y + height, 0f));
            line.SetPosition(2, new Vector3(point.x + halfWidth, point.y + height, 0f));
            line.SetPosition(3, new Vector3(point.x + halfWidth, point.y, 0f));
        }
    }
}
