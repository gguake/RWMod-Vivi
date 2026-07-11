using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_ArchiteGermlineImprint : HediffWithComps
    {
        public const float EggProgressFactor = 2.5f;

        private const float SecondGeneChance = 0.45f;
        private const float ThirdGeneChance = 0.1f;
        private const float InitialRemovalChance = 0.1f;
        private const float RemovalChanceIncrease = 0.2f;
        private const float MaximumRemovalChance = 0.9f;

        private int _eggsLaid;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _eggsLaid, "eggsLaid", 0);
        }

        public void Notify_EggProduced(CompViviHatcher hatcher)
        {
            if (hatcher == null)
            {
                return;
            }

            var geneCount = 1;
            if (Rand.Chance(SecondGeneChance))
            {
                geneCount++;
            }
            if (Rand.Chance(ThirdGeneChance))
            {
                geneCount++;
            }

            hatcher.architeGenes = ViviUtility.SelectRandomArchiteGenesForVivi(geneCount);

            var removalChance = Mathf.Min(
                MaximumRemovalChance,
                InitialRemovalChance + RemovalChanceIncrease * _eggsLaid);
            _eggsLaid++;

            if (Rand.Chance(removalChance))
            {
                pawn.health.RemoveHediff(this);
            }
        }
    }
}
