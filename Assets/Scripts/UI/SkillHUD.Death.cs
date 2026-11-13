using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 플레이어 사망 연출 (사망 애니메이션이 생기기 전까지의 대체 연출).
    // 1) 쓰러진 순간 슬로모션 (시간 0.25배, 실제 시간 1.2초 뒤 정상 속도로 복귀)
    // 2) 캐릭터가 회색으로 바래며 반투명해짐
    // 3) 쓰러진 캐릭터 둘레만 동그랗게 남기고 화면이 서서히 어두워지며(원이 좁아짐) 그 뒤 게임오버 창이 떠오름. 창이 다 뜨기 전에는 R/M이 먹지 않는다 (연타로 바로 넘어가지 않게).
    // 6) 캐릭터 그림(Visual)이 살짝 튀어 올랐다가 뒤로 넘어가 옆으로 눕는다. 그림만 돌리고 충돌체는 그대로 (PlayerSquash는 끈다)
    // 5) 몸에서 하늘색 영혼 별(마나 별 그림 UI/HUD/Star)이 좌우로 흔들리며 떠올라 사라진다 (어둠 위, 게임오버 창 뒤)
    // 0) 쓰러진 순간 화면이 하얗게 번쩍(0.15초)하고 카메라가 짧게 흔들린다
    // 4) 쓰러짐 소리 PlayerDown (Tools/AudioGen), 음악은 테이프처럼 느려지며(빠르기 0.6) 먹먹해진다. 곡은 게임오버 창이 뜰 때 꺼지고(SkillHUD.EndScreen.cs), 먹먹함은 HUD가 정리될 때 풀린다 (SkillHUD.OnDestroy)
    // 연출 시간은 모두 Time.unscaledTime 기준이라 슬로모션의 영향을 받지 않는다.
    public partial class SkillHUD
    {
        private const float DeathSlowScale = 0.25f;
        private const float DeathSlowDuration = 1.2f;   // 실제 시간(초)
        private const float DeathTintDuration = 1.0f;
        private const float DeathFadeDelay = 0.3f;
        private const float DeathFadeDuration = 1.2f;
        private const float DeathFadeAlpha = 0.72f;     // 조명 원 바깥 어둠 (원 안은 밝게 남는다)
        private const float DeathSpotEndRadius = 120f;  // 다 좁혀졌을 때 조명 원 반지름 (화면 픽셀)
        private const int DeathSpotTextureSize = 128;
        private const float SoulStart = 0.35f;          // 쓰러지고 영혼 별이 나오기 시작할 때까지 (실제 시간)
        private const float SoulDuration = 1.5f;
        private const float SoulRise = 90f;             // 떠오르는 높이 (화면 픽셀)
        private const float SoulSize = 22f;
        private const float FallHopHeight = 0.35f;      // 튀어 오르는 높이 (월드 단위)
        private const float FallHopTime = 0.45f;        // 실제 시간
        private const float FallTurnStart = 0.08f;
        private const float FallTurnTime = 0.42f;
        private const float FallAngle = 90f;            // 양수 = 뒤로 (HitStun 젖히기와 같은 쪽, 루트 좌우 반전이 방향도 뒤집는다)
        private static readonly Color SoulColor = Color.white; // 그림 색(하늘색) 그대로
        private const float GameOverDelay = 1.4f;       // 쓰러지고 게임오버 창이 뜨기 시작할 때까지
        private const float GameOverFadeIn = 0.4f;

        private const float DeathFlashTime = 0.15f;     // 실제 시간
        private const float DeathFlashAlpha = 0.55f;
        private const float DeathShakeTime = 0.08f;     // 카메라는 게임 시간으로 흔들므로 슬로모션(0.25배) 동안 실제 약 0.3초
        private const float DeathShakeMagnitude = 0.14f;
        private const float DeathMusicPitch = 0.6f;     // GameAudio가 초당 0.5씩 옮기므로 약 0.8초에 걸쳐 느려진다

        private static readonly Color DeadTint = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        private float deathStartTime;
        private bool deathSlowMotion;
        private SpriteRenderer deathVisual;
        private Color deathVisualStart = Color.white;
        private Transform fallVisual;      // 눕힐 그림 (Visual 자식, 없으면 눕히지 않음)
        private Vector3 fallStartPosition;
        private Vector3 fallLyingPosition; // 다 누웠을 때 발 높이에 몸 옆면이 닿는 위치

        private static Texture2D deathSpotTexture; // 가운데가 뚫린 둥근 어둠 (런타임에 한 번 만든다)

        private float DeathElapsed => Time.unscaledTime - deathStartTime;

        // 게임오버 창이 다 떠서 R/M 입력을 받을 수 있는지.
        private bool GameOverReady => deathHandled && DeathElapsed >= GameOverDelay + GameOverFadeIn;

        private void BeginDeathSequence()
        {
            deathStartTime = Time.unscaledTime;
            deathSlowMotion = true;
            Time.timeScale = DeathSlowScale;

            deathVisual = health.GetComponentInChildren<SpriteRenderer>();
            if (deathVisual != null) deathVisualStart = deathVisual.color;
            BeginFall();
            CameraFollow.ShakeMain(DeathShakeTime, DeathShakeMagnitude);
            GameAudio.PlaySfx("PlayerDown", 0.85f, 0f);
            GameAudio.SetMusicPitch(DeathMusicPitch); // 다음 곡을 틀면 1로 돌아간다
            GameAudio.SetMusicMuffled(true);          // 씬이 정리될 때 OnDestroy가 되돌린다 (SkillHUD.cs)
        }

        private void UpdateDeathSequence()
        {
            if (!deathHandled) return;

            float elapsed = DeathElapsed;
            if (deathSlowMotion && elapsed >= DeathSlowDuration) EndDeathSlowMotion();

            if (deathVisual != null)
                deathVisual.color = Color.Lerp(deathVisualStart, DeadTint, Mathf.Clamp01(elapsed / DeathTintDuration));
            UpdateFall(elapsed);
        }

        private void BeginFall()
        {
            fallVisual = health.transform.Find("Visual");
            if (fallVisual == null) return;
            var squash = health.GetComponent<IBlameYou.Player.PlayerSquash>();
            if (squash != null) squash.enabled = false; // 크기·위치를 원래대로 돌려놓고 더는 덮어쓰지 않는다 (OnDisable)

            fallStartPosition = fallVisual.localPosition;
            fallLyingPosition = fallStartPosition;
            var sprite = fallVisual.GetComponent<SpriteRenderer>();
            var movement = health.GetComponent<IBlameYou.Player.PlayerMovement>();
            if (sprite == null || sprite.sprite == null || movement == null) return;

            // 90도(반시계) 돌리면 그림 안의 점 (x, y)는 (-sy·y, sx·x)로 간다.
            // 옆면(왼쪽 가장자리)이 발 높이에 닿게 내리고, 누운 몸이 원래 자리 가운데 오게 옆으로 민다.
            var bounds = sprite.sprite.bounds;
            var scale = fallVisual.localScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y);
            fallLyingPosition = new Vector3(
                fallStartPosition.x + sy * bounds.center.y,
                movement.FeetLocalY - sx * (bounds.center.x - bounds.extents.x),
                fallStartPosition.z);
        }

        private void UpdateFall(float elapsed)
        {
            if (fallVisual == null) return;

            float turn = Mathf.Clamp01((elapsed - FallTurnStart) / FallTurnTime);
            float eased = 1f - (1f - turn) * (1f - turn) * (1f - turn); // 빨리 넘어가다 끝에서 천천히
            float hop = elapsed < FallHopTime ? Mathf.Sin(Mathf.PI * elapsed / FallHopTime) * FallHopHeight : 0f;
            fallVisual.localRotation = Quaternion.Euler(0f, 0f, FallAngle * eased);
            fallVisual.localPosition = Vector3.Lerp(fallStartPosition, fallLyingPosition, eased) + Vector3.up * hop;
        }

        private void EndDeathSlowMotion()
        {
            if (!deathSlowMotion) return;
            deathSlowMotion = false;
            Time.timeScale = 1f;
        }

        private void DrawDeathFade()
        {
            if (!deathHandled) return;

            var previous = GUI.color;
            float alpha = DeathFadeAlpha * Mathf.Clamp01((DeathElapsed - DeathFadeDelay) / DeathFadeDuration);
            if (alpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, alpha);
                DrawDeathSpotlight();
            }

            // 쓰러진 순간 하얀 번쩍임 (어둠보다 먼저 끝난다)
            float flash = DeathFlashAlpha * (1f - Mathf.Clamp01(DeathElapsed / DeathFlashTime));
            if (flash > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, flash);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            DrawDeathSoul();
        }

        // 쓰러진 캐릭터를 가운데 둔 조명 원 바깥을 어둡게 칠한다 (GUI.color에 어둠 색을 넣고 부른다).
        // 원 반지름은 화면 대각선에서 DeathSpotEndRadius까지 어둠이 짙어지는 동안 함께 좁아진다.
        private void DrawDeathSpotlight()
        {
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!TryDeathScreenPoint(out var center))
            {
                GUI.DrawTexture(full, Texture2D.whiteTexture);
                return;
            }
            float t = Mathf.Clamp01((DeathElapsed - DeathFadeDelay) / DeathFadeDuration);
            float startRadius = new Vector2(Screen.width, Screen.height).magnitude;
            float radius = Mathf.Lerp(startRadius, DeathSpotEndRadius, 1f - (1f - t) * (1f - t)); // 처음엔 빠르게, 끝에서 천천히

            var hole = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            GUI.DrawTexture(hole, DeathSpotTexture);
            // 원 상자 바깥 네 조각 (위·아래는 화면 가로 전체, 왼쪽·오른쪽은 원 상자 높이만)
            DrawDeathBlock(new Rect(0f, 0f, Screen.width, hole.yMin));
            DrawDeathBlock(new Rect(0f, hole.yMax, Screen.width, Screen.height - hole.yMax));
            DrawDeathBlock(new Rect(0f, hole.yMin, hole.xMin, hole.height));
            DrawDeathBlock(new Rect(hole.xMax, hole.yMin, Screen.width - hole.xMax, hole.height));
        }

        // 쓰러진 캐릭터 몸 가운데의 GUI 좌표 (카메라가 없으면 false).
        private bool TryDeathScreenPoint(out Vector2 point)
        {
            point = Vector2.zero;
            var cam = Camera.main;
            if (cam == null || health == null) return false;
            var screen = cam.WorldToScreenPoint(health.transform.position + Vector3.up * 0.5f);
            point = new Vector2(screen.x, Screen.height - screen.y); // GUI는 y가 아래로
            return true;
        }

        // 영혼 별: 몸에서 떠올라 좌우로 흔들리며 커졌다가 흐려진다.
        private void DrawDeathSoul()
        {
            float t = (DeathElapsed - SoulStart) / SoulDuration;
            if (t <= 0f || t >= 1f || starIcon == null || !TryDeathScreenPoint(out var body)) return;

            float alpha = Mathf.Min(Mathf.Clamp01(t / 0.12f), Mathf.Clamp01((1f - t) / 0.35f));
            float rise = SoulRise * (1f - (1f - t) * (1f - t)); // 처음엔 빠르게, 위에서 느리게
            float sway = Mathf.Sin(t * Mathf.PI * 3f) * 10f * (1f - t * 0.5f);
            float size = SoulSize * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t / 0.25f));
            var center = new Vector2(body.x + sway, body.y - rise);

            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Sin(t * Mathf.PI * 3f) * 12f, center); // 흔들리는 쪽으로 살짝 기울기
            GUI.color = new Color(SoulColor.r, SoulColor.g, SoulColor.b, alpha * previousColor.a);
            GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), starIcon, ScaleMode.ScaleToFit, true);
            // 뒤에 남는 작은 반짝이 두 개
            for (int i = 1; i <= 2; i++)
            {
                float back = i * 0.06f;
                if (t - back <= 0f) continue;
                float tb = t - back;
                float riseB = SoulRise * (1f - (1f - tb) * (1f - tb));
                float swayB = Mathf.Sin(tb * Mathf.PI * 3f) * 10f * (1f - tb * 0.5f);
                float sb = size * (0.45f - i * 0.12f);
                GUI.color = new Color(SoulColor.r, SoulColor.g, SoulColor.b, alpha * (0.6f - i * 0.2f) * previousColor.a);
                GUI.DrawTexture(new Rect(body.x + swayB - sb / 2f, body.y - riseB - sb / 2f + size * 0.3f, sb, sb), starIcon, ScaleMode.ScaleToFit, true);
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void DrawDeathBlock(Rect rect)
        {
            // 원 상자가 화면 밖으로 넘친 쪽은 크기가 0 이하가 되어 그리지 않는다
            if (rect.width <= 0f || rect.height <= 0f) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        // 하얀색, 가운데는 투명하고 가장자리로 갈수록 불투명 (반지름 55%부터 부드럽게, 모서리는 완전히 불투명).
        private static Texture2D DeathSpotTexture
        {
            get
            {
                if (deathSpotTexture != null) return deathSpotTexture;
                int size = DeathSpotTextureSize;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                        byte a = (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d)));
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                deathSpotTexture = texture;
                return texture;
            }
        }

        // 게임오버 창의 불투명도 (0이면 아직 그리지 않는다).
        private float GameOverAlpha => Mathf.Clamp01((DeathElapsed - GameOverDelay) / GameOverFadeIn);
    }
}
