using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마을 NPC: 식당 주인(요리사 모자 분홍 슬라임)·상인(깃털 모자 하늘색 슬라임). 건물 문 옆에서 숨 쉬듯 4프레임 Idle,
    // 플레이어가 가까이 오면 머리 위에 말풍선 한 줄 (다가올 때마다 다음 대사). 콜라이더가 없어 길을 막지 않는다.
    // 그림: Tools/ArtGen/generate_village.py (NpcChef_0~3, NpcMerchant_0~3, 20x22, 16픽셀 = 1유닛). 슬라임은 눈 없음 규칙.
    public class VillageNpc : MonoBehaviour
    {
        private const float FrameTime = 0.22f;
        private const float BubbleHeight = 2.05f;
        private const float GlyphWidth = 0.3f * BubbleScale; // 한글 한 글자 대략 폭 (RelicBannerView와 같은 글자 크기 기준)
        private const float BubbleScale = 0.55f;
        private const float TalkRange = 2.6f;               // 이 거리 안에 플레이어가 있으면 말풍선
        private static readonly Color BubbleColor = new Color(1f, 0.957f, 0.839f);   // cream
        private static readonly Color OutlineColor = new Color(0.094f, 0.078f, 0.102f); // outline #18141a

        private static readonly string[] ChefLines =
        {
            "배고프죠? 따끈한 거 있어요!", "든든하게 먹고 가요~", "오늘의 추천: 마나 수프!", "골드만 있으면 배부르게!"
        };
        private static readonly string[] MerchantLines =
        {
            "좋은 물건 들어왔어요!", "수식어는 운이에요, 운!", "안 쓰는 장비는 사 드려요", "구경은 공짜랍니다~"
        };

        private Sprite[] frames;
        private SpriteRenderer body;
        private GameObject bubble;
        private TextMesh label;
        private Transform labelBack, labelEdge;
        private string[] lines;
        private int lineIndex = -1;
        private bool near;
        private PlayerMovement player;
        private float time;

        public static VillageNpc Create(Transform parent, Vector2 groundPosition, VillageService service)
        {
            bool chef = service == VillageService.Restaurant;
            var go = new GameObject(chef ? "Npc_Chef" : "Npc_Merchant");
            go.transform.SetParent(parent, false);
            go.transform.position = groundPosition;

            var npc = go.AddComponent<VillageNpc>();
            npc.lines = chef ? ChefLines : MerchantLines;
            npc.frames = LoadFrames(chef ? "NpcChef" : "NpcMerchant");
            npc.body = go.AddComponent<SpriteRenderer>();
            npc.body.sortingOrder = -2; // 건물(-3) 앞, 플레이어(1) 뒤
            if (npc.frames.Length > 0) npc.body.sprite = npc.frames[0];
            npc.time = Random.value * FrameTime * 4f; // 두 NPC가 같은 박자로 숨 쉬지 않게
            npc.CreateBubble();
            return npc;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite[]> frameCache =
            new System.Collections.Generic.Dictionary<string, Sprite[]>();

        // 방을 다시 불러올 때마다 Sprite를 새로 만들지 않게 이름별로 한 번만 만든다 (리뷰 LOW)
        private static Sprite[] LoadFrames(string name)
        {
            if (frameCache.TryGetValue(name, out var cached) && cached.Length > 0 && cached[0] != null) return cached;
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 4; i++)
            {
                var tex = Resources.Load<Texture2D>($"Village/{name}_{i}");
                if (tex == null) break;
                tex.filterMode = FilterMode.Point;
                list.Add(Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0f), VillageBuilding.PixelsPerUnit));
            }
            var frames = list.ToArray();
            frameCache[name] = frames;
            return frames;
        }

        private void CreateBubble()
        {
            bubble = new GameObject("Bubble");
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = new Vector3(0f, BubbleHeight, 0f);

            labelEdge = Square("Edge", OutlineColor, 6);
            labelBack = Square("Back", BubbleColor, 7);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bubble.transform, false);
            textGo.transform.localScale = new Vector3(BubbleScale, BubbleScale, 1f);
            label = textGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.characterSize = 0.05f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = OutlineColor;
            var renderer = textGo.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 8;
            bubble.SetActive(false);
        }

        private Transform Square(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bubble.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = color;
            renderer.sortingOrder = order;
            return go.transform;
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (frames != null && frames.Length > 0)
                body.sprite = frames[(int)(time / FrameTime) % frames.Length];

            bool nowNear = PlayerNear();
            if (nowNear && !near) Say();
            near = nowNear;
            bubble.SetActive(near);
            if (near) bubble.transform.localPosition = new Vector3(0f, BubbleHeight + Mathf.Sin(time * 3f) * 0.04f, 0f);
        }

        private bool PlayerNear()
        {
            // 두 사람이 한 몸(A/B 협동)을 움직이므로 플레이어는 하나. 죽었다 다시 생기면 다시 찾는다.
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();
            if (player == null) return false;
            return ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude <= TalkRange * TalkRange;
        }

        private void Say()
        {
            lineIndex = (lineIndex + 1) % lines.Length;
            string text = lines[lineIndex];
            label.text = text;
            float width = text.Length * GlyphWidth + 0.4f;
            labelBack.localScale = new Vector3(width, 0.5f, 1f);
            labelEdge.localScale = new Vector3(width + 0.12f, 0.62f, 1f);
        }
    }
}
