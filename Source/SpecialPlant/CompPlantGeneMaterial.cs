using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_PlantGeneMaterial : CompProperties
    {
        public int totalFactor = 12;
        public int baseFactor = 3;
        public int uniqueFactor = 1;
        public int randomFactor = 10;

        public CompProperties_PlantGeneMaterial()
        {
            compClass = typeof(CompPlantGeneMaterial);
        }
    }

    public class CompPlantGeneMaterial : ThingComp
    {
        public CompProperties_PlantGeneMaterial Props => (CompProperties_PlantGeneMaterial)props;

        public float IngredientR => ingredientR / ingredientWeights;
        public float IngredientD => ingredientD / ingredientWeights;
        public float IngredientH => ingredientH / ingredientWeights;
        public float IngredientC => ingredientC / ingredientWeights;
        public float IngredientW => ingredientW / ingredientWeights;
        public float IngredientM => ingredientM / ingredientWeights;

        private float ingredientR;
        private float ingredientD;
        private float ingredientH;
        private float ingredientC;
        private float ingredientW;
        private float ingredientM;
        private float ingredientWeights;

        public CompPlantGeneMaterial()
        {
            ingredientR = 1;
            ingredientD = 1;
            ingredientH = 1;
            ingredientC = 1;
            ingredientW = 1;
            ingredientM = 1;
            ingredientWeights = 6;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ingredientR, "ingredientR");
            Scribe_Values.Look(ref ingredientD, "ingredientD");
            Scribe_Values.Look(ref ingredientH, "ingredientH");
            Scribe_Values.Look(ref ingredientC, "ingredientC");
            Scribe_Values.Look(ref ingredientW, "ingredientW");
            Scribe_Values.Look(ref ingredientM, "ingredientM");
        }

        public void InitializeIngreident(Map map, int seed)
        {
            var tileInfo = map.TileInfo;

            var rainFactor = Mathf.Clamp01(tileInfo.rainfall / 2600f);
            var temperatureFactor = Mathf.Clamp01((tileInfo.temperature + 20f) / 70f);
            float hillFactor = 0f;
            switch (tileInfo.hilliness)
            {
                case RimWorld.Planet.Hilliness.Undefined:
                case RimWorld.Planet.Hilliness.Flat:
                    hillFactor = 0f;
                    break;
                case RimWorld.Planet.Hilliness.SmallHills:
                    hillFactor = 0.35f;
                    break;

                case RimWorld.Planet.Hilliness.LargeHills:
                    hillFactor = 0.65f;
                    break;

                case RimWorld.Planet.Hilliness.Mountainous:
                case RimWorld.Planet.Hilliness.Impassable:
                    hillFactor = 1.0f;
                    break;
            }

            ingredientR = Props.baseFactor + (int)Mathf.Lerp(0, Props.totalFactor - Props.baseFactor, rainFactor) + Rand.RangeSeeded(0, Props.randomFactor, seed);
            ingredientD = Props.baseFactor + (int)(9 - Mathf.Lerp(0, Props.totalFactor - Props.baseFactor, rainFactor)) + Rand.RangeSeeded(0, Props.randomFactor, seed + 1);
            ingredientH = Props.baseFactor + (int)Mathf.Lerp(0, Props.totalFactor - Props.baseFactor, temperatureFactor) + Rand.RangeSeeded(0, Props.randomFactor, seed + 2);
            ingredientC = Props.baseFactor + (int)(9 - Mathf.Lerp(0, Props.totalFactor - Props.baseFactor, temperatureFactor)) + Rand.RangeSeeded(0, Props.randomFactor, seed + 3);
            ingredientW = Props.uniqueFactor + (int)Mathf.Lerp(0, Props.totalFactor - Props.uniqueFactor, tileInfo.swampiness * 2f) + Rand.RangeSeeded(0, Props.randomFactor, seed + 4);
            ingredientM = Props.uniqueFactor + (int)(9 - Mathf.Lerp(0, Props.totalFactor - Props.uniqueFactor, hillFactor) + Rand.RangeSeeded(0, Props.randomFactor, seed + 5));

            ingredientWeights = ingredientR + ingredientD + ingredientH + ingredientC + ingredientW + ingredientM;
        }

        public override string CompInspectStringExtra()
        {
            if (DebugSettings.godMode)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"R:{ingredientR} D:{ingredientD} H:{ingredientH}");
                sb.Append($"C:{ingredientC} W:{ingredientW} M:{ingredientM}");

                return sb.ToString();
            }
            else
            {
                return base.CompInspectStringExtra();
            }
        }

        public override bool AllowStackWith(Thing other)
        {
            var otherComp = other.TryGetComp<CompPlantGeneMaterial>();
            return otherComp != null;
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            var otherComp = otherStack.TryGetComp<CompPlantGeneMaterial>();
            if (otherComp == null)
            {
                return;
            }

            var parentProposition = parent.stackCount / (parent.stackCount + count);
            var otherProposition = 1f - parentProposition;

            ingredientR = ingredientR * parentProposition + otherComp.ingredientR * otherProposition;
            ingredientD = ingredientD * parentProposition + otherComp.ingredientD * otherProposition;
            ingredientH = ingredientH * parentProposition + otherComp.ingredientH * otherProposition;
            ingredientC = ingredientC * parentProposition + otherComp.ingredientC * otherProposition;
            ingredientW = ingredientW * parentProposition + otherComp.ingredientW * otherProposition;
            ingredientM = ingredientM * parentProposition + otherComp.ingredientM * otherProposition;
        }
    }
}
