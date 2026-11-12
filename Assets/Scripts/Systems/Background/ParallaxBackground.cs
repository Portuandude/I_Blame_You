using UnityEngine;

namespace IBlameYou.Systems
{
    // 카메라 뒤 시차(패럴랙스) 배경 3겹 + 떠다니는 반짝이 (Docs/ArtStyle.md 배경 규칙).
    // 그림은 Resources/Stage/BgSky, BgMid, BgNear, Sparkle (Tools/ArtGen/generate_background.py). 없으면 아무것도 그리지 않는다.
    // 깊이(StageEvents.Depth: 방 격자는 시작 방 거리 / 보스 거리, 외길은 가로 위치)에 따라 하늘 색조가 해질녘 → 밤 → 보스 앞 붉은 빛으로 바뀐다.
    // 방 격자 모드(SetRoom)에서는 깊이를 시작 방에서의 방 거리(StageEvents.Depth)로 쓰고, 방마다 옅은 색을 조금 섞어
    // 옆 방으로 옮겼다는 느낌을 준다 (방 번호로 정해져서 같은 방은 늘 같은 색).
    // CameraFollow가 이번 프레임 위치를 정한 뒤에 따라가도록 늦게 돈다 (먼저 돌면 한 프레임 늦어 떨려 보임).
    [DefaultExecutionOrder(1000)]
    public class ParallaxBackground : MonoBehaviour
    {
        private const int SkyOrder = -40;
        private const int MidOrder = -35;
        private const int NearOrder = -30;
        private const int SparkleOrder = -25;
        private const int SparkleCount = 22;
        private const float Dim = 0.8f; // 플레이 레이어(땅·캐릭터)가 앞에 읽히도록 배경 전체 명도를 낮춘다

        // 가로 시차: 0이면 땅과 같이 움직이고, 1이면 카메라에 붙어 멀리 있는 것처럼 보인다.
        private const float MidFactor = 0.85f;
        private const float NearFactor = 0.6f;
        private const float SparkleFactor = 0.5f;

        private static readonly Color[] TintKeys =
        {
            new Color(1f, 0.9f, 0.92f),   // 0%: 해질녘 (살짝 따뜻하게)
            new Color(0.82f, 0.86f, 1f),  // 50%: 밤
            new Color(0.78f, 0.8f, 1f),   // 80%: 깊은 밤
            new Color(1f, 0.75f, 0.78f),  // 100%: 보스 앞 붉은 빛 (적의 붉은색과 겹치지 않게 약하게)
        };
        private static readonly float[] TintStops = { 0f, 0.5f, 0.8f, 1f };

        // 방마다 섞는 옅은 색 (분홍·민트·라벤더·복숭아·하늘). 깊이 색조 위에 RoomTintMix만큼 섞는다.
        private static readonly Color[] RoomTints =
        {
            new Color(1f, 0.82f, 0.92f),
            new Color(0.8f, 1f, 0.9f),
            new Color(0.88f, 0.82f, 1f),
            new Color(1f, 0.9f, 0.78f),
            new Color(0.8f, 0.92f, 1f),
        };
        private const float RoomTintMix = 0.35f;

        private Camera cam;
        private Rect world;
        private Transform root;
        private SpriteRenderer sky;
        private Layer mid;
        private Layer near;
        private Sprite[] sparkleFrames;
        private Sparkle[] sparkles;
        private Vector3 lastCameraPosition;
        private int room = -1; // 방 격자 모드의 지금 방 (-1: 외길 모드)

        private class Layer
        {
            public SpriteRenderer[] Copies;
            public float Width;
            public float Factor;
            public float BottomFromView; // 화면 아래 끝에서 층 아래 끝까지 (유닛)
        }

        private struct Sparkle
        {
            public SpriteRenderer Renderer;
            public Vector2 Local; // 카메라 기준 위치
            public float Speed;
            public float Phase;
            public float Size;
        }

        public static ParallaxBackground Attach(Camera camera, Rect worldBounds)
        {
            var background = camera.GetComponent<ParallaxBackground>();
            if (background == null) background = camera.gameObject.AddComponent<ParallaxBackground>();
            background.Setup(camera, worldBounds);
            return background;
        }

        private void Setup(Camera camera, Rect worldBounds)
        {
            cam = camera;
            world = worldBounds;
            lastCameraPosition = cam.transform.position; // 다시 시작할 때 반짝이가 튀지 않게
            if (root != null) return; // 이미 만들어 둠 (다시 시작할 때 범위만 바꾼다)

            root = new GameObject("ParallaxBackground").transform;
            root.SetParent(transform, false);

            var skySprite = TrapCommon.Frame("BgSky");
            if (skySprite != null)
            {
                sky = CreateRenderer(root, "Sky", skySprite, SkyOrder);
            }

            mid = CreateLayer(root, "Mid", TrapCommon.Frame("BgMid"), MidOrder, MidFactor, 0.9f);
            near = CreateLayer(root, "Near", TrapCommon.Frame("BgNear"), NearOrder, NearFactor, -0.2f);

            sparkleFrames = TrapCommon.Frames("Sparkle");
            if (sparkleFrames != null)
            {
                sparkles = new Sparkle[SparkleCount];
                var random = new System.Random(5);
                for (int i = 0; i < SparkleCount; i++)
                {
                    var renderer = CreateRenderer(root, $"Sparkle_{i}", sparkleFrames[0], SparkleOrder);
                    renderer.color = new Color(0.5f, 0.97f, 1f); // aqua만 (gold는 보상 신호라 배경에 쓰지 않는다)
                    sparkles[i] = new Sparkle
                    {
                        Renderer = renderer,
                        Local = new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                        Speed = 0.15f + (float)random.NextDouble() * 0.25f,
                        Phase = (float)random.NextDouble() * 10f,
                        Size = 0.35f + (float)random.NextDouble() * 0.4f,
                    };
                }
            }

            lastCameraPosition = cam.transform.position;
        }

        // 방 격자 모드: 방에 들어올 때마다 부른다 (ChunkStreamer.RoomEntered). -1이면 외길 모드처럼 카메라 x로 깊이를 잰다.
        public void SetRoom(int index)
        {
            room = index;
        }

        private static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Layer CreateLayer(Transform parent, string name, Sprite sprite, int order, float factor, float bottomFromView)
        {
            if (sprite == null) return null;
            var layer = new Layer
            {
                Width = sprite.bounds.size.x,
                Factor = factor,
                BottomFromView = bottomFromView,
                Copies = new SpriteRenderer[5], // 카메라 왼쪽 2장 ~ 오른쪽 3장 폭을 덮는다 (아주 넓은 화면 대비)
            };
            for (int i = 0; i < layer.Copies.Length; i++) layer.Copies[i] = CreateRenderer(parent, $"{name}_{i}", sprite, order);
            return layer;
        }

        private void LateUpdate()
        {
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Color tint;
            if (room >= 0)
            {
                tint = Color.Lerp(Tint(StageEvents.Depth), RoomTints[(room * 3) % RoomTints.Length], RoomTintMix) * Dim;
            }
            else
            {
                tint = Tint(world.width > 0f ? Mathf.InverseLerp(world.xMin, world.xMax, camPos.x) : 0f) * Dim;
            }

            tint.a = 1f;

            if (sky != null)
            {
                // 하늘은 카메라에 붙어 화면을 꽉 채운다 (조금 크게 해서 흔들림에도 가장자리가 안 보이게).
                Vector2 size = sky.sprite.bounds.size;
                float scale = Mathf.Max(halfWidth * 2f / size.x, halfHeight * 2f / size.y) * 1.06f;
                sky.transform.position = new Vector3(camPos.x, camPos.y, 0f);
                sky.transform.localScale = new Vector3(scale, scale, 1f);
                sky.color = tint;
            }

            // 위로 올라갈수록 먼 층이 조금 덜 올라가 보이게 (세로 시차 약하게).
            float climb = camPos.y - (world.yMin + halfHeight);
            PlaceLayer(mid, camPos, halfHeight, tint, -climb * 0.08f);
            PlaceLayer(near, camPos, halfHeight, Color.Lerp(tint, Color.white * Dim, 0.4f), -climb * 0.04f);
            UpdateSparkles(camPos, halfWidth, halfHeight);
            lastCameraPosition = camPos;
        }

        private static void PlaceLayer(Layer layer, Vector3 camPos, float halfHeight, Color tint, float dropY)
        {
            if (layer == null) return;

            // 층의 기준점은 카메라 x의 Factor만큼만 따라간다. 화면을 덮도록 그림을 폭 간격으로 늘어놓는다.
            float origin = camPos.x * layer.Factor;
            float first = origin + Mathf.Floor((camPos.x - origin) / layer.Width) * layer.Width - 2f * layer.Width;
            float height = layer.Copies[0].sprite.bounds.size.y;
            float y = camPos.y - halfHeight + layer.BottomFromView + height / 2f + dropY;
            for (int i = 0; i < layer.Copies.Length; i++)
            {
                var copy = layer.Copies[i];
                copy.transform.position = new Vector3(first + layer.Width * (i + 0.5f), y, 0f);
                copy.color = tint;
            }
        }

        private void UpdateSparkles(Vector3 camPos, float halfWidth, float halfHeight)
        {
            if (sparkles == null) return;

            Vector2 cameraMove = camPos - lastCameraPosition;
            float width = halfWidth * 2f;
            float height = halfHeight * 2f;
            for (int i = 0; i < sparkles.Length; i++)
            {
                var s = sparkles[i];
                // 카메라가 움직이면 반대로 조금 밀려서 멀리 떠 있는 것처럼, 그리고 천천히 위로.
                s.Local.x = Mathf.Repeat(s.Local.x - cameraMove.x * (1f - SparkleFactor) / width, 1f);
                s.Local.y = Mathf.Repeat(s.Local.y + (s.Speed * Time.deltaTime - cameraMove.y * (1f - SparkleFactor)) / height, 1f);
                sparkles[i] = s;

                float t = Time.time * 1.6f + s.Phase;
                int frame = Mathf.FloorToInt(t * 2f) % sparkleFrames.Length;
                s.Renderer.sprite = sparkleFrames[frame];
                float x = camPos.x - halfWidth + s.Local.x * width + Mathf.Sin(t * 0.7f) * 0.3f;
                float y = camPos.y - halfHeight + s.Local.y * height;
                s.Renderer.transform.position = new Vector3(x, y, 0f);
                s.Renderer.transform.localScale = Vector3.one * s.Size;
                var color = s.Renderer.color;
                color.a = 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 2.3f));
                s.Renderer.color = color;
            }
        }

        private static Color Tint(float progress)
        {
            progress = Mathf.Clamp01(progress);
            for (int i = 1; i < TintStops.Length; i++)
            {
                if (progress <= TintStops[i])
                    return Color.Lerp(TintKeys[i - 1], TintKeys[i], Mathf.InverseLerp(TintStops[i - 1], TintStops[i], progress));
            }

            return TintKeys[TintKeys.Length - 1];
        }
    }
}
