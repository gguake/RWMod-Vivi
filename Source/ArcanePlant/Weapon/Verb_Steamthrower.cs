using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Verb_Steamthrower : Verb
    {
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
            return true;
        }

        public override void BurstingTick()
        {
            var vector = _targetCell.ToVector3Shifted() - caster.TrueCenter();
            var direction = vector.normalized;
            MakeAirPuff(caster.TrueCenter() + direction * verbProps.beamStartOffset, caster.Map, direction.ToAngleFlat(), verbProps.range * direction);
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
                dataStatic.velocityAngle = throwAngle + Rand.Range(-10, 10);
                dataStatic.velocitySpeed = Rand.Range(0f, 0.8f);
                dataStatic.velocity = inheritVelocity;
                map.flecks.CreateFleck(dataStatic);
            }
        }

    }
}
