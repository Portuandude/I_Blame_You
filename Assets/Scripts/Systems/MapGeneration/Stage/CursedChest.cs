using System;
using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 저주받은 상자 (Chunks/CursedChestRule): 보라색으로 물든 유물 상자. 보라 연기가 피어오르고, 플레이어가 닿으면 덜컹거리며
    // 정예 경고(소리·보라 가장자리·알림)와 함께 onAwaken을 부른다 (정예를 부르는 건 ChunkStreamer).
    // 정예를 쓰러뜨리면 ChunkStreamer가 Break를 불러 이 상자를 치우고 그 자리에 유물 상자(2개 중 고르기)를 놓는다.
    public class CursedChest : MonoBehaviour
    {
        private const float VisualHeight = 0.55f;
        private const float VisualScale = 1.3f;
        private const float WispInterval = 0.5f;
        private const float ShakeTime = 0.6f;
        private static readonly Color CurseColor = new Color(0.66f, 0.4f, 0.98f);

        private Transform visual;
        private Action onAwaken;
        private bool awake;
        private float wispTimer;
        private float shakeLeft;

        public static CursedChest Create(Transform parent, Vector2 position, Action onAwaken)
        {
            var go = new GameObject("CursedChest");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 1.6f);
            trigger.offset = new Vector2(0f, 0.8f);

            var chest = go.AddComponent<CursedChest>();
            chest.onAwaken = onAwaken;
            var v = EffectLibrary.CreateVisual(go.transform, "Chest", TrapCommon.Frames("RelicChest"), 8f, true, CurseColor, 0.8f, 3);
            v.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
            v.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            var renderer = v.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null) renderer.color = CurseColor; // 그림이 있으면 보라로 물들인다
            chest.visual = v.transform;
            return chest;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (awake || TrapCommon.PlayerFrom(other) == null) return;
            awake = true;
            shakeLeft = ShakeTime;
            GameAudio.PlaySfx("EliteWarn", 0.85f, 0f);
            ScreenFlash.EdgeWarn(CurseColor, 0.6f);
            StageEvents.PostNotice("저주받은 상자! 정예를 쓰러뜨려라!");
            EffectLibrary.PlayOneShot(transform.position + Vector3.up * VisualHeight, EffectLibrary.TeleportFrames, 20f, new Color(CurseColor.r, CurseColor.g, CurseColor.b, 0.8f), 1.4f);
            onAwaken?.Invoke();
        }

        private void Update()
        {
            if (visual == null) return;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                float k = Mathf.Max(0f, shakeLeft / ShakeTime);
                visual.localPosition = new Vector3(Mathf.Sin(Time.time * 60f) * 0.08f * k, VisualHeight, 0f);
            }

            wispTimer -= Time.deltaTime;
            if (wispTimer > 0f) return;
            wispTimer = awake ? WispInterval * 0.5f : WispInterval; // 깨어난 뒤에는 연기가 더 자주
            var color = new Color(CurseColor.r, CurseColor.g, CurseColor.b, 0.55f);
            EffectLibrary.PlayOneShot(transform.position + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), VisualHeight + 0.6f, 0f),
                EffectLibrary.TeleportFrames, 14f, color, 0.4f, false, 0.45f);
        }

        // 정예를 쓰러뜨림: 저주가 풀리며 상자가 터진다 (그 자리에 유물 상자는 ChunkStreamer가 놓는다).
        public void Break()
        {
            var center = transform.position + Vector3.up * VisualHeight;
            if (!VfxPlayer.ChestOpen(center, VisualScale)) EffectLibrary.PlayHit(Element.Neutral, center, 1.4f);
            GameAudio.PlaySfx("GateOpen", 0.6f);
            StageEvents.PostNotice("저주가 풀렸다! 유물 2개 중 하나를 고르자");
            Destroy(gameObject);
        }
    }
}
