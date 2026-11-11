using UnityEngine;

namespace IBlameYou.Systems
{
    // 피해량을 맞은 자리 위에 잠깐 띄운다. 데미지 공식 검증용으로 숫자가 바로 보이게 한다.
    public static class DamageNumbers
    {
        private static readonly Color OutlineColor = new Color(0.094f, 0.078f, 0.102f); // ArtStyle outline #18141a
        private static readonly Color Gold = new Color(1f, 0.847f, 0.302f);             // ArtStyle gold #ffd84d
        private const float OutlineOffset = 0.035f;

        private static Font font;

        // 물리 피해는 흰색, 속성 피해는 속성색. 치명타는 1.5배 크기에 물리면 금색 글자, 속성이면 속성색 글자 + 금색 테두리.
        public static void Show(Vector3 position, float amount, bool critical, DamageKind kind, Element element)
        {
            bool physical = kind == DamageKind.Physical;
            var color = physical ? Color.white : ElementInfo.Color(element);
            string text = Mathf.RoundToInt(amount).ToString();
            if (critical)
            {
                text += "!";
                if (physical) color = Gold;
            }

            ShowText(position + new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0f), text, color, critical ? 1.5f : 1f,
                critical && !physical ? Gold : OutlineColor);
        }

        // 실제로 찬 체력을 머리 위에 초록 숫자 "+N"으로 (가득 차 있어서 0.5 미만이면 띄우지 않는다).
        // feet: 플레이어 발밑. 같은 프레임에 여러 번 찼으면(중간보스 + 용사의 훈장, 처치 + 레벨업 등) 조금씩 위로 쌓아 겹치지 않게 한다.
        // label을 주면 "체력 +N"처럼 앞에 붙이고 조금 작게 쓴다.
        public static void ShowHeal(Vector3 feet, float gained, string label = "")
        {
            if (gained < 0.5f) return;
            healStack = healFrame == Time.frameCount ? healStack + 1 : 0;
            healFrame = Time.frameCount;
            ShowText(feet + Vector3.up * (HealHeight + HealStep * healStack), $"{label}+{gained:0}", HealColor,
                string.IsNullOrEmpty(label) ? 1f : 0.8f);
        }

        public const float HealHeight = 2.3f; // 훈장 메달(1.5 → 2.05)보다 위
        private const float HealStep = 0.45f;
        private static readonly Color HealColor = new Color(0.45f, 0.95f, 0.5f);
        private static int healFrame = -1;
        private static int healStack;

        public static void ShowText(Vector3 position, string text, Color color, float scale = 1f)
        {
            ShowText(position, text, color, scale, OutlineColor);
        }

        // 굵은 글자 + 사방 4겹 테두리(같은 글자를 살짝 비켜 뒤에 깐다). 처음 0.1초 동안 커졌다 줄어드는 팝.
        public static void ShowText(Vector3 position, string text, Color color, float scale, Color outline)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("FloatingText");
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            CreateMesh(go, text, color, 50);

            var offsets = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) };
            foreach (var o in offsets)
            {
                var child = new GameObject("Outline");
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = new Vector3(o.x * OutlineOffset, o.y * OutlineOffset, 0f);
                CreateMesh(child, text, outline, 49);
            }

            go.AddComponent<FloatingText>();
        }

        private static void CreateMesh(GameObject go, string text, Color color, int sortingOrder)
        {
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder;
        }
    }

    // 위로 떠오르면서 사라지는 글자.
    public class FloatingText : MonoBehaviour
    {
        private const float Lifetime = 0.8f;
        private const float RiseSpeed = 1.5f;

        private const float PopTime = 0.1f;
        private const float PopScale = 1.3f;

        private TextMesh[] meshes;
        private Color[] baseColors;
        private Vector3 baseScale;
        private float age;

        private void Start()
        {
            // 테두리 자식은 ShowText에서 AddComponent<FloatingText>보다 먼저 붙으므로 여기서 함께 모은다
            meshes = GetComponentsInChildren<TextMesh>();
            baseColors = new Color[meshes.Length];
            for (int i = 0; i < meshes.Length; i++) baseColors[i] = meshes[i].color;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (meshes == null) return;
            age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float pop = age < PopTime ? Mathf.Lerp(PopScale, 1f, age / PopTime) : 1f;
            transform.localScale = baseScale * pop;

            float alpha = 1f - Mathf.Clamp01(age / Lifetime);
            for (int i = 0; i < meshes.Length; i++)
            {
                var color = baseColors[i];
                color.a *= alpha;
                meshes[i].color = color;
            }

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
