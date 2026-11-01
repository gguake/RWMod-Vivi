namespace VVRace
{
    public enum ViviEndingStage : byte
    {
        None = 0,
        AltarUnlocked = 2,
        Siege = 3,
        Calm = 4,
        Boss = 5,
        Coronation = 6,
        Completed = 7,
        Failed = 8,
    }

    public static class ViviEndingStageUtility
    {
        public static bool IsActiveStage(this ViviEndingStage stage)
        {
            return stage >= ViviEndingStage.Siege && stage <= ViviEndingStage.Coronation;
        }

        public static bool IsFinishedStage(this ViviEndingStage stage)
        {
            return stage == ViviEndingStage.Completed || stage == ViviEndingStage.Failed;
        }
    }
}
