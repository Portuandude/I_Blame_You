using System.IO;
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

        public static bool Build()
        {
            foreach (var scene in Scenes)
            {
                if (!File.Exists(scene))
                {
                    Debug.LogError($"[WindowsBuild] 씬이 없습니다: {scene}");
                    if (Application.isBatchMode) EditorApplication.Exit(1); // 명령줄 빌드가 exe 없이 성공(0)으로 끝나지 않게
                    return false;
                }
            }

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WindowsBuild] 빌드 실패: {summary.result}, 오류 {summary.totalErrors}개. Console 창을 확인하세요.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return false;
            }

            Debug.Log($"[WindowsBuild] 완료: {Path.GetFullPath(options.locationPathName)} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}초)");
            return true;
        }
    }
}
