using UnityEngine;

namespace IBlameYou.Systems
{
    // 컨트롤 퍼즐: 양쪽 끝을 일정한 속도로 오가는 발판. 위에 선 플레이어를 함께 실어 나른다.
    // 떨어졌을 때 돌아갈 "안전한 땅"으로는 치지 않는다 (UnsafeGround).
    // 플레이어보다 먼저 FixedUpdate를 돌아야 같은 물리 프레임에 실어 나를 속도를 넘겨줄 수 있다.
    [DefaultExecutionOrder(-100)]
    public class MovingPlatform : MonoBehaviour
    {
        public const float Speed = 2.2f;      // 초당 유닛
        public const float EndPause = 0.8f;   // 양 끝에서 멈춰 있는 시간 (올라타고 내릴 여유)
        private const float Thickness = 0.35f;

        private Rigidbody2D body;
        private BoxCollider2D solid;
        private Vector2 start;
        private Vector2 end;
        private float cycle;
        private float phase;
        private readonly Collider2D[] riders = new Collider2D[8];
        private readonly System.Collections.Generic.HashSet<Rigidbody2D> carried = new System.Collections.Generic.HashSet<Rigidbody2D>();

        // 마법 스위치 발판: 처음엔 출발점에 멈춰 있고, Activate가 불리면 정해진 시간 동안 오간다.
        // 시간이 끝나도 끝점(출발점이나 건너편)에 닿을 때까지는 마저 움직여서 낭떠러지 한가운데 멈추지 않는다.
        // 멈춰 있는 동안은 흐릿한 "잠든 발판"이라 밟을 수 없다 (위에 누가 서 있으면 내릴 때까지 기다린다):
        // 멈춘 발판 위에서 뛰면 낭떠러지가 3칸 줄어 룬 없이 건널 수 있으므로 (2026-11-12 플레이테스트 피드백).
        public const float SwitchStartDelay = 1.2f; // 스위치를 맞히고 발판으로 걸어갈 여유 (멈춰 있던 발판만)
        private bool switchDriven;
        private float clock;       // 스위치 발판의 자체 시계 (움직일 때만 흐른다)
        private float activeUntil; // 이 시각(Time.time)까지 오간다
        private float holdUntil;   // 이 시각까지는 출발을 미룬다
        private SpriteRenderer[] planks;
        private Color plankColor;
        // 깨어남·잠듦 연출 (SwitchPlatformFx, 그림·소리만)
        private const float WakePopTime = 0.3f;
        private const float WakePopHeight = 0.12f;
        private Vector3[] plankPositions;
        private Color switchColor = Color.white;
        private bool everWoke;      // 한 번이라도 깨어났는지 (스테이지 시작 때 잠듦 연출이 나지 않게)
        private bool departPending; // 깨어난 뒤 아직 출발하지 않음
        private float wakeAt = -10f;
        // 도착·출발 연출 (MovingPlatformFx, 그림·소리만): 지난 물리 프레임에 끝에 멈춰 있었는지와 비교한다.
        private const float DipTime = 0.2f;     // 끝에 닿거나 누가 내려앉을 때 판자가 눌렸다 펴지는 시간
        private const float ArriveDip = 0.06f;
        private const float LandDip = 0.08f;
        private const float DepartWarn = 0.3f;  // 멈춤 마지막 이 시간 동안 판자가 살짝 밝아진다
        private bool endPrimed;
        private bool wasAtEnd;
        private float dipAt = -10f;
        private float dipDepth;
        private bool warnTinted;

        public bool SwitchDriven => switchDriven;
        public bool Idle => switchDriven && Time.time >= activeUntil && AtEnd(clock);
        private bool ghost; // 잠든 발판: 콜라이더를 끈 상태
        public Vector2 StartCenter => start;
        public float ActiveRemaining => switchDriven ? Mathf.Max(0f, activeUntil - Time.time) : 0f;

        // topLeft: 출발 위치에서 발판 윗면의 왼쪽 끝 (월드), width: 칸 수, travel: 오른쪽으로 움직이는 칸 수.
        // phaseOffset: 같은 스테이지의 발판들이 같은 박자로 움직이지 않게 (청크 번호로 정한다).
        // switchDriven: 마법 스위치로 움직이는 발판 (출발점에 멈춘 채로 시작, phaseOffset은 쓰지 않는다).
        public static MovingPlatform Create(Transform parent, Vector2 topLeft, int width, int travel, float phaseOffset,
            bool switchDriven = false)
        {
            var go = new GameObject("MovingPlatform");
            go.transform.SetParent(parent, false);
            var startCenter = new Vector2(topLeft.x + width / 2f, topLeft.y - Thickness / 2f);
            go.transform.position = startCenter;
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<UnsafeGround>();

            var platform = go.AddComponent<MovingPlatform>();
            platform.body = go.AddComponent<Rigidbody2D>();
            platform.body.bodyType = RigidbodyType2D.Kinematic;
            platform.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            platform.solid = go.AddComponent<BoxCollider2D>();
            platform.solid.size = new Vector2(width, Thickness);
            platform.solid.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            platform.start = startCenter;
            platform.end = startCenter + new Vector2(Mathf.Max(0, travel), 0f);
            float moveTime = Mathf.Max(0.01f, travel / Speed);
            platform.cycle = 2f * (moveTime + EndPause);
            platform.switchDriven = switchDriven;
            platform.phase = switchDriven ? 0f : Mathf.Repeat(phaseOffset, 1f) * platform.cycle;

            // 첫 물리 프레임에 경로 중간으로 순간이동하며 밀치지 않게, 지금 시각에 있어야 할 자리에서 시작한다.
            var now = Vector2.Lerp(platform.start, platform.end, platform.Progress(platform.CurrentTime()));
            go.transform.position = now;
            platform.body.position = now;

            var sprite = TrapCommon.Frame("Platform");
            platform.planks = new SpriteRenderer[width];
            for (int i = 0; i < width; i++)
            {
                var plank = TrapCommon.AddSprite(go.transform, $"Plank_{i}", sprite, new Vector2(i - (width - 1) / 2f, -0.05f),
                    new Vector2(1f, Thickness + 0.1f), new Color(0.45f, 0.7f, 0.9f), 0);
                if (sprite != null) plank.color = new Color(0.75f, 0.9f, 1f); // 다른 발판과 구분되게 푸른빛
                platform.planks[i] = plank;
            }
            platform.plankColor = platform.planks.Length > 0 ? platform.planks[0].color : Color.white;
            platform.plankPositions = new Vector3[width];
            for (int i = 0; i < width; i++) platform.plankPositions[i] = platform.planks[i].transform.localPosition;
            if (switchDriven) platform.ApplyPlankColor();

            return platform;
        }

        // 스위치 발판의 판자 색 (스위치 속성 색을 섞는다). 멈춰 있을 때는 어둡게.
        public void SetSwitchColor(Color color)
        {
            plankColor = Color.Lerp(plankColor, color, 0.45f);
            switchColor = color;
            ApplyPlankColor();
        }

        private void ApplyPlankColor()
        {
            if (planks == null) return;
            var c = Idle ? new Color(plankColor.r * 0.6f, plankColor.g * 0.6f, plankColor.b * 0.6f, plankColor.a * (ghost ? 0.35f : 1f)) : plankColor;
            foreach (var plank in planks)
                if (plank != null) plank.color = c;
        }

        // 스위치가 맞았다: duration초 동안 오간다. 이미 움직이는 중이면 시간만 늘린다.
        public void Activate(float duration)
        {
            if (!switchDriven) return;
            if (Idle)
            {
                // 잠든 발판이 깨어난다: 판자가 톡 튀고 반짝 조각 + "우웅-팅", 출발 여유 동안 판자가 밝아진다
                everWoke = true;
                departPending = true;
                wakeAt = Time.time;
                SwitchPlatformFx.Wake(body.position, planks != null ? planks.Length : 1, switchColor);
                holdUntil = Time.time + SwitchStartDelay;
                // 끝점에 멈춰 있던 시계를 그 멈춤 구간 끝으로 옮겨, 여유 시간이 지나면 바로 출발한다.
                clock = PauseEnd(clock);
            }
            activeUntil = Mathf.Max(activeUntil, Time.time + duration);
            ghost = false;
            solid.enabled = true; // 잠든 발판이 깨어나 밟을 수 있다 (1.2초 뒤 출발)
            ApplyPlankColor();
        }

        private float CurrentTime() => switchDriven ? clock : Time.time;

        // 시각 t가 양 끝의 멈춤 구간 안인지.
        private bool AtEnd(float t)
        {
            float moveTime = cycle / 2f - EndPause;
            float p = Mathf.Repeat(t + phase, cycle);
            return p < EndPause || (p >= EndPause + moveTime && p < 2f * EndPause + moveTime);
        }

        // t가 멈춤 구간 안이면 그 구간이 끝나는 시각 (바로 앞), 아니면 t 그대로.
        private float PauseEnd(float t)
        {
            float moveTime = cycle / 2f - EndPause;
            float p = Mathf.Repeat(t + phase, cycle);
            float baseTime = t - p;
            const float Epsilon = 0.0001f;
            if (p < EndPause) return baseTime + EndPause - Epsilon;
            if (p >= EndPause + moveTime && p < 2f * EndPause + moveTime) return baseTime + 2f * EndPause + moveTime - Epsilon;
            return t;
        }

        // 0 ~ 1: 출발점 → 끝점. 양 끝에서 EndPause만큼 멈춘다.
        private float Progress(float time)
        {
            float moveTime = cycle / 2f - EndPause;
            float t = Mathf.Repeat(time + phase, cycle);
            if (t < EndPause) return 0f;
            t -= EndPause;
            if (t < moveTime) return Mathf.SmoothStep(0f, 1f, t / moveTime);
            t -= moveTime;
            if (t < EndPause) return 1f;
            t -= EndPause;
            return Mathf.SmoothStep(1f, 0f, t / moveTime);
        }

        private void FixedUpdate()
        {
            if (switchDriven)
            {
                bool wasIdle = Idle;
                if (Time.time >= holdUntil && (Time.time < activeUntil || !AtEnd(clock))) clock += Time.fixedDeltaTime;
                // 잠든 발판은 Activate로만 깨어난다 (꺼진 콜라이더의 bounds는 비어 있어 위를 볼 수 없다).
                bool shouldGhost = Idle && (ghost || !HasRider());
                if (departPending && Time.time >= holdUntil)
                {
                    departPending = false;
                    SwitchPlatformFx.Depart(body.position, planks != null ? planks.Length : 1, Thickness);
                    ApplyPlankColor(); // 출발 여유 동안의 밝아짐을 원래 색으로
                }
                if (shouldGhost != ghost)
                {
                    ghost = shouldGhost;
                    solid.enabled = !ghost;
                    ApplyPlankColor();
                    if (ghost && everWoke) SwitchPlatformFx.Sleep(body.position, planks != null ? planks.Length : 1, switchColor);
                }
                else if (wasIdle != Idle) ApplyPlankColor();
            }
            UpdateEndFx();
            var target = Vector2.Lerp(start, end, Progress(CurrentTime()));
            var delta = target - body.position;
            CarryRiders(delta);
            body.MovePosition(target);
        }

        private void OnEnable() => endPrimed = false;

        // 끝에 닿는 순간·떠나는 순간 (그림·소리만). 처음 프레임·다시 켜진 뒤 첫 프레임은 상태만 기억한다.
        private void UpdateEndFx()
        {
            float t = CurrentTime();
            bool atEnd = AtEnd(t);
            if (!endPrimed)
            {
                endPrimed = true;
                wasAtEnd = atEnd;
                return;
            }
            if (atEnd == wasAtEnd) return;
            wasAtEnd = atEnd;
            int width = planks != null ? planks.Length : 1;
            bool atFar = Progress(t) > 0.5f; // 끝점(오른쪽)에 있는지
            if (atEnd)
            {
                MovingPlatformFx.Arrive(body.position, width, Thickness, atFar ? 1f : -1f);
                Dip(ArriveDip);
            }
            else if (!(switchDriven && Time.time - holdUntil < 0.25f)) // 깨어난 스위치 발판은 SwitchPlatformFx.Depart가 이미 낸다
            {
                MovingPlatformFx.Depart(body.position, width, Thickness, atFar ? 1f : -1f);
            }
        }

        private void Dip(float depth)
        {
            dipAt = Time.time;
            dipDepth = depth;
        }

        // 플레이어가 위로 내려앉는 순간 판자가 살짝 눌렸다 펴진다 (그림만).
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (TrapCommon.PlayerFrom(collision.collider) == null) return;
            if (collision.collider.bounds.min.y < solid.bounds.max.y - 0.12f) return; // 옆이나 아래에서 부딪힘
            if (Mathf.Abs(collision.relativeVelocity.y) < 1f) return; // 거의 떨어지지 않고 미끄러져 올라옴
            Dip(LandDip);
        }

        // 판자 그림: 깨어남 톡 튀기·눌림·출발 직전 밝아짐 (스위치 발판은 출발 여유 동안 점점 빠르게 밝아졌다 어두워진다).
        private void LateUpdate()
        {
            if (planks == null || plankPositions == null) return;
            float since = Time.time - wakeAt;
            float lift = switchDriven && since >= 0f && since < WakePopTime ? Mathf.Sin(since / WakePopTime * Mathf.PI) * WakePopHeight : 0f;
            float sinceDip = Time.time - dipAt;
            if (sinceDip >= 0f && sinceDip < DipTime) lift -= Mathf.Sin(sinceDip / DipTime * Mathf.PI) * dipDepth;
            for (int i = 0; i < planks.Length; i++)
                if (planks[i] != null) planks[i].transform.localPosition = plankPositions[i] + Vector3.up * lift;

            UpdateDepartWarn();
            if (!switchDriven) return;

            if (!departPending || Time.time >= holdUntil) return;
            float p = Mathf.Clamp01(1f - (holdUntil - Time.time) / SwitchStartDelay);
            float cycles = 1.5f * p + 2.5f * p * p; // 갈수록 빨라진다
            float k = 0.5f - 0.5f * Mathf.Cos(cycles * 2f * Mathf.PI);
            var c = Color.Lerp(plankColor, Color.white, 0.45f * k);
            c.a = plankColor.a;
            foreach (var plank in planks)
                if (plank != null) plank.color = c;
        }

        // 멈춤 마지막 DepartWarn초 동안 판자가 살짝 밝아져 곧 떠난다는 걸 알린다. 잠든 스위치 발판·깨어난 직후 출발 여유는 제외.
        private void UpdateDepartWarn()
        {
            float t = CurrentTime();
            float remain = PauseEnd(t) - t;
            bool moving = !switchDriven || (Time.time < activeUntil && !departPending);
            bool warn = moving && AtEnd(t) && remain < DepartWarn;
            if (warn)
            {
                float k = 1f - Mathf.Clamp01(remain / DepartWarn);
                var c = Color.Lerp(plankColor, Color.white, 0.35f * k);
                c.a = plankColor.a;
                foreach (var plank in planks)
                    if (plank != null) plank.color = c;
                warnTinted = true;
            }
            else if (warnTinted)
            {
                warnTinted = false;
                ApplyPlankColor();
            }
        }

        // 발판 윗면 바로 위에 살아 있는 몸(플레이어·적)이 있는지. 잠든 발판으로 바꾸기 전에 본다.
        private bool HasRider()
        {
            var bounds = solid.bounds;
            var center = new Vector2(bounds.center.x, bounds.max.y + 0.1f);
            var size = new Vector2(bounds.size.x - 0.1f, 0.2f);
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(center, size, 0f, filter, riders);
            for (int i = 0; i < count; i++)
            {
                var rider = riders[i].attachedRigidbody;
                if (rider != null && rider != body && rider.bodyType == RigidbodyType2D.Dynamic) return true;
            }
            return false;
        }

        // 발판 윗면 바로 위에 선 플레이어/적을 같이 움직인다 (몸 콜라이더가 마찰 없는 재질이라 저절로 실려 가지 않는다).
        // 플레이어는 속도로 실어 나른다: 위치를 직접 바꾸면 보간이 끊겨 발판 위에서 캐릭터와 카메라가 떨린다.
        private void CarryRiders(Vector2 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return;

            var bounds = solid.bounds;
            var center = new Vector2(bounds.center.x, bounds.max.y + 0.1f);
            var size = new Vector2(bounds.size.x - 0.1f, 0.2f);
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(center, size, 0f, filter, riders);
            carried.Clear();
            for (int i = 0; i < count; i++)
            {
                var rider = riders[i].attachedRigidbody;
                if (rider == null || rider == body || rider.bodyType != RigidbodyType2D.Dynamic) continue;
                if (rider.linearVelocity.y > 0.5f) continue; // 막 뛰어오르는 중이면 싣지 않는다
                if (!carried.Add(rider)) continue;           // 콜라이더가 여러 개인 몸은 한 번만
                var mover = rider.GetComponent<IBlameYou.Player.PlayerMovement>();
                if (mover != null) mover.SetPlatformVelocity(delta / Time.fixedDeltaTime);
                else rider.position += delta; // 적 등 속도를 받지 않는 몸
            }
        }
    }
}
