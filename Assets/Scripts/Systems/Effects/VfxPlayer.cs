using UnityEngine;

namespace IBlameYou.Systems
{
    // 시각 효과 묶음 (Tools/ArtGen/generate_vfx.py → Resources/Effects/{Dust,LevelUp,DeathPop,Sparkle,CheckpointPillar,ClearBurst,ChestOpen,RelicBanner,Shockwave}).
    // 그림이 없으면 아무것도 띄우지 않는다 (원형 대체 그림은 발밑·상자 위에서 어색하다).
    public static class VfxPlayer
    {
        // 그림 안에서 발밑이 중심보다 얼마나 아래인지 (유닛, 100px = 1유닛).
        private const float DustFootBelowCenter = 0.22f;   // 96x56 캔버스, 발밑 = 아래에서 6px
        private const float PillarFootBelowCenter = 0.6f;  // 120x240 캔버스, 발밑 = 위에서 180px
        private const float ShockwaveFloorBelowCenter = 0.16f; // 192x64 캔버스, 바닥 = 아래에서 16px
        private const float ChestBelowCenter = 0.48f;      // ChestOpen 120x240 캔버스, 상자 그림 가운데 = 캔버스 가운데보다 48px 아래

        // 착지 먼지. strength 0~1 (떨어진 속도)로 크기를 정한다.
        public static void Dust(Vector3 feet, float strength)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Dust");
            if (frames == null) return;
            float scale = Mathf.Lerp(0.7f, 1.2f, Mathf.Clamp01(strength));
            EffectLibrary.PlayOneShot(feet + Vector3.up * (DustFootBelowCenter * scale), frames, 18f, Color.clear, 0f,
                false, scale, sortingOrder: -1); // 땅 그림(-3 이하) 위, 발판·캐릭터(0 이상) 뒤
        }

        // 레벨업 빛기둥. 플레이어를 따라다니도록 자식으로 붙인다 (그림이 좌우 대칭이라 방향 뒤집힘은 상관없다).
        public static void LevelUp(Transform follow, float feetLocalY)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/LevelUp");
            if (frames == null || follow == null) return;
            var visual = EffectLibrary.CreateVisual(follow, "LevelUpPillar", frames, 14f, false, Color.clear, 0f, 2,
                destroyAtEnd: true);
            visual.transform.localPosition = new Vector3(0f, feetLocalY + PillarFootBelowCenter, 0f);
        }

        // 적이 쓰러질 때 별·하트가 튀는 펑.
        public static void DeathPop(Vector3 position, float scale)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/DeathPop");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(position, frames, 20f, Color.clear, 0f, false, Mathf.Clamp(scale, 0.6f, 2f));
        }

        // 유물 상자 위에서 계속 반짝이는 별. 상자가 사라지면 함께 사라진다.
        public static void AttachSparkle(Transform parent, float height)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Sparkle");
            if (frames == null || parent == null) return;
            var visual = EffectLibrary.CreateVisual(parent, "Sparkle", frames, 10f, true, Color.clear, 0f, 4);
            visual.transform.localPosition = new Vector3(0f, height, 0f);
        }

        // (체크포인트 제거 2026-11-10 뒤로 쓰는 곳 없음 — 샘·구슬 연출에 다시 쓸 수 있게 남김) 수정 발밑에서 aqua 빛기둥이 솟고 큰 반짝임 3개가 떠오른다 (그림은 레벨업 기둥과 같은 틀).
        public static void CheckpointPillar(Vector3 feet)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/CheckpointPillar");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(feet + Vector3.up * (PillarFootBelowCenter * 1.3f), frames, 12f, Color.clear, 0f,
                false, 1.3f, sortingOrder: 2); // 수정(1) 앞, 기둥이 수정보다 조금 크게
        }

        // 전투 구역을 깼을 때 구역 가운데에서 별·하트가 크게 터진다.
        public static void ClearBurst(Vector3 center)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/ClearBurst");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(center, frames, 14f, Color.clear, 0f, false, 1.6f, sortingOrder: 8);
        }

        // 유물 상자를 열 때: 뚜껑이 튀어 올라 별로 터지고 gold 빛기둥 + 별·하트. chestCenter는 상자 그림의 가운데, scale은 상자 그림 크기.
        // 그림이 없으면 false (부른 쪽이 예전 터짐으로 대신한다).
        public static bool ChestOpen(Vector3 chestCenter, float scale)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/ChestOpen");
            if (frames == null) return false;
            EffectLibrary.PlayOneShot(chestCenter + Vector3.up * (ChestBelowCenter * scale), frames, 14f, Color.clear, 0f,
                false, scale, sortingOrder: 3); // 상자(3)가 사라진 자리에 같은 순서로
            return true;
        }

        // 유물 이름 띠: 상자 위에 띠가 톡 튀어나와 잠깐 떠 있다가 사라진다. 띠 그림이 없으면 글자만.
        public static void RelicBanner(Vector3 position, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/RelicBanner");
            RelicBannerView.Create(position, frames != null ? frames[0] : null, text);
        }

        // 무거운 착지 충격파 (중간보스 등장). feet = 바닥 위 발밑.
        public static void Shockwave(Vector3 feet, float scale)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Shockwave");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(feet + Vector3.up * (ShockwaveFloorBelowCenter * scale), frames, 18f, Color.clear, 0f,
                false, scale, sortingOrder: 2);
        }

        // 투구 꼭대기에서 크게 한 번 반짝 (반짝임 그림을 크게, 빠르게).
        public static void HelmetGlint(Vector3 position)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Sparkle");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(position, frames, 14f, Color.clear, 0f, false, 0.9f, sortingOrder: 7);
        }

        // 적 원거리 공격 예고: 화살 끝에서 작게 한 번 반짝인다 (유물 상자 반짝임 그림을 작게, 빠르게).
        public static void AimGlint(Vector3 position)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Sparkle");
            if (frames == null) return;
            EffectLibrary.PlayOneShot(position, frames, 16f, Color.clear, 0f, false, 0.45f, sortingOrder: 7);
        }
    }
}
