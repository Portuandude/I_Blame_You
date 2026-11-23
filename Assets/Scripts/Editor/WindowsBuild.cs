using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IBlameYou.EditorTools
{
    // Tools > I Blame You > Windows 빌드 만들기
    // 에디터 없이 실행되는 Windows용 게임(.exe)을 Builds/Windows/에 만든다 (Builds/는 .gitignore).
    // 쓰는 곳 (Docs/OnlinePlay.md):
    //   - 지금: 친구와 컴퓨터 두 대로 시험. 호스트가 이 .exe를 켜고 Parsec으로 화면을 공유한다 (에디터 Play보다 가볍고 전체 화면이 깔끔하다).
    //   - 나중: 스팀 배포. 이 폴더를 그대로 SteamPipe로 올린다 (스팀 출시 후 친구는 Remote Play Together로 들어온다).
    // 헤드리스: Unity.exe -batchmode -nographics -projectPath "<경로>" -executeMethod IBlameYou.EditorTools.WindowsBuild.Build -quit
    public static class WindowsBuild
    {
        // 지금은 씬 하나에서 PlaytestBootstrap이 게임 전체를 띄운다 (README "실행").
        private static readonly string[] Scenes = { "Assets/Scenes/testScene.unity" };
        private const string OutputDir = "Builds/Windows";
        private const string ExeName = "IBlameYou.exe";

        [MenuItem("Tools/I Blame You/Windows 빌드 만들기")]
        public static void BuildFromMenu()
        {
            if (Build() && !Application.isBatchMode)
                EditorUtility.RevealInFinder(Path.Combine(OutputDir, ExeName));
        }

        public static bool Build() => BuildTo(Path.Combine(OutputDir, ExeName), null);

        // Tools > I Blame You > 데모 빌드 만들기 (2026-11-23 "데모버전 생성", Docs/Demo.md)
        // 1) Generate Sprites And Animations(+Generate Prefabs)를 먼저 돌려 그림·프리팹을 최신으로
        // 2) IBY_DEMO를 켜고(개발자 모드·치트·기능 테스트 없음) 제품 이름 "I Blame You! Demo"로 굽고 (저장이 개발용 빌드와 섞이지 않게)
        // 3) Builds/Demo/IBlameYou_Demo_<버전>/ 폴더와 같은 이름의 .zip을 만든다. 친구에게는 zip 하나만 보내면 된다.
        // 헤드리스·CI(.github/workflows/demo-build.yml): -executeMethod IBlameYou.EditorTools.WindowsBuild.BuildDemo
        public const string DemoDefine = "IBY_DEMO";
        private const string DemoRoot = "Builds/Demo";
        private const string DemoProductSuffix = " Demo";

        public static string DemoName => "IBlameYou_Demo_" + IBlameYou.Core.GameSession.DemoVersion;

        [MenuItem("Tools/I Blame You/데모 빌드 만들기 (친구에게 줄 zip)")]
        public static void BuildDemoFromMenu()
        {
            string zip = BuildDemoZip();
            if (zip != null && !Application.isBatchMode) EditorUtility.RevealInFinder(zip);
        }

        public static void BuildDemo()
        {
            string zip = BuildDemoZip();
            if (Application.isBatchMode) EditorApplication.Exit(zip != null ? 0 : 1);
        }

        private static string BuildDemoZip()
        {
            GameAssetSetup.Generate(); // 끝에서 PrefabSetup.Generate까지 부른다

            string folder = Path.Combine(DemoRoot, DemoName);
            if (Directory.Exists(folder)) Directory.Delete(folder, true); // 지난 빌드 찌꺼기(Builds/는 .gitignore)가 zip에 섞이지 않게
            string originalProduct = PlayerSettings.productName;
            bool ok;
            try
            {
                if (!originalProduct.EndsWith(DemoProductSuffix)) PlayerSettings.productName = originalProduct + DemoProductSuffix;
                ok = BuildTo(Path.Combine(folder, ExeName), new[] { DemoDefine });
            }
            finally
            {
                PlayerSettings.productName = originalProduct; // ProjectSettings가 바뀐 채 남지 않게
                AssetDatabase.SaveAssets();
            }
            if (!ok) return null;

            // 버스트·디버그 기호 폴더는 게임에 필요 없다 (zip을 줄인다)
            foreach (var dir in Directory.GetDirectories(folder, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(dir, true);
            File.WriteAllText(Path.Combine(folder, "읽어 주세요.txt"), FriendReadme(), new System.Text.UTF8Encoding(true));

            string zip = folder + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log($"[WindowsBuild] 데모 zip: {Path.GetFullPath(zip)} ({new FileInfo(zip).Length / (1024 * 1024)} MB)");
            return zip;
        }

        private static string FriendReadme() =>
            $"I Blame You! 데모 {IBlameYou.Core.GameSession.DemoVersion} ({DateTime.Now:yyyy-MM-dd})\r\n\r\n" +
            "1. 이 폴더를 아무 데나 풀고 IBlameYou.exe를 켠다. (\"Windows의 PC 보호\" 창이 뜨면 \"추가 정보\" → \"실행\")\r\n" +
            "2. 한 사람(P1, 키보드: 이동·순간이동)은 \"온라인 2인\" → \"호스트 하기\" → 보이는 주소를 친구에게 보낸다.\r\n" +
            "   처음 방을 열 때 Windows 방화벽이 물으면 \"허용\".\r\n" +
            "3. 친구(P2, 마우스: 공격·마법진)는 \"온라인 2인\" → 주소 칸에 붙여 넣고 \"참가하기\". F10 = 연결 끊기.\r\n" +
            "4. 같은 집이 아니면 둘 다 Radmin VPN(무료)에 같은 네트워크로 들어가서 그 주소(26.x.x.x)를 쓴다.\r\n" +
            "5. 두 사람 모두 같은 데모 버전이어야 한다 (시작 화면 오른쪽 아래 \"데모 0.x\").\r\n\r\n" +
            "한 컴퓨터로 둘이 할 때는 그냥 \"게임 시작\" (P1 키보드, P2 마우스).\r\n" +
            "이야기 건너뛰기: P1 Space + P2 클릭 동시에 꾹 (또는 F12 꾹).\r\n" +
            "이상한 점·버그는 화면을 찍어서 알려 주세요!\r\n";

        private static bool BuildTo(string exePath, string[] extraDefines)
        {
            foreach (var scene in Scenes)
            {
                if (!File.Exists(scene))
                {
                    Debug.LogError($"[WindowsBuild] 씬이 없습니다: {scene}");
                    return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(exePath));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = extraDefines,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WindowsBuild] 빌드 실패: {summary.result}, 오류 {summary.totalErrors}개. Console 창을 확인하세요.");
                if (Application.isBatchMode && extraDefines == null) EditorApplication.Exit(1); // 데모는 BuildDemo가 종료 코드를 정한다
                return false;
            }

            Debug.Log($"[WindowsBuild] 완료: {Path.GetFullPath(options.locationPathName)} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}초)");
            return true;
        }
    }
}
