namespace VVRace
{
    public enum GrowingArcanePlantSensitivity
    {
        None,
        Low,
        Medium,
        High
    }

    public static class GrowingArcanePlantSensitivityExtension
    {
        private const float DangerDamage = 50f / 60000f;
        private const float CriticalDamage = 150f / 60000f;

        public static (float danger, float critical) CalcThreshold(this GrowingArcanePlantSensitivity sensitivity)
        {
            switch (sensitivity)
            {
                case GrowingArcanePlantSensitivity.Low:
                    return (0.2f, 0.05f);

                case GrowingArcanePlantSensitivity.Medium:
                    return (0.35f, 0.12f);

                case GrowingArcanePlantSensitivity.High:
                    return (0.5f, 0.25f);
            }

            return (0f, 0f);
        }

        public static float CalcTickDamage(this GrowingArcanePlantSensitivity sensitivity, float pct)
        {
            if (sensitivity == GrowingArcanePlantSensitivity.None) { return 0f; }

            switch (sensitivity)
            {
                case GrowingArcanePlantSensitivity.Low:
                    if      (pct < 0.05f)   { return CriticalDamage; }
                    else if (pct < 0.20f)   { return DangerDamage; }
                    break;

                case GrowingArcanePlantSensitivity.Medium:
                    if      (pct < 0.12f)   { return CriticalDamage; }
                    else if (pct < 0.35f)   { return DangerDamage; }
                    break;

                case GrowingArcanePlantSensitivity.High:
                    if      (pct < 0.25f)   { return CriticalDamage; }
                    else if (pct < 0.50f)   { return DangerDamage; }
                    break;
            }

            return 0f;
        }
    }
}
