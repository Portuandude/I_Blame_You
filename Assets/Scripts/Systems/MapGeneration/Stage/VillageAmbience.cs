using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마을 방 분위기 입자 (콜라이더 없음): 식당 간판 그릇에서 김이 천천히 올라가고, 가로등 둘레에 반딧불이 맴돈다.
    // VillageDecor.Build가 마을 방 청크 루트 아래에 하나 붙인다 (방이 내려가면 같이 사라진다).
    // 카메라에서 멀면(다른 방) 움직이지 않는다. 입자는 미리 만든 몇 개를 돌려 쓴다.
    public class VillageAmbience : MonoBehaviour
    {
        private const int SteamCount = 5;
        private const float SteamLife = 2.4f;        // 김 한 덩이가 올라가 사라지는 시간
        private const float SteamRise = 1.6f;
        private const float SteamSize = 0.32f;
        private const int FirefliesPerLamp = 3;
        private const float FireflyRadius = 0.55f;
        private const float LampGlowHeight = 2.15f;  // 가로등 불빛 높이 (Lamp.png 40px 중 위쪽 등, 16px = 1유닛)
        private const float ActiveRange = 26f;       // 카메라가 이 거리 안일 때만 움직인다
        private static readonly Color SteamColor = new Color(1f, 0.96f, 0.84f, 0.55f); // cream
        private static readonly Color FireflyColor = new Color(1f, 0.85f, 0.3f, 1f);   // gold

        private Vector3 steamOrigin;                 // 로컬 (방 바닥 왼쪽 기준)
        private SpriteRenderer[] steam;
        private readonly List<SpriteRenderer> fireflies = new List<SpriteRenderer>();
        private readonly List<Vector3> fireflyCenters = new List<Vector3>();
        private float time;

        // parent: VillageDecor (방 왼쪽 바닥이 원점), steamAt: 김이 나오는 로컬 위치, lampsX: 가로등 로컬 x들
        public static VillageAmbience Create(Transform parent, Vector3 steamAt, IList<float> lampsX)
        {
            var go = new GameObject("VillageAmbience");
            go.transform.SetParent(parent, false);
            var ambience = go.AddComponent<VillageAmbience>();
            ambience.steamOrigin = steamAt;
            ambience.time = Random.value * 10f;
            var circle = SolidSpriteFactory.CreateCircle();

            ambience.steam = new SpriteRenderer[SteamCount];
            for (int i = 0; i < SteamCount; i++) ambience.steam[i] = ambience.Dot(circle, SteamColor, -3); // 건물(-3)과 같은 층, 나중에 만들어 앞에

            foreach (float x in lampsX)
                for (int k = 0; k < FirefliesPerLamp; k++)
                {
                    ambience.fireflies.Add(ambience.Dot(circle, FireflyColor, -1));
                    ambience.fireflyCenters.Add(new Vector3(x, LampGlowHeight, 0f));
                }
            return ambience;
        }

        private SpriteRenderer Dot(Sprite sprite, Color color, int order)
        {
            var go = new GameObject("Dot");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            go.transform.localScale = Vector3.zero;
            return renderer;
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null && Mathf.Abs(cam.transform.position.x - (transform.position.x + steamOrigin.x)) > ActiveRange) return;
            time += Time.deltaTime;

            // 김: 덩이마다 같은 간격으로 늦게 시작해 위로 흔들리며 커지고 옅어진다
            for (int i = 0; i < steam.Length; i++)
            {
                float t = Mathf.Repeat(time / SteamLife + i / (float)steam.Length, 1f);
                float sway = Mathf.Sin((time + i * 1.7f) * 1.6f) * 0.18f * t;
                steam[i].transform.localPosition = steamOrigin + new Vector3(sway, t * SteamRise, 0f);
                float size = SteamSize * (0.5f + t);
                steam[i].transform.localScale = new Vector3(size, size, 1f);
                var c = SteamColor;
                c.a *= Mathf.Sin(t * Mathf.PI); // 나타났다가 사라진다
                steam[i].color = c;
            }

            // 반딧불: 가로등 불빛 둘레를 서로 다른 속도로 8자처럼 맴돌며 깜빡인다
            for (int i = 0; i < fireflies.Count; i++)
            {
                float phase = time * (0.7f + (i % 3) * 0.23f) + i * 2.1f;
                var offset = new Vector3(Mathf.Sin(phase) * FireflyRadius, Mathf.Sin(phase * 2f) * FireflyRadius * 0.5f, 0f);
                fireflies[i].transform.localPosition = fireflyCenters[i] + offset;
                float blink = 0.5f + 0.5f * Mathf.Sin(time * 3.1f + i * 1.3f);
                float size = 0.07f + 0.05f * blink;
                fireflies[i].transform.localScale = new Vector3(size, size, 1f);
                var c = FireflyColor;
                c.a = 0.4f + 0.6f * blink;
                fireflies[i].color = c;
            }
        }
    }
}
