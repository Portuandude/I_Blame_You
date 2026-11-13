using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹이 소환한 무리(SlimeBrood)의 합체 연출 (그림·소리만, 제한 시간·회복량은 SlimeBrood 그대로, 2026-11-13).
    // - Warn: 합체 5초 전부터 1초마다 살아남은 슬라임마다 초록 고리 + "똑" (남은 초가 적을수록 음이 높아진다)
    // - UpdateLines: 합체 3초 전부터 슬라임마다 킹 쪽으로 초록 젤리 끈이 이어지며 점점 굵고 진해진다 (어디로 갈지 예고)
    // - MergeStart / Trail: 합체가 시작되면 "슈우욱" + 날아가는 슬라임 뒤로 초록 젤리 방울이 떨어진다
    // - Gulp: 킹이 삼킬 때 "꿀꺽" + 킹 둘레로 큰 초록 고리 + 위로 튀는 방울 (SlimeKingController.AbsorbBrood)
    // - Cleared: 시간 안에 모두 처치하면 마지막 자리에서 금빛 고리 + 밝은 세 음 + 금빛 방울
    // 끈은 무리 오브젝트의 자식이 아니다 (SlimeBrood.Finish가 자식을 모두 부모로 옮기므로). 이 컴포넌트가 지워질 때 함께 지운다.
    public class SlimeBroodFx : MonoBehaviour
    {
        public static readonly Color MergeColor = new Color(0.45f, 1f, 0.5f, 0.9f);
        public static readonly Color ClearColor = new Color(1f, 0.85f, 0.3f, 0.95f);

        public const float WarnTime = 5f;
        public const float LineTime = 3f;
        private const float TrailInterval = 0.05f;

        private static Material lineMaterial;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private float nextTrail;

        // 남은 초(1~5)가 바뀔 때 한 번 부른다.
        public static void Warn(List<HealthSystem> members, int secondsLeft)
        {
            bool anyOnScreen = false;
            foreach (var member in members)
            {
                if (member == null) continue;
                VfxPlayer.ManaRing(member.transform, Vector3.up * 0.4f, MergeColor, 1.1f);
                if (SlimeSplitFx.OnScreen(member.transform.position)) anyOnScreen = true;
            }

            if (anyOnScreen)
            {
                float pitch = 1f + (WarnTime - Mathf.Clamp(secondsLeft, 1, (int)WarnTime)) * 0.08f;
                GameAudio.PlaySfxPitched("BroodTick", pitch, 0.8f);
            }
        }

        // urgency 0→1: 끈이 가늘고 옅은 데서 굵고 진하게.
        public void UpdateLines(List<HealthSystem> members, Transform king, float urgency)
        {
            if (king == null)
            {
                ClearLines();
                return;
            }

            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            urgency = Mathf.Clamp01(urgency);
            float width = Mathf.Lerp(0.04f, 0.14f, urgency);
            // 끝이 다가올수록 살짝 두근거리듯 굵기가 흔들린다
            width *= 1f + 0.25f * urgency * Mathf.Sin(Time.time * Mathf.Lerp(6f, 18f, urgency));
            var color = MergeColor;
            color.a = Mathf.Lerp(0.2f, 0.75f, urgency);
            Vector3 target = king.position + Vector3.up * 1f;

            int used = 0;
            foreach (var member in members)
            {
                if (member == null) continue;
                if (used == lines.Count) lines.Add(NewLine());
                var line = lines[used++];
                line.enabled = true;
                line.SetPosition(0, member.transform.position + Vector3.up * 0.3f);
                line.SetPosition(1, target);
                line.startColor = color;
                var end = color;
                end.a *= 0.4f;
                line.endColor = end; // 킹 쪽은 옅게
                line.startWidth = width;
                line.endWidth = width * 0.5f;
            }

            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }

        public void ClearLines()
        {
            foreach (var line in lines) if (line != null) Destroy(line.gameObject);
            lines.Clear();
        }

        public static void MergeStart(List<HealthSystem> members)
        {
            foreach (var member in members)
            {
                if (member != null && SlimeSplitFx.OnScreen(member.transform.position))
                {
                    GameAudio.PlaySfx("BroodMerge", 0.8f);
                    return;
                }
            }
        }

        // 합체 비행 중 매 프레임: TrailInterval마다 슬라임 자리에서 젤리 방울을 하나씩 떨어뜨린다.
        public void Trail(List<HealthSystem> members)
        {
            if (Time.time < nextTrail) return;
            nextTrail = Time.time + TrailInterval;
            foreach (var member in members)
            {
                if (member == null) continue;
                var v = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1.5f), 0f);
                SlimeSplitFx.Drop(member.transform.position, v, MergeColor, Random.Range(0.12f, 0.2f) * member.transform.localScale.x + 0.05f);
            }
        }

        public static void Gulp(Transform king, Vector3 center, float bodyHeight)
        {
            if (king == null) return;
            if (SlimeSplitFx.OnScreen(center)) GameAudio.PlaySfx("BroodGulp", 0.9f);
            VfxPlayer.ManaRing(king, king.InverseTransformPoint(center), MergeColor, Mathf.Max(2f, bodyHeight * 1.3f));
            Vector3 top = center + Vector3.up * bodyHeight * 0.5f;
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(40f, 140f, i / 4f) * Mathf.Deg2Rad;
                SlimeSplitFx.Drop(top, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(4f, 6f), MergeColor, Random.Range(0.18f, 0.28f));
            }
        }

        public static void Cleared(Vector3 position)
        {
            // 고리는 따라갈 대상이 필요하다: 잠깐 남는 빈 오브젝트에 붙인다
            var anchor = new GameObject("BroodClearFx");
            anchor.transform.position = position;
            VfxPlayer.ManaRing(anchor.transform, Vector3.zero, ClearColor, 2.5f);
            Destroy(anchor, 0.6f);
            if (SlimeSplitFx.OnScreen(position)) GameAudio.PlaySfx("BroodClear", 0.85f);
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Lerp(30f, 150f, i / 5f) * Mathf.Deg2Rad;
                SlimeSplitFx.Drop(position, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(4f, 6.5f), ClearColor, Random.Range(0.14f, 0.22f));
            }
        }

        private LineRenderer NewLine()
        {
            var go = new GameObject("BroodPullLine");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.material = lineMaterial;
            line.numCapVertices = 4;
            line.sortingOrder = 0; // 몸(1) 뒤
            return line;
        }

        private void OnDestroy() => ClearLines();
    }
}
