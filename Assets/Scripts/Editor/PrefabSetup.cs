using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEditor;
using UnityEngine;

namespace IBlameYou.EditorTools
{
    // Tools > I Blame You > Generate Prefabs
    // PlayerSpawner.Build / EnemySpawner.BuildSlime이 조립한 오브젝트를 프리팹으로 굽고,
    // LevelArtConfig에 참조를 채운다. GameAssetSetup.Generate가 컨트롤러를 새로 만들 때마다
    // 프리팹의 애니메이터 참조가 끊어지지 않도록 그 끝에서 자동으로 함께 호출된다.
    public static class PrefabSetup
    {
        private const string ConfigPath = "Assets/Resources/LevelArtConfig.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string SlimePrefabPath = "Assets/Prefabs/Enemies/Slime.prefab";
        private const string FrictionlessPath = "Assets/Art/Materials/Frictionless.physicsMaterial2D";

        [MenuItem("Tools/I Blame You/Generate Prefabs")]
        public static void Generate()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelArtConfig>(ConfigPath);
            if (config == null || config.playerAnimatorController == null || config.slimeAnimatorController == null)
            {
                Debug.LogError("[PrefabSetup] LevelArtConfig가 없거나 비어 있습니다. 먼저 Tools > I Blame You > Generate Sprites And Animations를 실행하세요.");
                return;
            }

            EnsureLayer(CharacterLayers.PlayerLayerName);
            EnsureLayer(CharacterLayers.EnemyLayerName);

            var frictionless = GetOrCreateFrictionless();

            config.playerPrefab = Bake(PlayerSpawner.Build(config), PlayerPrefabPath, frictionless);
            config.slimePrefab = Bake(EnemySpawner.BuildSlime(config), SlimePrefabPath, frictionless);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PrefabSetup] 완료: Player.prefab / Slime.prefab 생성/갱신 + LevelArtConfig 연결됨.");
        }

        // 프리팹 오브젝트에 지정할 레이어가 프로젝트에 없으면 빈 사용자 레이어 슬롯(8~31)에 추가한다.
        private static void EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            int emptySlot = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                string existing = layers.GetArrayElementAtIndex(i).stringValue;
                if (existing == layerName) return;
                if (emptySlot < 0 && string.IsNullOrEmpty(existing)) emptySlot = i;
            }

            if (emptySlot < 0)
            {
                Debug.LogError($"[PrefabSetup] '{layerName}' 레이어를 추가할 빈 슬롯이 없습니다.");
                return;
            }

            layers.GetArrayElementAtIndex(emptySlot).stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // 런타임에 만든 PhysicsMaterial2D는 프리팹에 저장되지 않으므로, 실제 에셋을 만들어 콜라이더에 물린다.
        private static PhysicsMaterial2D GetOrCreateFrictionless()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (material != null) return material;

            material = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, FrictionlessPath);
            return material;
        }

        private static GameObject Bake(GameObject instance, string prefabPath, PhysicsMaterial2D material)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>())
            {
                collider.sharedMaterial = material;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }
    }
}
