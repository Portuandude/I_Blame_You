using System.Collections.Generic;
using System.IO;
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
        private const string SlimeRoot = "Assets/2D Platformer Tileset/Sprites/Enemy/Slime";
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

            Debug.Log("[GameAssetSetup] 완료: Player/Slime 애니메이션 + LevelArtConfig 생성/갱신됨. Play를 눌러 확인하세요.");
        }

        private static (AnimatorController controller, Sprite defaultSprite) GeneratePlayerAnimations()
        {
            var idleFrames = LoadNamedFrames($"{CharacterRoot}/idle", "player_sword_idle_", 0, 7);
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

            var idleClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Idle", idleFrames, 10f, true);
            var runClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Run", runFrames, 14f, true);
            var riseClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Rise", new[] { riseSprite }, 1f, true);
            var fallClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Fall", new[] { fallSprite }, 1f, true);
            var dashClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Dash", dashFrames, 18f, false);
            var attackClip = CreateOrReplaceClip(PlayerOutputFolder, "Player_Attack", attackFrames, 14f, false);

            var controller = BuildPlayerAnimatorController(idleClip, runClip, riseClip, fallClip, dashClip, attackClip);
            return (controller, idleFrames[0]);
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
            var idleFrames = LoadNamedFrames($"{SlimeRoot}/idle", "slime_idle_", 0, 10, padWidth: 2);
            var runFrames = LoadNamedFrames($"{SlimeRoot}/run", "slime_run_", 0, 11, padWidth: 2);
            var dieFrames = LoadNamedFrames($"{SlimeRoot}/die", "slime_die_", 0, 12, padWidth: 2);

            if (idleFrames.Length == 0 || runFrames.Length == 0 || dieFrames.Length == 0)
            {
                Debug.LogError("[GameAssetSetup] 슬라임 프레임을 찾지 못했습니다. 임포트 경로를 확인하세요.");
                return (null, null);
            }

            var idleClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Idle", idleFrames, 8f, true);
            var runClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Run", runFrames, 12f, true);
            var dieClip = CreateOrReplaceClip(EnemyOutputFolder, "Slime_Die", dieFrames, 12f, false);

            var controller = BuildSlimeAnimatorController(idleClip, runClip, dieClip);
            return (controller, idleFrames[0]);
        }

        private static AnimatorController BuildSlimeAnimatorController(AnimationClip idle, AnimationClip run, AnimationClip die)
        {
            string controllerPath = $"{EnemyOutputFolder}/SlimeAnimator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;

            var idleState = stateMachine.AddState("Idle"); idleState.motion = idle;
            var runState = stateMachine.AddState("Run"); runState.motion = run;
            var dieState = stateMachine.AddState("Die"); dieState.motion = die;

            stateMachine.defaultState = idleState;

            AddAnyStateTransition(stateMachine, dieState, ("IsDead", AnimatorConditionMode.If, 0f));

            AddAnyStateTransition(stateMachine, runState,
                ("IsDead", AnimatorConditionMode.IfNot, 0f),
                ("Speed", AnimatorConditionMode.Greater, 0.05f));

            AddAnyStateTransition(stateMachine, idleState,
                ("IsDead", AnimatorConditionMode.IfNot, 0f),
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

        private static Sprite[] LoadNamedFrames(string folder, string prefix, int startIndex, int endIndexInclusive, int padWidth = 1)
        {
            var list = new List<Sprite>();
            for (int i = startIndex; i <= endIndexInclusive; i++)
            {
                string number = i.ToString().PadLeft(padWidth, '0');
                string path = $"{folder}/{prefix}{number}.png";
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
