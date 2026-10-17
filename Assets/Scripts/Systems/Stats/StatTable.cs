namespace IBlameYou.Systems
{
    // 바탕화면 스탯문서.ods의 값을 그대로 옮겨 둔 표. 수치를 바꿀 때는 문서와 이 파일을 함께 고친다.
    // 배열 순서: 불, 물, 바람, 땅, 무 (Element 순서).
    // 문서 용어: 저항도 → 디버프 저항도(debuffResistance), 속성 저항도 → 속성 방어력(elementDefense).
    // 문서에서 치명타 데미지가 0으로 적힌 몹은 치명타 확률도 0이라 쓰이지 않으므로 150%로 둔다.
    public static class StatTable
    {
        public static StatBlock Player => new StatBlock
        {
            maxHealth = 100, maxMana = 100, maxStamina = 100, moveSpeed = 2.5f,
            debuffResistance = 10, defense = 10, critChance = 0.1f, critDamage = 1.5f,
            elementDamage = new float[] { 25, 25, 25, 25, 25 },
            elementDefense = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 1f, physicalDamage = 25
        };

        public static StatBlock Slime => new StatBlock
        {
            maxHealth = 50, maxMana = 0, maxStamina = 25, moveSpeed = 0.45f, // 문서 0.3의 1.5배 (2026-10-16 사용자 지시)
            debuffResistance = 0, defense = 10, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 25 },
            elementDefense = new float[] { 0, 0, 0, 0, 25 },
            attackSpeed = 0.2f, physicalDamage = 25, experience = 15
        };

        public static StatBlock Goblin => new StatBlock
        {
            maxHealth = 80, maxMana = 0, maxStamina = 80, moveSpeed = 2f,
            debuffResistance = 15, defense = 8, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 30 },
            elementDefense = new float[] { 0, 0, 0, 0, 30 },
            attackSpeed = 0.5f, physicalDamage = 18, experience = 32
        };

        public static StatBlock GoblinArcher => new StatBlock
        {
            maxHealth = 50, maxMana = 0, maxStamina = 50, moveSpeed = 1.5f,
            debuffResistance = 10, defense = 5, critChance = 0.05f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementDefense = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 0.4f, physicalDamage = 40, experience = 48
        };

        public static StatBlock GoblinSoldier => new StatBlock
        {
            maxHealth = 100, maxMana = 0, maxStamina = 100, moveSpeed = 1.7f,
            debuffResistance = 15, defense = 15, critChance = 0.1f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementDefense = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 0.7f, physicalDamage = 30, experience = 56
        };

        public static StatBlock MotherSlime => new StatBlock
        {
            maxHealth = 125, maxMana = 0, maxStamina = 25, moveSpeed = 0.2f,
            debuffResistance = 0, defense = 20, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementDefense = new float[] { -10, 0, 0, 10, 30 },
            attackSpeed = 0.125f, physicalDamage = 52, experience = 180
        };

        public static StatBlock GoblinWarrior => new StatBlock
        {
            maxHealth = 250, maxMana = 50, maxStamina = 125, moveSpeed = 1.4f,
            debuffResistance = 30, defense = 30, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementDefense = new float[] { 30, 15, 5, 10, 50 },
            attackSpeed = 0.5f, physicalDamage = 18, experience = 480
        };

        // 함정 (스탯 문서에 없는 가안). 플레이어 방어력 10 기준 가시 약 18, 화살 약 15, 불기둥 한 번 약 12 + 화상.
        public static StatBlock Trap => new StatBlock
        {
            maxHealth = 1, moveSpeed = 0f, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 20, 0, 0, 0, 0 },
            elementDefense = new float[] { 0, 0, 0, 0, 0 },
            physicalDamage = 20
        };
    }
}
