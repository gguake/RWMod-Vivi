using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class VerbProperties_Steamthrower : VerbProperties
    {
        public float breathStartOffset;
        public float breathAngleHalf;

        public VerbProperties_Steamthrower()
        {
            verbClass = typeof(Verb_Steamthrower);
        }
    }

    public class Verb_Steamthrower : Verb
    {
        public VerbProperties_Steamthrower VerbProps => (VerbProperties_Steamthrower)verbProps;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = caster.TryGetComp<CompMana>();
                }

                if (_manaComp == null)
                {
                    _manaComp = EquipmentSource?.TryGetComp<CompMana>();
                }

                return _manaComp;
            }
        }
        [Unsaved]
        private CompMana _manaComp;

        protected override int ShotsPerBurst => BurstShotCount;

        private IntVec3 _targetCell;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _targetCell, "targetCell");
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / ShotsPerBurst ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            lastShotTick = Find.TickManager.TicksGame;

            compMana.Stored -= manaPerShoot;
            return true;
        }

        public override void BurstingTick()
        {
            var vector = _targetCell.ToVector3Shifted() - caster.TrueCenter();
            var direction = vector.normalized;
            MakeAirPuff(
                caster.TrueCenter() + direction * VerbProps.breathStartOffset, 
                caster.Map, 
                direction.ToAngleFlat(), 
                verbProps.range * direction);
        }

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            _targetCell = currentTarget.Cell; 
        }

        public static void MakeAirPuff(Vector3 loc, Map map, float throwAngle, Vector3 inheritVelocity)
        {
            if (loc.ToIntVec3().ShouldSpawnMotesAt(map))
            {
                var dataStatic = FleckMaker.GetDataStatic(
                    loc + new Vector3(Rand.Range(-0.005f, 0.005f), 0f, Rand.Range(-0.005f, 0.005f)),
                    map, 
                    VVFleckDefOf.VV_Steam, 
                    Rand.Range(0.6f, 0.8f));

                dataStatic.rotationRate = Rand.RangeInclusive(-240, 240);
                dataStatic.velocityAngle = throwAngle + Rand.Range(-15, 15);
                dataStatic.velocitySpeed = Rand.Range(0f, 0.8f);
                dataStatic.velocity = inheritVelocity;
                map.flecks.CreateFleck(dataStatic);
            }
        }

        private List<IntVec3> _tmpHighlightCells = new List<IntVec3>();
        public override void DrawHighlight(LocalTargetInfo target)
        {
            verbProps.DrawRadiusRing(caster.Position, this);

            var vector = (target.Cell - caster.Position).ToVector3();

            _tmpHighlightCells.Clear();
            _tmpHighlightCells.AddRange(GenRadial.RadialCellsAround(caster.Position, verbProps.range, false)
                .Where(c =>
                {
                    var l = c.ToVector3() - caster.Position.ToVector3();
                    if (Vector3.Angle(l, vector) < VerbProps.breathAngleHalf) { return true; }

                    var lt = c.ToVector3() + new Vector3(-0.5f, 0f, -0.5f) - caster.Position.ToVector3();
                    if (Vector3.Angle(lt, vector) < VerbProps.breathAngleHalf) { return true; }

                    var lb = c.ToVector3() + new Vector3(-0.5f, 0f, 0.5f) - caster.Position.ToVector3();
                    if (Vector3.Angle(lb, vector) < VerbProps.breathAngleHalf) { return true; }

                    var rt = c.ToVector3() + new Vector3(0.5f, 0f, -0.5f) - caster.Position.ToVector3();
                    if (Vector3.Angle(rt, vector) < VerbProps.breathAngleHalf) { return true; }

                    var rb = c.ToVector3() + new Vector3(0.5f, 0f, 0.5f) - caster.Position.ToVector3();
                    if (Vector3.Angle(rb, vector) < VerbProps.breathAngleHalf) { return true; }

                    return false;
                }));

            GenDraw.DrawFieldEdges(_tmpHighlightCells);
        }


    }
}
