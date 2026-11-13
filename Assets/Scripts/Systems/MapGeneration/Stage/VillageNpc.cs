using IBlameYou.Core;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마을 NPC: 식당 주인(요리사 모자 분홍 슬라임)·상인(깃털 모자 하늘색 슬라임). 건물 문 옆에서 숨 쉬듯 4프레임 Idle,
    // 플레이어가 가까이 오면 머리 위에 말풍선 한 줄 (다가올 때마다 다음 대사). 콜라이더가 없어 길을 막지 않는다.
    // 2026-11-13 반응: 말풍선이 새로 뜰 때 "뽀로롱" 말소리(NpcTalk, 식당 주인 낮게·상인 높게), 판에서 처음 다가오면 인사 + 톡 뛰기
    // (그 전까지 머리 위 금색 "!"), 체력·골드에 따른 상황 대사, 멀어지면 작별 말풍선.
    // 그림: Tools/ArtGen/generate_village.py (NpcChef_0~3, NpcMerchant_0~3, 20x22, 16픽셀 = 1유닛). 슬라임은 눈 없음 규칙.
    public class VillageNpc : MonoBehaviour
    {
        private const float FrameTime = 0.22f;
        private const float BubbleHeight = 2.05f;
        private const float GlyphWidth = 0.3f * BubbleScale; // 한글 한 글자 대략 폭 (RelicBannerView와 같은 글자 크기 기준)
        private const float BubbleScale = 0.55f;
        private const float TalkRange = 2.6f;               // 이 거리 안에 플레이어가 있으면 말풍선
        private const float ReactionTime = 1.5f;           // 사고팔 때 반응 말풍선 시간 (창을 닫은 뒤)
        private const float HopTime = 0.3f;
        private const float HopHeight = 0.25f;
        private const float FarewellTime = 1.2f;           // 말풍선 거리 밖으로 나간 뒤 작별 말풍선 시간
        private const float LeaveSlack = 0.5f;             // 말풍선 거리 밖으로 나갈 때는 이만큼 더 멀어져야 (경계 떨림 방지)
        private const float LowHealthRatio = 0.5f;         // 식당 주인 "창백해요" 대사 (체력 이 비율 이하)
        private const int RichGold = 300;                  // 상인 "주머니가 두둑" 대사 (골드 이 값 이상)
        private const float TalkVolume = 0.8f;
        private static readonly Color ExclaimColor = new Color(1f, 0.84f, 0.3f); // 금색 (RoomDoorMarkers Gold)
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

        // 판에서 처음 다가왔을 때 인사를 한 NPC (스테이지 시드로 판을 구분한다. 방을 다시 불러와도 인사는 한 번만).
        private static readonly System.Collections.Generic.Dictionary<VillageService, int> greetedSeed =
            new System.Collections.Generic.Dictionary<VillageService, int>();

        private Sprite[] frames;
        private SpriteRenderer body;
        private GameObject bubble;
        private TextMesh label;
        private Transform labelBack, labelEdge;
        private string[] lines;
        private int lineIndex = -1;
        private bool near;
        private Vector3 basePosition;
        private VillageService service;
        private string reaction;          // 사고팔 때 받은 대사 (창이 닫혀 게임이 다시 흐르면 보여 준다)
        private float reactionLeft;       // 반응 말풍선이 남은 시간 (게임 시간)
        private PlayerMovement player;
        private HealthSystem playerHealth;
        private float time;
        private float talkPitch;          // 말소리 음높이 (식당 주인 낮게, 상인 높게)
        private float greetHop = -1f;     // 인사 톡 뛰기 경과 시간 (음수면 안 뜀)
        private float farewellLeft;       // 작별 말풍선이 남은 시간
        private string lastLine;          // 바로 전에 다가왔을 때 한 대사 (상황 대사가 매번 반복되지 않게)
        private GameObject exclaim;       // 아직 인사 안 한 NPC 머리 위 금색 "!"

        public static VillageNpc Create(Transform parent, Vector2 groundPosition, VillageService service)
        {
            bool chef = service == VillageService.Restaurant;
            var go = new GameObject(chef ? "Npc_Chef" : "Npc_Merchant");
            go.transform.SetParent(parent, false);
            go.transform.position = groundPosition;

            var npc = go.AddComponent<VillageNpc>();
            npc.basePosition = go.transform.position;
            npc.lines = chef ? ChefLines : MerchantLines;
            npc.service = service;
            npc.talkPitch = chef ? 0.85f : 1.2f;
            StageEvents.VillageNpcSay += npc.OnSay;
            npc.frames = LoadFrames(chef ? "NpcChef" : "NpcMerchant");
            npc.body = go.AddComponent<SpriteRenderer>();
            npc.body.sortingOrder = -2; // 건물(-3) 앞, 플레이어(1) 뒤
            if (npc.frames.Length > 0) npc.body.sprite = npc.frames[0];
            npc.time = Random.value * FrameTime * 4f; // 두 NPC가 같은 박자로 숨 쉬지 않게
            npc.CreateBubble();
            npc.CreateExclaim();
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

        // 아직 인사하지 않은 NPC 머리 위 금색 "!" (검은 그림자 한 겹). 말풍선과 같은 높이라 말풍선이 뜨면 숨긴다.
        private void CreateExclaim()
        {
            exclaim = new GameObject("Exclaim");
            exclaim.transform.SetParent(transform, false);
            exclaim.transform.localPosition = new Vector3(0f, BubbleHeight, 0f);
            ExclaimText("Shadow", OutlineColor, new Vector3(0.05f, -0.05f, 0f), 6);
            ExclaimText("Mark", ExclaimColor, Vector3.zero, 7);
            exclaim.SetActive(false);
        }

        private void ExclaimText(string name, Color color, Vector3 offset, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(exclaim.transform, false);
            go.transform.localPosition = offset;
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.text = "!";
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
        }

        // 새 판이 시작될 때 인사 기록을 지운다 (SkillHUD가 RunGold.Reset과 함께 부른다 — 고정 시드로 다시 해도 다시 인사).
        public static void ResetGreetings() => greetedSeed.Clear();

        private bool Greeted => greetedSeed.TryGetValue(service, out int seed) && seed == StageEvents.StageSeed;

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

        private void OnDestroy() => StageEvents.VillageNpcSay -= OnSay;

        // 창이 열려 게임이 멈춘 동안 받은 대사는 마지막 것만 남겼다가, 창이 닫히면 ReactionTime 동안 보여 준다.
        private void OnSay(VillageService who, string line)
        {
            if (who != service || string.IsNullOrEmpty(line)) return;
            reaction = line;
            reactionLeft = ReactionTime;
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (frames != null && frames.Length > 0)
                body.sprite = frames[(int)(time / FrameTime) % frames.Length];

            bool nowNear = PlayerNear();
            if (nowNear && !near)
            {
                farewellLeft = 0f;
                Say();
            }
            else if (!nowNear && near && player != null && reaction == null)
                Farewell(); // 멀어지면 작별 한마디 (반응 말풍선이 떠 있으면 그대로 둔다)
            near = nowNear;
            if (farewellLeft > 0f) farewellLeft -= Time.deltaTime;

            float hop = 0f;
            if (greetHop >= 0f)
            {
                greetHop += Time.deltaTime;
                if (greetHop < HopTime) hop = Mathf.Sin(greetHop / HopTime * Mathf.PI) * HopHeight;
                else greetHop = -1f;
            }
            if (reaction != null && Time.timeScale > 0f)
            {
                if (reactionLeft == ReactionTime) Speak(reaction); // 첫 프레임에 대사를 바꿔 끼운다
                reactionLeft -= Time.deltaTime;
                float t = ReactionTime - reactionLeft;
                if (t < HopTime) hop = Mathf.Max(hop, Mathf.Sin(t / HopTime * Mathf.PI) * HopHeight); // 톡 뛰기 한 번
                if (reactionLeft <= 0f)
                {
                    reaction = null;
                    if (near && lastLine != null) SetText(lastLine); // 다시 다가왔을 때 한 대사로
                }
            }
            transform.position = basePosition + Vector3.up * hop; // 말풍선은 자식이라 같이 뛴다

            bool show = near || reaction != null && Time.timeScale > 0f || farewellLeft > 0f;
            bubble.SetActive(show);
            if (show) bubble.transform.localPosition = new Vector3(0f, BubbleHeight + Mathf.Sin(time * 3f) * 0.04f, 0f);

            // 아직 인사하지 않았으면 머리 위 "!"가 둥실둥실 (말풍선이 뜨면 숨김)
            bool mark = !show && !Greeted;
            exclaim.SetActive(mark);
            if (mark) exclaim.transform.localPosition = new Vector3(0f, BubbleHeight + 0.1f + Mathf.Sin(time * 4f) * 0.08f, 0f);
        }

        private bool PlayerNear()
        {
            // 두 사람이 한 몸(A/B 협동)을 움직이므로 플레이어는 하나. 죽었다 다시 생기면 다시 찾는다.
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerMovement>();
                playerHealth = player != null ? player.GetComponent<HealthSystem>() : null;
            }
            if (player == null) return false;
            float range = near ? TalkRange + LeaveSlack : TalkRange; // 경계에서 서성여도 인사·작별이 번갈아 수다스럽지 않게
            return ((Vector2)player.transform.position - (Vector2)basePosition).sqrMagnitude <= range * range;
        }

        private void Say()
        {
            string line;
            if (!Greeted)
            {
                // 판에서 처음 다가옴: 인사 + 톡 뛰기
                greetedSeed[service] = StageEvents.StageSeed;
                line = service == VillageService.Restaurant ? "어서 와요!" : "어서 오세요, 손님!";
                greetHop = 0f;
            }
            else
            {
                string situation = SituationLine();
                if (situation != null && situation != lastLine) line = situation; // 상황 대사는 평소 대사와 번갈아
                else
                {
                    lineIndex = (lineIndex + 1) % lines.Length;
                    line = lines[lineIndex];
                }
            }
            lastLine = line;
            if (reaction == null) Speak(line); // 반응 말풍선이 떠 있으면 그 대사를 덮지 않는다
        }

        // 플레이어 상황에 맞는 한마디 (없으면 null): 식당 주인은 체력이 낮을 때, 상인은 골드가 없거나 많을 때.
        private string SituationLine()
        {
            if (service == VillageService.Restaurant)
            {
                if (playerHealth != null && playerHealth.Max > 0f && playerHealth.Current <= playerHealth.Max * LowHealthRatio)
                    return "얼굴이 창백해요! 뭐라도 먹어요";
                return null;
            }
            if (RunStats.Gold <= 0) return "구경만 해도 괜찮아요~";
            if (RunStats.Gold >= RichGold) return "주머니가 두둑하시네요!";
            return null;
        }

        private void Farewell()
        {
            farewellLeft = FarewellTime;
            Speak(service == VillageService.Restaurant ? "또 와요~" : "또 오세요!");
        }

        // 말풍선 글을 바꾸고 말소리 "뽀로롱" (NPC마다 음높이가 다르다)
        private void Speak(string text)
        {
            SetText(text);
            GameAudio.PlaySfxPitched("NpcTalk", talkPitch, TalkVolume);
        }

        private void SetText(string text)
        {
            label.text = text;
            float width = text.Length * GlyphWidth + 0.4f;
            labelBack.localScale = new Vector3(width, 0.5f, 1f);
            labelEdge.localScale = new Vector3(width + 0.12f, 0.62f, 1f);
        }
    }
}
