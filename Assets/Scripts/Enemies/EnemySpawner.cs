using System;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 모든 적이 공유하는 스폰/조립 헬퍼. 적별 구성(콜라이더, 수치, 컨트롤러)은 각 적의 Spawner에 둔다.
    // (예: Common/Slime/SlimeSpawner) — 새 적은 그 폴더 구조를 따라 Spawner + Controller를 추가하면 된다.
    public static class EnemySpawner
    {
        // 프리팹이 있고 필요한 컨트롤러 T가 붙어 있으면 인스턴스화하고, 아니면 build로 즉석 조립한다.
        // build는 에디터 툴(PrefabSetup)이 프리팹을 구울 때도 쓰는 단일 출처여야 한다.
        public static T Spawn<T>(GameObject prefab, Func<GameObject> build, Vector3 position, string name)
            where T : EnemyController
        {
            CharacterLayers.EnsureCollisionRules();

            GameObject go;
            if (prefab != null && prefab.GetComponent<T>() != null)
            {
                go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            }
            else
            {
                go = build();
                go.transform.position = position;
            }

            go.name = name;
            return go.GetComponent<T>();
        }

        public static GameObject CreateBody(string name, float gravityScale = 3f)
        {
            var go = new GameObject(name);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            rb.freezeRotation = true;

            return go;
        }

        // 스프라이트가 있으면 그걸, 없으면 단색 사각형을 fallbackColor로 그린다. 컨트롤러가 있으면 Animator도 붙인다.
        public static GameObject AddVisual(GameObject go, Sprite sprite, RuntimeAnimatorController controller,
            float scale, Vector3 localPosition, Color fallbackColor)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            visual.transform.localPosition = localPosition;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = fallbackColor;
            }

            if (controller != null)
            {
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
            }

            return visual;
        }

        // 컨트롤러(EnemyController)보다 먼저 붙여야 한다.
        public static HealthSystem AddHealthAndStun(GameObject go, float maxHealth, float stunDuration)
        {
            var health = go.AddComponent<HealthSystem>();
            health.ConfigureMaxHealth(maxHealth);
            go.AddComponent<HitStun>().Configure(stunDuration, false);
            return health;
        }

        public static LayerMask GroundMask()
        {
            int layer = LayerMask.NameToLayer("Ground");
            return layer >= 0 ? (LayerMask)(1 << layer) : (LayerMask)1;
        }

        public static void AssignEnemyLayer(GameObject go)
        {
            CharacterLayers.Assign(go, CharacterLayers.EnemyLayerName);
        }
    }
}
