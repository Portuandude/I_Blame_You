using System;
using IBlameYou.Core;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 정예 전투 구역을 돌파하면 회복 구슬 옆에 나오는 유물 상자. 닿으면 HUD가 유물 3개 중 하나를 고르는 창을 연다 (게임 정지).
    // 고르면 유물을 얻고 상자는 사라진다. 고르지 못하고 창이 닫히면(쓰러짐 등) 상자는 그대로 남는다.
    // 줍지 않고 떠나도 청크 기록(ChunkStreamer)에 남아서, 다시 돌아오면 같은 자리에 또 있다.
    public class RelicChest : MonoBehaviour
    {
        // HUD가 등록한다: (후보, 고른 유물을 받을 콜백, 고르지 않고 닫혔을 때 콜백) → 창을 열었으면 true.
        // 영창·미니게임 중처럼 지금 열 수 없으면 false를 돌려주고, 상자는 플레이어가 닿아 있는 동안 다시 묻는다.
        public static Func<RelicId[], Action<RelicId>, Action, bool> OpenRequested;

        // HUD가 등록한다: 지금 창을 열 수 있는지 (후보를 계산하기 전에 가볍게 묻는다).
        public static Func<bool> CanOpen;

        private const float VisualScale = 1.3f;
        private const float VisualHeight = 0.45f;
        private const float BannerHeight = 2.3f; // 이름 띠는 빛기둥 중간쯤

        private int seed;
        private int chunkIndex;
        private Action onTaken;
        private bool waiting; // 창이 열려 고르기를 기다리는 중
        private bool taken;
        private PlayerMovement touching; // 지금 상자에 닿아 있는 플레이어 (잠든 Rigidbody는 Stay 콜백이 멈추므로 Update에서 다시 묻는다)

        // 떨어지는 등장 (보상 연출, 2026-11-13): 방금 나온 상자는 그림만 DropHeight 위에서 떨어져 "쿵" 착지하고 납작하게 눌렸다 돌아온다.
        // 판정(트리거)은 처음부터 바닥에 있어 떨어지는 중에 닿아도 바로 열린다.
        private const float DropHeight = 3f;
        private const float DropTime = 0.32f;
        private const float LandSquashTime = 0.22f;
        private const float LandSquash = 0.22f; // 착지 순간 세로 -22%
        private Transform visual;
        private float dropElapsed = -1f; // 0 이상이면 떨어지거나 눌리는 중

        // 들썩임 (보상 연출, 2026-11-13): 열지 않은 상자에 플레이어가 NearRadius 안으로 오면 HopInterval마다 살짝 뛰며 좌우로 기우뚱.
        private const float NearRadius = 2.5f;
        private const float NearCheckInterval = 0.2f;
        private const float HopInterval = 1.1f;
        private const float HopTime = 0.28f;
        private const float HopHeight = 0.16f;
        private const float HopTilt = 7f; // 도
        private float nearCheckTimer;
        private bool playerNear;
        private float hopTimer = 0.35f; // 가까이 오고 조금 뒤 첫 들썩
        private float hopElapsed = -1f; // 0 이상이면 뛰는 중
        private float hopSide = 1f;

        // position: 상자가 놓일 바닥 위 가운데 (월드). dropped: 방금 나온 것이면 등장 이펙트를 보여준다.
        public static RelicChest Create(Transform parent, Vector2 position, int seed, int chunkIndex, bool dropped, Action onTaken)
        {
            var go = new GameObject("RelicChest");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 1.6f);
            trigger.offset = new Vector2(0f, 0.8f);

            var chest = go.AddComponent<RelicChest>();
            chest.seed = seed;
            chest.chunkIndex = chunkIndex;
            chest.onTaken = onTaken;

            var visual = EffectLibrary.CreateVisual(go.transform, "Chest", TrapCommon.Frames("RelicChest"), 8f, true,
                new Color(0.95f, 0.75f, 0.3f), 0.8f, 3);
            visual.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
            visual.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            chest.visual = visual.transform;
            VfxPlayer.AttachSparkle(go.transform, VisualHeight + 0.3f); // 열기 전까지 반짝임 (Take에서 상자째 사라짐)

            if (dropped)
            {
                chest.dropElapsed = 0f;
                visual.transform.localPosition = new Vector3(0f, VisualHeight + DropHeight, 0f);
            }
            return chest;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;
            touching = player;
            TryOpen(player);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player != null && player == touching) touching = null;
        }

        // 돌파 직후 상자가 생긴 자리에 플레이어가 가만히 서 있으면(Rigidbody 수면) Enter가 오지 않을 수 있어, 처음 한 번 직접 겹침을 본다.
        private void Start()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            var center = (Vector2)transform.position + box.offset;
            foreach (var hit in Physics2D.OverlapBoxAll(center, box.size, 0f))
            {
                var player = TrapCommon.PlayerFrom(hit);
                if (player != null) { touching = player; break; }
            }
        }

        // 영창 중처럼 열 수 없었던 경우, 닿아 있는 동안 매 프레임 다시 묻는다.
        private void Update()
        {
            UpdateDrop();
            UpdateHop();
            if (touching != null) TryOpen(touching);
        }

        private void UpdateHop()
        {
            if (visual == null || taken || dropElapsed >= 0f) return;

            nearCheckTimer -= Time.deltaTime;
            if (nearCheckTimer <= 0f)
            {
                nearCheckTimer = NearCheckInterval;
                playerNear = false;
                foreach (var hit in Physics2D.OverlapCircleAll((Vector2)transform.position + Vector2.up * VisualHeight, NearRadius))
                {
                    var player = TrapCommon.PlayerFrom(hit);
                    if (player == null) continue;
                    var health = player.GetComponent<HealthSystem>();
                    if (health != null && health.IsDead) continue;
                    playerNear = true;
                    break;
                }
            }

            if (hopElapsed >= 0f)
            {
                hopElapsed += Time.deltaTime;
                float k = Mathf.Clamp01(hopElapsed / HopTime);
                float lift = Mathf.Sin(k * Mathf.PI); // 올라갔다 내려온다
                visual.localPosition = new Vector3(0f, VisualHeight + HopHeight * lift, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, HopTilt * hopSide * lift);
                if (k >= 1f)
                {
                    hopElapsed = -1f;
                    visual.localPosition = new Vector3(0f, VisualHeight, 0f);
                    visual.localRotation = Quaternion.identity;
                }
                return;
            }

            if (!playerNear || waiting)
            {
                hopTimer = 0.35f;
                return;
            }

            hopTimer -= Time.deltaTime;
            if (hopTimer > 0f) return;
            hopTimer = HopInterval;
            hopElapsed = 0f;
            hopSide = -hopSide; // 번갈아 반대쪽으로 기우뚱
        }

        private void UpdateDrop()
        {
            if (dropElapsed < 0f || visual == null) return;
            float before = dropElapsed;
            dropElapsed += Time.deltaTime;

            if (dropElapsed < DropTime)
            {
                float t = dropElapsed / DropTime;
                visual.localPosition = new Vector3(0f, VisualHeight + DropHeight * (1f - t * t), 0f); // 점점 빨라지며 떨어진다
                return;
            }

            if (before < DropTime) Land();

            float k = (dropElapsed - DropTime) / LandSquashTime;
            if (k >= 1f)
            {
                dropElapsed = -1f;
                visual.localPosition = new Vector3(0f, VisualHeight, 0f);
                visual.localScale = new Vector3(VisualScale, VisualScale, 1f);
                return;
            }

            float squash = LandSquash * (1f - k) * (1f - k); // 눌렸다가 부드럽게 돌아온다
            float sy = 1f - squash;
            float sx = 1f + squash * 0.6f;
            // 그림 아래 끝(바닥)이 그대로 있게 가운데를 내린다 (그림 가운데가 바닥 위 VisualHeight = 반 높이)
            visual.localPosition = new Vector3(0f, VisualHeight - VisualHeight * (1f - sy), 0f);
            visual.localScale = new Vector3(VisualScale * sx, VisualScale * sy, 1f);
        }

        private void Land()
        {
            var feet = transform.position;
            VfxPlayer.Dust(feet, 1f);
            EffectLibrary.PlayHit(Element.Neutral, feet + Vector3.up * VisualHeight, 1.4f); // 예전 등장 터짐은 착지 순간으로
            GameAudio.PlaySfx("ChestLand", 0.75f);
            CameraFollow.ShakeMain(0.1f, 0.06f);
        }

        private void TryOpen(PlayerMovement player)
        {
            if (taken || waiting || OpenRequested == null || player == null) return;
            if (CanOpen != null && !CanOpen()) return;

            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;

            var relics = PlayerRelics.For(player.gameObject);
            var offer = RelicBook.Offer(seed, chunkIndex, relics.Owned);
            if (offer.Length == 0)
            {
                // 유물을 다 가졌으면 상자는 빈 채로 사라진다.
                StageEvents.PostNotice("유물 상자가 비어 있다 (모든 유물을 가졌다)");
                Take();
                return;
            }

            waiting = true;
            bool opened = OpenRequested(offer, chosen =>
            {
                waiting = false;
                if (taken || relics == null || !relics.Add(chosen)) return;
                bool firstTime = SaveData.RecordRelic((int)chosen); // 유물 도감 (시작 화면 "유물 도감 N/M", 카드 NEW, 결과 창 새 유물·도감 완성)
                if (firstTime) RunStats.AddNewRelic((int)chosen, SaveData.RelicCodexComplete(GameSession.DevMode, RelicBook.All.Length));
                GameAudio.PlaySfx("RelicOpen", 0.7f);
                if (firstTime) GameAudio.PlaySfxDelayed("RelicNew", 0.32f, 0.6f); // 도감에 처음 오른 유물: 아르페지오 끝에 반짝 화음
                StageEvents.PostNotice($"유물: {RelicBook.Name(chosen)} ({RelicBook.Description(chosen)})");
                VfxPlayer.RelicBanner(transform.position + Vector3.up * BannerHeight, RelicBook.Name(chosen));
                Take();
            }, () => waiting = false);

            if (!opened) waiting = false;
        }

        private void Take()
        {
            taken = true;
            var center = transform.position + Vector3.up * VisualHeight;
            if (!VfxPlayer.ChestOpen(center, VisualScale)) EffectLibrary.PlayHit(Element.Neutral, center, 1.4f); // 열기 그림이 없으면 예전 터짐
            CameraFollow.ShakeMain(0.12f, 0.08f);
            onTaken?.Invoke();
            Destroy(gameObject);
        }
    }
}
