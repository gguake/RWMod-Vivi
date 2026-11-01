using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectDef_FlowerLand : RitualOutcomeEffectDef
    {
        public float radius = 6.6f;
        public TerrainDef terrain;
        public EffecterDef areaEffecter;
    }

    public class RitualOutcomeEffectWorker_FlowerLand : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectDef_FlowerLand Def => (RitualOutcomeEffectDef_FlowerLand)def;

        public RitualOutcomeEffectWorker_FlowerLand() { }
        public RitualOutcomeEffectWorker_FlowerLand(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            var cell = everflower.ConsumeSelectedFlowerLandCell();
            var map = everflower.MapHeld;
            if (map != null && cell.HasValue && cell.Value.InBounds(map))
            {
                Bloom(map, cell.Value);
            }

            if (everflower.Spawned)
            {
                def.effecter?.Spawn(everflower, everflower.Map).Cleanup();
            }

            everflower.Notify_RitualComplete(quality);
        }

        public void Bloom(Map map, IntVec3 cell)
        {
            var flowers = FlowerLandUtility.Apply(map, cell, Def.radius, Def.terrain ?? TerrainDefOf.Soil);
            Def.areaEffecter?.Spawn(cell, map).Cleanup();

            Messages.Message(
                LocalizeString_EndingQueen.VV_Message_FlowerLandBloomed.Translate(flowers.Named("COUNT")),
                new LookTargets(cell, map),
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
