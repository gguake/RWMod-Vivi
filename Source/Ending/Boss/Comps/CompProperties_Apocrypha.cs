using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_Apocrypha : CompProperties
    {
        public float engageRange = 20f;
        public float queenEngageRadius = 13f;
        public int combatMemoryTicks = 600;

        public int escortFocusTicks = 600;

        public int rangedAttackerMemoryTicks = 1200;

        public int firstDoomWaveDelayTicks = 1500;
        public IntRange doomWaveIntervalTicks = new IntRange(18000, 27000);
        public int doomWaveBloomStart = 3;
        public int doomWaveBloomGrowthEvery = 2;
        public int doomWaveBloomMax = 10;
        public int maxDoomFlowers = 30;

        public float manaShieldMax = 2500f;
        public int shieldBreakStunTicks = 1500;
        public int shieldRestoreTicks = 5000;
        public IntRange fairyFlowerCount = new IntRange(3, 5);
        public int fairyFlowerCountGrowthMax = 3;
        public FloatRange fairyFlowerDistance = new FloatRange(6f, 18f);

        public int swarmFairyCount = 8;
        public float swarmAttackRadius = 7f;
        public int swarmStaggerTicks = 95;
        public IntRange swarmAttackCooldownTicks = new IntRange(100, 100);

        public float embraceRadius = 11.4f;
        public int embraceCastTicks = 600;
        public int embraceCooldownTicks = 1000;
        public int embraceFirstDelayTicks = 900;
        public int embraceInterruptRecoverTicks = 600;
        public float embraceBondRecoverPerVivi = 0.04f;

        public float asphodelBodyRadius = 1.2f;
        public float fairyBondInitial = 100000f;
        public float fairyBondDecayStart = 0.25f;
        public float fairyBondClear = 0.05f;
        public FloatRange fairyBondDecayPerHour = new FloatRange(0.015f, 0.06f);
        public float fairyBondDecayRampWidth = 0.2f;

        public float contactLineDrainPerHour = 0.03f;
        public float contactFieldDrainPerHour = 0.075f;
        public float contactLineDamageBondLoss = 1f;
        public float contactFieldDamageBondLoss = 2.5f;
        public float contactHexagonFactor = 3f;

        public float blastBreachRadius = 2.4f;
        public float blastBreachBuildingDamage = 600f;

        public int needleFirstDelayTicks = 900;
        public IntRange needleCastIntervalTicks = new IntRange(480, 720);
        public float needleFanRadius = 5f;
        public float needleFanHalfAngle2 = 50f;
        public float needleFanHalfAngle3 = 65f;
        public int needleAimTicks = 225;
        public float needleTargetRange = 14f;
        public float needleRange = 19f;
        public float needleHalfWidth = 1.5f;
        public float needleDamage = 50f;
        public float needleArmorPenetration = 1.0f;

        public IntRange approachIntervalTicks = new IntRange(4000, 7500);
        public float approachDistance = 9f;
        public float approachRingHalfWidth = 5f;
        public float wanderRadius = 6f;
        public IntRange wanderWaitTicks = new IntRange(600, 1200);

        public float escortGuardRadius = 10f;
        public float escortRetaliationRadius = 25f;
        public float flowerGuardRadius = 6f;
        public float flowerGuardRetaliationRadius = 20f;

        public int arrivalHornetCount = 9;
        public IntRange escortSummonIntervalTicks = new IntRange(12000, 15000);
        public int escortSummonCount = 4;
        public float escortRecallRadius = 15f;
        public int escortRecallCastTicks = 240;
        public int escortRecallCount = 4;
        public int escortRecallCooldownTicks = 3000;
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

        public float maxVitality = 9000f;
        public List<float> damageFactorByStuckNeedles = new List<float> { 1f, 1.5f, 2f, 3f };

        public int queenNeedleCount = 3;
        public FloatRange needlePlantRadius = new FloatRange(5f, 10f);
        public int needleActivateTicks = 60;
        public int needleLockOnTicks = 225;
        public int needleLockedTicks = 112;
        public float needleStickVitalityDamage = 0.1f;
        public int needleStickStunTicks = 420;
        public int needleShakeOffTicks = 7500;
        public int shakeOffHornetCount = 6;

        public int rebirthTicks = 1320;

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
        public SimpleCurve fairyBondFactorByThreatScale = new SimpleCurve
        {
            new CurvePoint(0.1f, 0.1f),
            new CurvePoint(0.3f, 0.3f),
            new CurvePoint(0.6f, 0.5f),
            new CurvePoint(1f, 1f),
            new CurvePoint(1.55f, 1.5f),
            new CurvePoint(2.2f, 2f),
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

        public int channelTicksQueen = 3600;
        public int channelTicksAsphodel = 5000;
        public float channelRangeAltar = 3.9f;
        public float channelRangeEverflower = 1.9f;

        public CompProperties_Apocrypha()
        {
            compClass = typeof(CompApocrypha);
        }
    }
}
