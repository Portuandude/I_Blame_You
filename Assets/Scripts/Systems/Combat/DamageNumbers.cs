using UnityEngine;

namespace IBlameYou.Systems
{
    // 피해량을 맞은 자리 위에 잠깐 띄운다. 데미지 공식 검증용으로 숫자가 바로 보이게 한다.
    public static class DamageNumbers
    {
        private static Font font;

        public static void Show(Vector3 position, float amount, bool critical, DamageKind kind, Element element)
        {
            var color = kind == DamageKind.Physical ? Color.white : ElementInfo.Color(element);
            string text = Mathf.RoundToInt(amount).ToString();
            if (critical) text += "!";
            ShowText(position + new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0f), text, critical ? new Color(1f, 0.9f, 0.2f) : color,
                critical ? 1.3f : 1f);
        }

        public static void ShowText(Vector3 position, string text, Color color, float scale = 1f)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("FloatingText");
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 50;

            go.AddComponent<FloatingText>();
        }
    }

    // 위로 떠오르면서 사라지는 글자.
    public class FloatingText : MonoBehaviour
    {
        private const float Lifetime = 0.8f;
        private const float RiseSpeed = 1.5f;

        private TextMesh mesh;
        private Color baseColor;
        private float age;

        private void Awake()
        {
            mesh = GetComponent<TextMesh>();
            baseColor = mesh.color;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            var color = baseColor;
            color.a = 1f - Mathf.Clamp01(age / Lifetime);
            mesh.color = color;

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
