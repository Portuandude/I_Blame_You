using UnityEngine;

namespace IBlameYou.Systems
{
    // 유물 이름 띠 (VfxPlayer.RelicBanner). 띠가 톡 튀어나와(0.15초) 천천히 떠오르다 마지막 0.5초에 옅어지며 사라진다.
    // 띠 그림: Tools/ArtGen/generate_vfx.py → Resources/Effects/RelicBanner (360x80, 가운데 글자 자리 약 3유닛).
    public class RelicBannerView : MonoBehaviour
    {
        private const float Lifetime = 2.2f;
        private const float FadeTime = 0.5f;
        private const float PopTime = 0.15f;
        private const float RiseSpeed = 0.3f;
        private const float BaseScale = 0.8f;
        private const float GlyphWidth = 0.3f;      // 한 글자 대략 폭 (띠 로컬 유닛, 한글 기준)
        private const float TextRoom = 3.0f;        // 띠 그림에서 글자가 들어갈 폭 (로컬 유닛)
        private static readonly Color TextColor = new Color(0.094f, 0.078f, 0.102f); // ArtStyle outline #18141a

        private static Font font;

        private SpriteRenderer ribbon;
        private TextMesh label;
        private Color ribbonColor;
        private Color labelColor;
        private float age;

        public static RelicBannerView Create(Vector3 position, Sprite ribbonSprite, string text)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("RelicBanner");
            go.transform.position = position;
            go.transform.localScale = Vector3.zero; // 첫 Update에서 팝
            var view = go.AddComponent<RelicBannerView>();

            if (ribbonSprite != null)
            {
                var ribbonGo = new GameObject("Ribbon");
                ribbonGo.transform.SetParent(go.transform, false);
                // 이름이 길면 띠를 가로로 늘린다 (꼬리도 같이 늘어나지만 짧은 이름이 대부분이라 괜찮다)
                float stretch = Mathf.Clamp((text.Length * GlyphWidth + 0.4f) / TextRoom, 1f, 1.6f); // 너무 늘이면 꼬리가 찌그러진다
                ribbonGo.transform.localScale = new Vector3(stretch, 1f, 1f);
                view.ribbon = ribbonGo.AddComponent<SpriteRenderer>();
                view.ribbon.sprite = ribbonSprite;
                view.ribbon.sortingOrder = 9;
                view.ribbonColor = view.ribbon.color;
            }

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.04f, 0f); // 띠 그림의 띠 가운데가 캔버스 가운데보다 4px 아래
            view.label = textGo.AddComponent<TextMesh>();
            view.label.text = text;
            view.label.font = font;
            view.label.fontSize = 48;
            view.label.fontStyle = FontStyle.Bold;
            view.label.characterSize = 0.05f;
            view.label.anchor = TextAnchor.MiddleCenter;
            view.label.alignment = TextAlignment.Center;
            view.label.color = ribbonSprite != null ? TextColor : new Color(1f, 0.847f, 0.302f); // 띠가 없으면 gold 글자
            view.labelColor = view.label.color;
            var renderer = textGo.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 10;

            return view;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float pop = age < PopTime ? Mathf.Sin(age / PopTime * Mathf.PI * 0.5f) * 1.15f
                : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - PopTime) / PopTime));
            transform.localScale = new Vector3(BaseScale * pop, BaseScale * pop, 1f);

            float alpha = 1f - Mathf.Clamp01((age - (Lifetime - FadeTime)) / FadeTime);
            if (ribbon != null)
            {
                var c = ribbonColor;
                c.a *= alpha;
                ribbon.color = c;
            }

            if (label != null)
            {
                var c = labelColor;
                c.a *= alpha;
                label.color = c;
            }

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
