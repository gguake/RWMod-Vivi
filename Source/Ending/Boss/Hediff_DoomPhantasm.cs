using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_DoomPhantasm : HediffWithComps
    {
        private Rot4? _deathRotation;
        private IntVec3 _deathCell = IntVec3.Invalid;
        private Vector3 _deathDrawOffset;

        public static void Dissolve(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Destroyed) { return; }

            var dissolving = (DissolvingHornet)ThingMaker.MakeThing(VVThingDefOf.VV_DissolvingHornet);
            dissolving.InitAshen(pawn, null, pawn.Rotation, EndingVfxUtility.OffsetFromCell(pawn.DrawPos, pawn.Position));
            GenSpawn.Spawn(dissolving, pawn.Position, pawn.Map);
            pawn.Destroy(DestroyMode.Vanish);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            pawn.health.overrideDeathOnDownedChance = 1f;
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            pawn.health.overrideDeathOnDownedChance = -1f;
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn.Downed && !pawn.Dead)
            {
                pawn.Kill(null, this);
            }
        }

        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();

            _deathRotation = pawn.Rotation;
            if (pawn.Spawned)
            {
                _deathCell = pawn.Position;
                _deathDrawOffset = EndingVfxUtility.OffsetFromCell(pawn.DrawPos, pawn.Position);
            }

            pawn.inventory?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();
            pawn.apparel?.DestroyAll();
            pawn.carryTracker?.DestroyCarriedThing();
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
