"""Assets/ 아래 새 .cs·.asmdef·폴더에 Unity .meta를 만들고, 빠진 것을 찾는다 (Unity 없이 작업할 때).

    python3 Tools/Dev/unity_meta.py check            → 이번 작업(origin/develop 이후)에 새로 만든 .cs·.asmdef와 그 상위 폴더 중 .meta가 없는 것
    python3 Tools/Dev/unity_meta.py check --all      → 저장소 전체 (예전 파일 중 사람 PC에서만 .meta가 생긴 것도 나온다. 그건 만들지 말 것:
                                                       사람 PC의 추적 안 된 .meta와 충돌해 pull이 막힌다)
    python3 Tools/Dev/unity_meta.py add <경로>...    → 그 파일/폴더(와 .meta 없는 상위 폴더)에 .meta 생성 (이미 있으면 건너뜀)
    python3 Tools/Dev/unity_meta.py add --missing    → check(이번 작업분)에 나온 것 전부 생성

Resources 아래 런타임 로드 PNG는 .meta 없이 두는 것이 이 저장소의 관례라 검사하지 않는다.
"""
import os
import subprocess
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CHECKED = (".cs", ".asmdef")

SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True).stdout.split("\n")


def candidates(everything=False):
    if everything:
        out = git("ls-files", "--cached", "--others", "--exclude-standard", "Assets")
    else:
        # 이번 작업에서 새로 생긴 파일만: origin/develop 이후 추가된 것 + 아직 커밋 안 한 새 파일
        out = git("diff", "--name-only", "--diff-filter=A", "origin/develop...HEAD", "--", "Assets")
        out += git("ls-files", "--others", "--exclude-standard", "Assets")
    out = [f for f in out if f]
    paths = set()
    for f in out:
        if not f.endswith(CHECKED):
            continue  # 스크립트가 들어 있는 폴더만 본다 (Resources 그림 폴더는 관례상 .meta 없이 둔다)
        paths.add(f)
        d = os.path.dirname(f)
        while d and d != "Assets":
            paths.add(d + "/")
            d = os.path.dirname(d)
    return sorted(paths)


def missing(everything=False):
    result = []
    for p in candidates(everything):
        rel = p.rstrip("/")
        if not os.path.exists(os.path.join(ROOT, rel)):
            continue
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            result.append(rel)
    return result


def write_meta(rel):
    full = os.path.join(ROOT, rel)
    meta = full + ".meta"
    if os.path.exists(meta):
        return False
    if os.path.isdir(full):
        text = FOLDER_META
    elif rel.endswith(".asmdef"):
        text = ASMDEF_META
    elif rel.endswith(".cs"):
        text = SCRIPT_META
    else:
        print(f"건너뜀 (.cs·.asmdef·폴더만 지원): {rel}")
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text.format(guid=uuid.uuid4().hex))
    print(f"생성: {rel}.meta")
    return True


def add(paths):
    for p in paths:
        rel = os.path.relpath(os.path.abspath(p), ROOT)
        d = os.path.dirname(rel)
        parents = []
        while d and d != "Assets":
            parents.append(d)
            d = os.path.dirname(d)
        for parent in reversed(parents):
            write_meta(parent)
        write_meta(rel)


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "check":
        gaps = missing(everything="--all" in sys.argv)
        print("\n".join(gaps) if gaps else "빠진 .meta 없음")
        sys.exit(1 if gaps else 0)
    if len(sys.argv) >= 3 and sys.argv[1] == "add":
        add(missing() if sys.argv[2] == "--missing" else sys.argv[2:])
        sys.exit(0)
    print(__doc__)
    sys.exit(2)
