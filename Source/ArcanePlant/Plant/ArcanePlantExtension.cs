using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PassedBulletOverride
    {
        public ThingDef thingDef;
        public DamageDef damageDef;
        public float amount;
        public float armorPenetration;
        public float chance = 1f;
    }

    public class ArcanePlantExtension : DefModExtension
    {
        public int zeroManaDurableTicks = 15000;
        public IntRange zeroManaDamageByChance = new IntRange(0, 0);
        
        public int consumeManaPerVerbShoot = 0;
        public float idleTurnAnglePerTick = 0.1f;
        public Vector3 turretTopBaseOffset = Vector3.zero;
        public float turretTopBaseAngle = 0f;
        public bool turretTopBaseFlippable = false;

        public bool hasRandomDrawScale = true;

        public List<PassedBulletOverride> passedBulletOverrides;

        public ThingDef growingAdjacentFlowerDef;
        public IntRange growingAdjacentFlowerIntervalTicks;
        public float growingAdjacentFlowerChance;
        public float growingAdjacentFlowerRange;
    }
}
