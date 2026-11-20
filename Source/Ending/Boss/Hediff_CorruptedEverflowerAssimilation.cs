using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_CorruptedEverflowerAssimilation : HediffWithComps
    {
        private Rot4? _deathRotation;
        private IntVec3 _deathCell = IntVec3.Invalid;
        private Vector3 _deathDrawOffset;

        // Pawn.Kill은 Notify_PawnDied보다 먼저 Corpse.SpawnSetup을 호출해 InnerPawn.Rotation을 South로 덮어쓴다.
        internal static void CaptureDeathState(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) { return; }

            var hediff = pawn.health?.hediffSet?.GetFirstHediff<Hediff_CorruptedEverflowerAssimilation>();
            if (hediff == null) { return; }

            hediff._deathRotation = pawn.Rotation;
            if (pawn.Spawned)
            {
                hediff._deathCell = pawn.Position;
                hediff._deathDrawOffset = EndingVfxUtility.OffsetFromCell(pawn.DrawPos, pawn.Position);
            }
            else
            {
                hediff._deathCell = IntVec3.Invalid;
                hediff._deathDrawOffset = Vector3.zero;
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            var rotation = _deathRotation ?? pawn?.Rotation ?? Rot4.South;
            var deathCell = _deathCell;
            var deathDrawOffset = _deathDrawOffset;
            _deathRotation = null;
            _deathCell = IntVec3.Invalid;
            _deathDrawOffset = Vector3.zero;

            var corpse = pawn?.Corpse;
            if (corpse == null || !corpse.Spawned) { return; }

            var drawOffset = deathCell.IsValid && deathCell == corpse.Position ? deathDrawOffset : Vector3.zero;
            var dissolving = (DissolvingHornet)ThingMaker.MakeThing(VVThingDefOf.VV_DissolvingHornet);
            dissolving.InitAshen(pawn, corpse, rotation, drawOffset);
            GenSpawn.Spawn(dissolving, corpse.Position, corpse.Map);
        }
    }
}
