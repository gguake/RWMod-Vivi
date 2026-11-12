using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityQueenVerdict : CompProperties_AbilityEffect
    {
        public ThingDef needleDef;

        public CompProperties_AbilityQueenVerdict()
        {
            compClass = typeof(CompAbilityEffect_QueenVerdict);
        }
    }

    public class CompAbilityEffect_QueenVerdict : CompAbilityEffect
    {
        public new CompProperties_AbilityQueenVerdict Props => (CompProperties_AbilityQueenVerdict)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var caster = parent.pawn;
            if (caster == null || !caster.Spawned || Props.needleDef == null) { return; }

            var needle = (QueenVerdictNeedle)ThingMaker.MakeThing(Props.needleDef);
            needle.Init(caster, target.Cell);
            GenSpawn.Spawn(needle, caster.Position, caster.Map);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            var caster = parent.pawn;
            if (caster == null || !caster.Spawned || !target.IsValid) { return; }

            QueenVerdictNeedle.ComputeLane(caster, target.Cell, out _, out var laneStart, out var direction, out var length);
            GenDraw.DrawFieldEdges(QueenVerdictNeedle.LaneCells(caster.Map, laneStart, direction, length).ToList());
        }
    }
}
