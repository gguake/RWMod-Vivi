using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_Apocrypha : CompProperties
    {
        public long initialBondCount = 1440000000L;
        public long bondReleaseAmount = 360000000L;
        public int releaseStunTicks = 300;

        public float shellRange = 20f;
        public float shellMinRange = 2.9f;
        public int shellAimTicks = 240;
        public IntRange shellCastIntervalTicks = new IntRange(180, 300);
        public int maxShellAims = 3;

        public int escortFocusTicks = 600;

        public int rangedAttackerMemoryTicks = 1200;

        public int firstDoomSeedDelayTicks = 1500;
        public IntRange doomSeedIntervalTicks = new IntRange(18000, 27000);
        public int doomSeedVolleyStart = 3;
        public int doomSeedVolleyGrowthEvery = 2;
        public int doomSeedVolleyMax = 10;
        public int maxDoomFlowers = 30;
        public int fairySeedEveryVolleys = 3;
        public int maxFairyFlowers = 2;

        public float manaShieldMax = 1500f;
        public int shieldBreakStunTicks = 3000;

        public int needleCount = 3;
        public int needleFirstDelayTicks = 900;
        public IntRange needleCastIntervalTicks = new IntRange(480, 720);
        public IntRange needleVolleyCount = new IntRange(1, 3);
        public float needleFanRadius = 5f;
        public float needleFanHalfAngle2 = 50f;
        public float needleFanHalfAngle3 = 65f;
        public float needleFanMinTargetDistance = 9f;
        public int needleAimTicks = 150;
        public float needleTargetRange = 24f;
        public float needleRange = 32f;
        public float needleHalfWidth = 1.5f;
        public float needleDamage = 80f;
        public float needleArmorPenetration = 0.8f;
        public float needleBreachDamageFactor = 5f;

        public IntRange approachIntervalTicks = new IntRange(9000, 13500);
        public float approachDistance = 9f;
        public float wanderRadius = 10f;
        public IntRange wanderWaitTicks = new IntRange(600, 1200);

        public float escortGuardRadius = 10f;
        public float escortRetaliationRadius = 25f;
        public float flowerGuardRadius = 6f;
        public float flowerGuardRetaliationRadius = 20f;

        public int arrivalHornetCount = 9;
        public int interferenceHornetCount = 6;
        public int interferenceHornetGrowth = 2;
        public int volleyEscortCount = 4;
        public int doomFlowerHornetIntervalTicks = 10000;
        public int doomFlowerHornetCount = 1;
        public FloatRange summonRadiusAroundBoss = new FloatRange(3.5f, 7f);
        public SimpleCurve hornetCountFactorByThreatPoints = new SimpleCurve
        {
            new CurvePoint(500f, 0.5f),
            new CurvePoint(1500f, 0.75f),
            new CurvePoint(2500f, 1f),
            new CurvePoint(5000f, 1.5f),
            new CurvePoint(10000f, 2.2f),
        };

        public int transformTicks = 420;
        public int transformGraphicSwapTicks = 210;
        public int transformStunTicks = 420;

        public float maxVitality = 9000f;
        public List<float> damageFactorByStuckNeedles = new List<float> { 1f, 1.5f, 2f, 4f };

        public int unboundNeedleCount = 3;
        public FloatRange unboundNeedlePlantRadius = new FloatRange(5f, 10f);
        public int unboundNeedleActivateTicks = 60;
        public int unboundNeedleLockOnTicks = 150;
        public int unboundNeedleLockedTicks = 75;
        public float needleStickVitalityDamage = 0.1f;
        public int needleStickStunTicks = 420;
        public int allNeedlesStuckStunTicks = 3600;

        public int breathCooldownTicks = 1200;
        public int breathWarmupTicks = 90;
        public float breathRange = 7f;
        public float breathAngle = 50f;
        public float breathToxicSeverity = 0.25f;
        public float breathAcidBurnDamage = 8f;
        public float breathGasRadius = 3.9f;
        public int meleeIntervalTicks = 60;
        public int smashIntervalTicks = 30;
        public float smashDamage = 400f;

        public SimpleCurve hitPointsFactorByThreatScale = new SimpleCurve
        {
            new CurvePoint(0.1f, 0.4f),
            new CurvePoint(0.3f, 0.55f),
            new CurvePoint(0.6f, 0.75f),
            new CurvePoint(1f, 1f),
            new CurvePoint(1.55f, 1.3f),
            new CurvePoint(2.2f, 1.6f),
        };
        public SimpleCurve doomBloomTimeFactorByThreatScale = new SimpleCurve
        {
            new CurvePoint(0.1f, 2f),
            new CurvePoint(0.3f, 1.6f),
            new CurvePoint(0.6f, 1.25f),
            new CurvePoint(1f, 1f),
            new CurvePoint(1.55f, 0.8f),
            new CurvePoint(2.2f, 0.65f),
        };

        public int channelTicksBound = 5000;
        public int channelTicksUnbound = 3600;
        public float channelRangeAltar = 3.9f;
        public float channelRangeEverflower = 1.9f;

        public CompProperties_Apocrypha()
        {
            compClass = typeof(CompApocrypha);
        }
    }
}
