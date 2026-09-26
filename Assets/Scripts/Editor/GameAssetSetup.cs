using System.Collections.Generic;
using System.IO;
using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IBlameYou.EditorTools
{
    // Tools > I Blame You > Generate Sprites And Animations 메뉴 한 번으로:
    // - 2D Platformer Tileset의 Player Sword 프레임들로 Idle/Run/Rise/Fall/Dash/Attack 클립 + 애니메이터를 만들고
    // - Slime 프레임들로 Idle/Run/Die 클립 + 애니메이터를 만들고
    // - 바닥 타일 스프라이트를 골라
    // Assets/Resources/LevelArtConfig.asset에 전부 담아둔다. 런타임 코드는 이 설정 하나만 Resources.Load로 읽는다.
    // 몇 번을 다시 실행해도 기존 결과물을 덮어쓰도록 만들어져 있다(idempotent).
    public static class GameAssetSetup
    {
        private const string CharacterRoot = "Assets/2D Platformer Tileset/Sprites/Main_Character/Player Sword";
        private const string SlimeSpriteRoot = "Assets/Art/Sprites/Enemies/Slime"; // 프로젝트 전용 슬라임 아트(공격 시트에서 만든 Attack/Idle/Run/Die)
        private const string WizardSpriteRoot = "Assets/Art/Sprites/Characters/Wizard"; // 플레이어 마법사 아트 (동작별로 하나씩 교체 중)
        private const string TilesetPath = "Assets/2D Platformer Tileset/Sprites/Tileset/tileset_1.png";
        private const string GroundTileName = "tileset_1_39";

        private const string PlayerOutputFolder = "Assets/Art/Animations/Player";
        private const string EnemyOutputFolder = "Assets/Art/Animations/Enemies/Slime";
        private const string ConfigPath = "Assets/Resources/LevelArtConfig.asset";

        [MenuItem("Tools/I Blame You/Generate Sprites And Animations")]
        public static void Generate()
        {
            EnsureFolder(PlayerOutputFolder);
            EnsureFolder(EnemyOutputFolder);
            EnsureFolder("Assets/Resources");

            var (playerController, playerDefaultSprite) = GeneratePlayerAnimations();
            var (slimeController, slimeDefaultSprite) = GenerateSlimeAnimations();

            if (playerController == null || slimeController == null)
            {
                Debug.LogError("[GameAssetSetup] 애니메이션 생성에 실패해 LevelArtConfig 갱신을 건너뜁니다. 위 경고 로그를 확인하세요.");
                return;
            }

            EnsureFullRectMesh(TilesetPath);
            var groundTile = FindSprite(TilesetPath, GroundTileName);
            if (groundTile == null)
            {
                Debug.LogWarning($"[GameAssetSetup] 바닥 타일 스프라이트 '{GroundTileName}'을(를) 찾지 못했습니다. groundTile은 비워둡니다.");
            }

            var config = AssetDatabase.LoadAssetAtPath<LevelArtConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LevelArtConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.groundTile = groundTile;
            config.playerAnimatorController = playerController;
            config.playerDefaultSprite = playerDefaultSprite;
            config.slimeAnimatorController = slimeController;
            config.slimeDefaultSprite = slimeDefaultSprite;
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 컨트롤러를 지웠다 다시 만들기 때문에 GUID가 바뀌므로, 프리팹의 애니메이터 참조도 같이 갱신한다.
            PrefabSetup.Generate();

            Debug.Log("[GameAssetSetup] 완료: Player/Slime 애니메이션 + LevelArtConfig + 프리팹 생성/갱신됨. Play를 눌러 확인하세요.");
        }

        private static (AnimatorController controller, Sprite defaultSprite) GeneratePlayerAnimations()
        {
            // Idle만 마법사 아트로 교체 (나머지 동작은 Player Sword 그대로, 순서대로 교체 예정).
            // 프레임이 빛나는 스태프의 밝기 변화라서, 끝에서 처음으로 튀지 않게 왕복(핑퐁) 재생한다.
            var idleFrames = PingPong(LoadNamedFrames($"{WizardSpriteRoot}/Idle", "wizard_idle_", 0, 7, padWidth: 2, prepare: EnsureWizardSpriteImport));
            var runFrames = LoadNamedFrames($"{CharacterRoot}/run", "player_sword_run_", 0, 9, padWidth: 2);
            var riseSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CharacterRoot}/jump/player_sword_rise.png");
            var fallSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CharacterRoot}/jump/player_sword_fall.png");
            var dashFrames = LoadNamedFrames($"{CharacterRoot}/roll", "player_sword_roll_", 0, 10, padWidth: 2);
            var attackFrames = LoadNamedFrames($"{CharacterRoot}/attack_1", "player_sword_attack1_", 0, 7);

            if (idleFrames.Length == 0 || runFrames.Length == 0 || riseSprite == null || fallSprite == null
                || dashFrames.Length == 0 || attackFrames.Length == 0)
            {
                Debug.LogError("[GameAssetSetup] 플레이어(Player Sword) 프레임을 찾지 못했습니다. 임포트 경로를 확인하세요.");
                return (null, null);
            }

            var idleClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Idle", idleFrames, 8f, true);
            var runClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Run", runFrames, 14f, true);
            var riseClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Rise", new[] { riseSprite }, 1f, true);
            var fallClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Fall", new[] { fallSprite }, 1f, true);
            var dashClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Dash", dashFrames, 18f, false);
            var attackClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Attack", attackFrames, 14f, false);

            var controller = BuildPlayerAnimatorController(idleClip, runClip, riseClip, fallClip, dashClip, attackClip);
            return (controller, idleFrames[0]);
        }

        // 0,1,...,n-1,n-2,...,1 순서로 (첫/끝 프레임은 한 번씩만) — 루프할 때 이음매가 없다.
        private static Sprite[] PingPong(Sprite[] frames)
        {
            if (frames.Length < 3) return frames;

            var result = new List<Sprite>(frames);
            for (int i = frames.Length - 2; i >= 1; i--) result.Add(frames[i]);
            return result.ToArray();
        }

        private static AnimatorController BuildPlayerAnimatorController(AnimationClip idle, AnimationClip run,
            AnimationClip rise, AnimationClip fall, AnimationClip dash, AnimationClip attack)
        {
            string controllerPath = $"{PlayerOutputFolder}/PlayerAnimator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;

            var idleState = stateMachine.AddState("Idle"); idleState.motion = idle;
            var runState = stateMachine.AddState("Run"); runState.motion = run;
            var riseState = stateMachine.AddState("Rise"); riseState.motion = rise;
            var fallState = stateMachine.AddState("Fall"); fallState.motion = fall;
            var dashState = stateMachine.AddState("Dash"); dashState.motion = dash;
            var attackState = stateMachine.AddState("Attack"); attackState.motion = attack;

            stateMachine.defaultState = idleState;

            // Any State는 canTransitionToSelf=false로 "이미 그 상태면 스스로에게는 전이하지 않는다"는
            // 뜻일 뿐, 검사를 멈추지는 않는다. 그래서 Dash/Attack에 머물러 있는 동안에도 매 프레임
            // 아래 4개 조건이 계속 같이 만족되면 그쪽으로 전이했다가, 다음 프레임에 IsDashing/IsAttacking이
            // 여전히 true라서 다시 Dash/Attack으로 돌아오는 게 반복돼 매 프레임 상태가 뒤바뀌며
            // 버벅이는 것처럼 보였다. Rise/Fall/Run/Idle 쪽에 "대쉬/공격 중이 아닐 때만"이라는
            // 조건을 추가해서 막는다.
            AddAnyStateTransition(stateMachine, dashState, ("IsDashing", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(stateMachine, attackState, ("IsAttacking", AnimatorConditionMode.If, 0f));

            AddAnyStateTransition(stateMachine, riseState,
                ("IsDashing", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("IsGrounded", AnimatorConditionMode.IfNot, 0f),
                ("VerticalVelocity", AnimatorConditionMode.Greater, 0.05f));

            AddAnyStateTransition(stateMachine, fallState,
                ("IsDashing", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("IsGrounded", AnimatorConditionMode.IfNot, 0f),
                ("VerticalVelocity", AnimatorConditionMode.Less, 0.05f));

            AddAnyStateTransition(stateMachine, runState,
                ("IsDashing", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("IsGrounded", AnimatorConditionMode.If, 0f),
                ("Speed", AnimatorConditionMode.Greater, 0.05f));

            AddAnyStateTransition(stateMachine, idleState,
                ("IsDashing", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("IsGrounded", AnimatorConditionMode.If, 0f),
                ("Speed", AnimatorConditionMode.Less, 0.05f));

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static (AnimatorController controller, Sprite defaultSprite) GenerateSlimeAnimations()
        {
            // 슬라임은 직접 만든 아트(공격 시트에서 분리)로 Attack/Idle/Run/Die 전부 통일. 임포트 설정은 아래에서 자동으로 맞춘다.
            var idleFrames = LoadNamedFrames($"{SlimeSpriteRoot}/Idle", "slime_idle_", 0, 7, padWidth: 2, prepare: EnsureSlimeSpriteImport);
            var runFrames = LoadNamedFrames($"{SlimeSpriteRoot}/Run", "slime_run_", 0, 7, padWidth: 2, prepare: EnsureSlimeSpriteImport);
            var dieFrames = LoadNamedFrames($"{SlimeSpriteRoot}/Die", "slime_die_", 0, 7, padWidth: 2, prepare: EnsureSlimeSpriteImport);
            var attackFrames = LoadNamedFrames($"{SlimeSpriteRoot}/Attack", "slime_attack_", 0, 9, padWidth: 2, prepare: EnsureSlimeSpriteImport);

            if (idleFrames.Length == 0 || runFrames.Length == 0 || dieFrames.Length == 0 || attackFrames.Length == 0)
            {
                Debug.LogError("[GameAssetSetup] 슬라임 프레임을 찾지 못했습니다. 임포트 경로를 확인하세요.");
                return (null, null);
            }

            var idleClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Idle", idleFrames, 8f, true);
            var runClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Run", runFrames, 12f, true);
            var dieClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Die", dieFrames, 12f, false);
            // 프레임레이트는 SlimeController의 구간(윈드업/공중/회복) 물리 타이밍과 맞물려 있다.
            var attackClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Attack", attackFrames, SlimeController.AttackFrameRate, false);

            var controller = BuildSlimeAnimatorController(idleClip, runClip, dieClip, attackClip);
            return (controller, idleFrames[0]);
        }

        private static AnimatorController BuildSlimeAnimatorController(AnimationClip idle, AnimationClip run, AnimationClip die, AnimationClip attack)
        {
            string controllerPath = $"{EnemyOutputFolder}/SlimeAnimator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;

            var idleState = stateMachine.AddState("Idle"); idleState.motion = idle;
            var runState = stateMachine.AddState("Run"); runState.motion = run;
            var dieState = stateMachine.AddState("Die"); dieState.motion = die;
            var attackState = stateMachine.AddState("Attack"); attackState.motion = attack;

            stateMachine.defaultState = idleState;

            // Any State는 "이미 그 상태면 자기 자신으로는 전이 안 함"일 뿐 검사를 멈추지 않으므로,
            // 상위 우선순위 상태(Die/Attack)에 머무는 동안 아래 조건들이 새지 않게 서로 제외 조건을 건다.
            AddAnyStateTransition(stateMachine, dieState, ("IsDead", AnimatorConditionMode.If, 0f));

            AddAnyStateTransition(stateMachine, attackState,
                ("IsDead", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.If, 0f));

            AddAnyStateTransition(stateMachine, runState,
                ("IsDead", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("Speed", AnimatorConditionMode.Greater, 0.05f));

            AddAnyStateTransition(stateMachine, idleState,
                ("IsDead", AnimatorConditionMode.IfNot, 0f),
                ("IsAttacking", AnimatorConditionMode.IfNot, 0f),
                ("Speed", AnimatorConditionMode.Less, 0.05f));

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState target,
            params (string parameter, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var transition = stateMachine.AddAnyStateTransition(target);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;

            foreach (var (parameter, mode, threshold) in conditions)
            {
                transition.AddCondition(mode, threshold, parameter);
            }
        }

        private static AnimationClip CreateOrReplaceClip(string folder, string clipName, Sprite[] frames, float frameRate, bool loop)
        {
            string path = $"{folder}/{clipName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var clip = new AnimationClip { frameRate = frameRate };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / frameRate,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // prepare: 스프라이트를 로드하기 전에 해당 파일의 임포트 설정을 맞추는 콜백(선택).
        private static Sprite[] LoadNamedFrames(string folder, string prefix, int startIndex, int endIndexInclusive,
            int padWidth = 1, System.Action<string> prepare = null)
        {
            var list = new List<Sprite>();
            for (int i = startIndex; i <= endIndexInclusive; i++)
            {
                string number = i.ToString().PadLeft(padWidth, '0');
                string path = $"{folder}/{prefix}{number}.png";
                prepare?.Invoke(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning($"[GameAssetSetup] 프레임을 찾지 못함: {path}");
                    continue;
                }

                list.Add(sprite);
            }

            return list.ToArray();
        }

        // 슬라임 프레임 PNG를 단일 스프라이트(PPU 100, 하단 중앙 피벗 = root가 발밑 가운데, 무압축)로 임포트한다.
        private static void EnsureSlimeSpriteImport(string path) { EnsureSingleSpriteImport(path, SpriteAlignment.BottomCenter); }

        // 마법사 프레임은 기존 Player Sword와 같은 300x256 캔버스(중앙 피벗)라 전환해도 발 위치가 어긋나지 않는다.
        private static void EnsureWizardSpriteImport(string path) { EnsureSingleSpriteImport(path, SpriteAlignment.Center); }

        private static void EnsureSingleSpriteImport(string path, SpriteAlignment alignment)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool upToDate = importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                && settings.spriteAlignment == (int)alignment
                && importer.alphaIsTransparency
                && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (upToDate) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // 바닥/벽/플랫폼을 SpriteDrawMode.Tiled로 그리려면 스프라이트 Mesh Type이 Full Rect여야 한다.
        // (Tight면 "Sprite Tiling might not appear correctly" 경고와 함께 타일링이 어긋날 수 있음)
        private static void EnsureFullRectMesh(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType == SpriteMeshType.FullRect) return;

            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Sprite FindSprite(string texturePath, string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
