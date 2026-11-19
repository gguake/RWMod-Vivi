namespace VVRace
{
    public enum ViviEndingStage : byte
    {
        None = 0,
        Tales = 1,
        Missive = 2,
        Ruins = 3,
        AltarUnlocked = 4,
        Siege = 5,
        Calm = 6,
        Boss = 7,
        Coronation = 8,
        Completed = 9,
        Failed = 10,
    }

    public static class ViviEndingStageUtility
    {
        public static bool IsActiveStage(this ViviEndingStage stage)
        {
            return stage >= ViviEndingStage.Siege && stage <= ViviEndingStage.Coronation;
        }

        public static bool IsPreCoronationStage(this ViviEndingStage stage)
        {
            return stage >= ViviEndingStage.Siege && stage <= ViviEndingStage.Boss;
        }
    }
}
