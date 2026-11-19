using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Net
{
    // P2 "여기!" 신호: P2(공격)가 마우스 가운데 버튼을 누르면 커서 자리에 주황 고리 + "여기!"가 2.5초 동안 뜬다.
    // 온라인이면 친구 마우스 가운데 버튼(P2Input)이고, 호스트 씬에 생긴 그림이라 두 화면 모두에 보인다 (NetWorld가 옮긴다).
    // 목소리 없이도 "저 적 먼저", "이쪽 길" 같은 걸 가리킬 수 있게 (Docs/OnlinePlay.md).
    // 판 도중(플레이어가 있고 시간이 흐를 때)에만. 0.4초마다 하나, 한 번에 3개까지 (넷째는 가장 오래된 것을 지운다).
    public class P2PingMarker : MonoBehaviour
    {
        public const float Lifetime = 2.5f;
        private const float Cooldown = 0.4f;
        private const int MaxMarkers = 3;
        private static readonly Color P2Orange = new Color(1f, 0.65f, 0.35f);
        private static readonly Color Outline = new Color(0.094f, 0.078f, 0.102f); // ArtStyle #18141a

        private static Sprite ringSprite;
        private static Font font;

        private PlayerMovement player;
        private float lastPing = -10f;
        private readonly System.Collections.Generic.List<GameObject> markers = new System.Collections.Generic.List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("P2PingMarker");
            DontDestroyOnLoad(go);
            go.AddComponent<P2PingMarker>();
        }

        private void Update()
        {
            markers.RemoveAll(m => m == null);
            if (NetClientView.Active) return; // 친구 컴퓨터는 게임을 돌리지 않는다 (입력은 호스트로 간다)
            if (!P2Input.GetMouseButtonDown(2)) return;
            if (Time.timeScale <= 0f || Time.unscaledTime - lastPing < Cooldown) return;
            if (!FindPlayer()) return;
            var cam = Camera.main;
            if (cam == null) return;

            var mouse = P2Input.mousePosition;
            mouse.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(mouse);
            world.z = 0f;
            lastPing = Time.unscaledTime;
            if (markers.Count >= MaxMarkers)
            {
                Destroy(markers[0]);
                markers.RemoveAt(0);
            }
            markers.Add(Spawn(world));
            GameAudio.PlaySfx("PingMark", 0.6f, 0.04f);
        }

        private bool FindPlayer()
        {
            if (player != null) return true;
            player = FindAnyObjectByType<PlayerMovement>();
            return player != null;
        }

        private static GameObject Spawn(Vector3 position)
        {
            if (ringSprite == null) ringSprite = MakeRing();
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("P2 Ping");
            go.transform.position = position;
            var ring = new GameObject("Ring");
            ring.transform.SetParent(go.transform, false);
            var sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = P2Orange;
            sr.sortingOrder = 60;

            var shadow = MakeText(go.transform, new Vector3(0.04f, 0.86f, 0f), Outline, 61);
            var label = MakeText(go.transform, new Vector3(0f, 0.9f, 0f), P2Orange, 62);
            go.AddComponent<PingFade>().Init(sr, new[] { label, shadow });
            return go;
        }

        private static TextMesh MakeText(Transform parent, Vector3 local, Color color, int order)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = "여기!";
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = 0.055f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return mesh;
        }

        // 32px 고리 (픽셀 느낌 그대로, 가운데는 비움)
        private static Sprite MakeRing()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "P2PingRing" };
            var pixels = new Color32[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    bool ring = d <= 15f && d >= 11.5f;
                    bool dot = d <= 2.5f;
                    pixels[y * size + x] = ring || dot ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }

    // 처음 0.15초 크게 튀어나왔다가, 고리가 숨 쉬듯 커졌다 작아지고, 마지막 0.6초 동안 옅어지며 사라진다.
    public class PingFade : MonoBehaviour
    {
        private SpriteRenderer ring;
        private TextMesh[] texts;
        private Color[] textColors;
        private Color ringColor;
        private float age;

        public void Init(SpriteRenderer ringRenderer, TextMesh[] labels)
        {
            ring = ringRenderer;
            texts = labels;
            ringColor = ring.color;
            textColors = new Color[texts.Length];
            for (int i = 0; i < texts.Length; i++) textColors[i] = texts[i].color;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime; // 일시정지 중에도 사라진다 (메뉴 위에 남지 않게)
            if (age >= P2PingMarker.Lifetime || ring == null)
            {
                Destroy(gameObject);
                return;
            }
            float pop = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f + 0.12f * Mathf.Sin(age * 10f);
            ring.transform.localScale = new Vector3(pop, pop, 1f);
            float fadeStart = P2PingMarker.Lifetime - 0.6f;
            float alpha = age < fadeStart ? 1f : 1f - (age - fadeStart) / 0.6f;
            var rc = ringColor;
            rc.a *= alpha;
            ring.color = rc;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                var tc = textColors[i];
                tc.a *= alpha;
                texts[i].color = tc;
            }
        }
    }
}
