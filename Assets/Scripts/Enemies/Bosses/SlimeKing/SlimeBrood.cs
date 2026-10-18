using System.Collections;
using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 패턴 1로 소환된 어미 슬라임 무리. 어미가 분열해 나온 슬라임도 이 무리에 들어온다 (SplitOnDeath → IEnemyGroup).
    // 제한 시간 안에 모두 처치하지 못하면 살아남은 슬라임이 킹에게 돌아가 합체하고, 킹은 그만큼 체력을 회복한다.
    // 이 무리는 전투 구역(ArenaZone)의 클리어 조건에 들어가지 않는다. 킹이 모두 쓰러지면 남은 슬라임은 함께 사라진다.
    public class SlimeBrood : MonoBehaviour, IEnemyGroup
    {
        private const float MergeTime = 0.9f;
        private const float MotherHealRatio = 0.08f; // 살아남은 어미 하나당 킹 최대 체력의 8%
        private const float SlimeHealRatio = 0.03f;  // 분열해 나온 작은 슬라임 하나당 3%

        private readonly List<HealthSystem> members = new List<HealthSystem>();
        private float endTime;
        private bool merging;

        public static SlimeBrood Active { get; private set; }

        public static SlimeBrood Create(Transform parent, float duration)
        {
            var go = new GameObject("SlimeBrood");
            go.transform.SetParent(parent, false);
            go.transform.position = parent != null ? parent.position : Vector3.zero;
            var brood = go.AddComponent<SlimeBrood>();
            brood.endTime = Time.time + duration;
            Active = brood;
            return brood;
        }

        public void RegisterEnemy(HealthSystem enemy)
        {
            if (enemy == null || enemy.IsDead || members.Contains(enemy)) return;
            members.Add(enemy);
            enemy.transform.SetParent(transform, true);
        }

        private void Update()
        {
            if (merging) return;

            members.RemoveAll(m => m == null || m.IsDead);
            if (members.Count == 0)
            {
                StageEvents.PostNotice("슬라임을 모두 처치했다!");
                Finish();
                return;
            }

            float left = endTime - Time.time;
            BossEvents.Notice = $"슬라임 {members.Count}마리 · 합체까지 {Mathf.CeilToInt(Mathf.Max(left, 0f))}초";
            if (left <= 0f) StartCoroutine(Merge());
        }

        // 제한 시간이 지났다: 살아남은 슬라임이 가장 가까운 킹에게 빨려 들어가 체력을 채워 준다.
        private IEnumerator Merge()
        {
            merging = true;
            var king = SlimeKingController.Nearest(transform.position);
            if (king == null)
            {
                Dismiss();
                yield break;
            }

            StageEvents.PostNotice("슬라임들이 슬라임 킹과 합체했다!");
            BossEvents.Notice = "합체 중...";

            var starts = new List<Vector3>();
            float heal = 0f;
            foreach (var member in members)
            {
                starts.Add(member.transform.position);
                Freeze(member);
                heal += member.GetComponent<SplitOnDeath>() != null ? MotherHealRatio : SlimeHealRatio;
            }

            for (float t = 0f; t < MergeTime; t += Time.deltaTime)
            {
                if (king == null) break;
                Vector3 target = king.transform.position + Vector3.up * 1f;
                float k = t / MergeTime;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] == null) continue;
                    var position = Vector3.Lerp(starts[i], target, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f;
                    members[i].transform.position = position;
                    members[i].transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
                }

                yield return null;
            }

            foreach (var member in members) if (member != null) Destroy(member.gameObject);
            members.Clear();

            if (king != null) king.AbsorbBrood(heal);
            Finish();
        }

        // 킹이 모두 쓰러졌을 때: 남은 슬라임은 경험치 없이 녹아 사라진다.
        public void Dismiss()
        {
            foreach (var member in members)
            {
                if (member == null) continue;
                EffectLibrary.PlayHit(Element.Earth, member.transform.position + Vector3.up * 0.4f, 0.8f);
                Destroy(member.gameObject);
            }

            members.Clear();
            Finish();
        }

        private static void Freeze(HealthSystem member)
        {
            var controller = member.GetComponent<EnemyController>();
            if (controller != null) controller.enabled = false;
            var body = member.GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
            member.Invulnerable = true;
        }

        private void Finish()
        {
            // 쓰러지는 중인 슬라임은 무리와 함께 지우지 않고 사망 연출을 마저 하게 둔다.
            for (int i = transform.childCount - 1; i >= 0; i--) transform.GetChild(i).SetParent(transform.parent, true);
            BossEvents.Notice = null;
            if (Active == this) Active = null;
            Destroy(gameObject, 0.1f);
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
                BossEvents.Notice = null;
            }
        }
    }
}
