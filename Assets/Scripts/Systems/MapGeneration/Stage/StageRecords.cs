using System;

namespace IBlameYou.Systems
{
    // ChunkStreamer가 청크를 치워도 들고 있는 기록을 저장용으로 옮겨 담은 것 (런 진행 저장, JsonUtility용 배열).
    [Serializable]
    public class StageRecords
    {
        public int[] killed = new int[0];               // 쓰러뜨린 적 (청크 기능 Id)
        public int[] clearedArenas = new int[0];        // 돌파한 전투 구역 (청크 번호)
        public int[] collectedRewards = new int[0];     // 주운 구역 보상
        public int[] takenRelics = new int[0];          // 유물을 고른 유물 상자
        public int[] activatedCheckpoints = new int[0]; // 켠 체크포인트
        public int[] openedGates = new int[0];          // 연 봉인문
        public int[] gateStepChunks = new int[0];       // 두 속성 봉인문: 청크 번호
        public int[] gateStepValues = new int[0];       //   그 청크에서 이미 맞힌 속성 수
        public bool bossCleared;
    }
}
