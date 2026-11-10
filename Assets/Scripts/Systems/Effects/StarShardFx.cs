using System;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 별똥 조각 연출 (VfxPlayer.StarShard): 처치한 적 자리에서 작은 금별이 위로 톡 튀었다가 플레이어 쪽으로 빨려 들어간다.
    // 닿으면(또는 대상이 사라지거나 MaxLifetime이 지나면) onArrive를 한 번 부르고 작게 반짝이며 사라진다.
    // 그림은 Resources/Effects/Sparkle (유물 상자 반짝임)을 작게 돌려 쓴다.
    public class StarShardFx : MonoBehaviour
    {
        private const float PopTime = 0.18f;      // 위로 튀는 시간
        private const float PopSpeed = 3.5f;      // 튀는 속도 (유닛/초)
        private const float StartSpeed = 4f;      // 날아가기 시작 속도
        private const float Accel = 30f;          // 빨려 들어가는 가속 (유닛/초²)
        private const float ArriveDistance = 0.35f;
        private const float MaxLifetime = 1.5f;   // 무슨 일이 있어도 이 안에 마나는 들어온다
        private const float Scale = 0.45f;

        private Transform target;
        private Vector3 targetOffset;
        private Action onArrive;
        private float age;
        private float speed = StartSpeed;
        private bool arrived;

        // 그림이 없으면 연출 없이 바로 onArrive (마나는 그대로 찬다).
        public static void Launch(Vector3 from, Transform target, Vector3 targetOffset, Sprite[] frames, Action onArrive)
        {
            if (frames == null || frames.Length == 0 || target == null)
            {
                onArrive?.Invoke();
                return;
            }

            var go = new GameObject("StarShard");
            go.transform.position = from;
            go.transform.localScale = new Vector3(Scale, Scale, 1f);
            EffectLibrary.CreateVisual(go.transform, "Visual", frames, 16f, true, Color.clear, 0f, 8);
            var fx = go.AddComponent<StarShardFx>();
            fx.target = target;
            fx.targetOffset = targetOffset;
            fx.onArrive = onArrive;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (target == null || age >= MaxLifetime)
            {
                Arrive();
                return;
            }

            if (age < PopTime)
            {
                transform.position += Vector3.up * (PopSpeed * Time.deltaTime);
                return;
            }

            Vector3 goal = target.position + targetOffset;
            Vector3 toGoal = goal - transform.position;
            speed += Accel * Time.deltaTime;
            float step = speed * Time.deltaTime;
            if (toGoal.magnitude <= Mathf.Max(ArriveDistance, step))
            {
                transform.position = goal;
                Arrive();
                return;
            }
            transform.position += toGoal.normalized * step;
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            var callback = onArrive;
            onArrive = null;
            callback?.Invoke();
            VfxPlayer.AimGlint(transform.position); // 닿은 자리에서 작게 반짝
            Destroy(gameObject);
        }
    }
}
