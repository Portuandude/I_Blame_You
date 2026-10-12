namespace IBlameYou.Systems
{
    // 바탕화면 스탯문서.ods의 값을 그대로 옮겨 둔 표. 수치를 바꿀 때는 문서와 이 파일을 함께 고친다.
    // 배열 순서: 불, 물, 바람, 땅, 무 (Element 순서).
    // 문서에서 치명타 데미지가 0으로 적힌 몹은 치명타 확률도 0이라 쓰이지 않으므로 150%로 둔다.
    public static class StatTable
    {
        public static StatBlock Player => new StatBlock
        {
            maxHealth = 100, maxMana = 100, maxStamina = 100, moveSpeed = 2.5f,
            resistance = 10, defense = 10, critChance = 0.1f, critDamage = 1.5f,
            elementDamage = new float[] { 25, 25, 25, 25, 25 },
            elementResistance = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 1f, physicalDamage = 25
        };

        public static StatBlock Slime => new StatBlock
        {
            maxHealth = 50, maxMana = 0, maxStamina = 25, moveSpeed = 0.3f,
            resistance = 0, defense = 10, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 25 },
            elementResistance = new float[] { 0, 0, 0, 0, 25 },
            attackSpeed = 0.2f, physicalDamage = 25, experience = 15
        };

        public static StatBlock Goblin => new StatBlock
        {
            maxHealth = 80, maxMana = 0, maxStamina = 80, moveSpeed = 2f,
            resistance = 15, defense = 8, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 30 },
            elementResistance = new float[] { 0, 0, 0, 0, 30 },
            attackSpeed = 0.5f, physicalDamage = 18, experience = 32
        };

        public static StatBlock GoblinArcher => new StatBlock
        {
            maxHealth = 50, maxMana = 0, maxStamina = 50, moveSpeed = 1.5f,
            resistance = 10, defense = 5, critChance = 0.05f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementResistance = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 0.4f, physicalDamage = 40, experience = 48
        };

        public static StatBlock GoblinSoldier => new StatBlock
        {
            maxHealth = 100, maxMana = 0, maxStamina = 100, moveSpeed = 1.7f,
            resistance = 15, defense = 15, critChance = 0.1f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementResistance = new float[] { 0, 0, 0, 0, 0 },
            attackSpeed = 0.7f, physicalDamage = 30, experience = 56
        };

        public static StatBlock MotherSlime => new StatBlock
        {
            maxHealth = 125, maxMana = 0, maxStamina = 25, moveSpeed = 0.2f,
            resistance = 0, defense = 20, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementResistance = new float[] { -10, 0, 0, 10, 30 },
            attackSpeed = 0.125f, physicalDamage = 52, experience = 180
        };

        public static StatBlock GoblinWarrior => new StatBlock
        {
            maxHealth = 250, maxMana = 50, maxStamina = 125, moveSpeed = 1.4f,
            resistance = 30, defense = 30, critChance = 0f, critDamage = 1.5f,
            elementDamage = new float[] { 0, 0, 0, 0, 0 },
            elementResistance = new float[] { 30, 15, 5, 10, 50 },
            attackSpeed = 0.5f, physicalDamage = 18, experience = 480
        };
    }
}
