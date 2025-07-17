using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VVRace.Utility;

namespace VVRace
{
    public class VerbProperties_Steamthrower : VerbProperties
    {
        public DamageDef steamDamageDef;
        public float steamDamageAmount;

        public ThingDef steamProjectile;
        public int steamProjectileSpawnTerm;
        public float steamHeatPerCell;

        public float breathStartOffset;
        public float breathAngleHalf;
        public int breathFriendlyFireSafeDistance;

        public float propagationSpeed;

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

        public bool HasSufficientMana
        {
            get
            {
                if (Bursting)
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / BurstShotCount;
                }
                else
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
                }
            }
        }

        protected override int ShotsPerBurst => BurstShotCount;

        private IntVec3 _targetCell;

        public override bool Available()
        {
            if (!base.Available()) { return false; }

            return HasSufficientMana;
        }

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

            var steam = GenSpawn.Spawn(VVThingDefOf.VV_SteamProjectile, caster.Position, caster.Map) as SteamProjectile;
            steam.caster = caster;
            steam.weaponDef = EquipmentSource?.def;
            steam.damageDef = VerbProps.steamDamageDef;
            steam.damageAmount = VerbProps.steamDamageAmount;
            steam.friendlyFireSafeDistance = VerbProps.breathFriendlyFireSafeDistance;
            steam.heatPerCell = VerbProps.steamHeatPerCell;

            steam.range = VerbProps.range;
            steam.propagationSpeed = 20;
            steam.affectCells = GetArcShapedCells(caster.Map, caster.Position, _targetCell, verbProps.range).ToList();

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
                direction.ToAngleFlat());
        }

        public override void WarmupComplete()
        {
            _targetCell = CellUtility.FindLOSLastCell(
                caster.Map,
                caster.Position,
                caster.Position + ((currentTarget.Cell - caster.Position).ToVector3().normalized * verbProps.range).ToIntVec3(),
                verbProps.range);

            base.WarmupComplete();
        }

        private List<IntVec3> _tmpHighlightColoredCells = new List<IntVec3>();
        private List<IntVec3> _tmpHighlightCells = new List<IntVec3>();
        public override void DrawHighlight(LocalTargetInfo target)
        {
            verbProps.DrawRadiusRing(caster.Position, this);

            if (target.IsValid)
            {
                var lastLOSCell = CellUtility.FindLOSLastCell(
                    caster.Map,
                    caster.Position,
                    caster.Position + ((target.Cell - caster.Position).ToVector3().normalized * verbProps.range).ToIntVec3(),
                    verbProps.range);

                var cells = GetArcShapedCells(caster.Map, caster.Position, lastLOSCell, verbProps.range);
                _tmpHighlightColoredCells.Clear();
                _tmpHighlightCells.Clear();

                foreach (var cell in cells)
                {
                    if (GenSight.LineOfSight(caster.Position, cell, caster.Map))
                    {
                        if (cell.DistanceTo(caster.Position) <= VerbProps.breathFriendlyFireSafeDistance)
                        {
                            _tmpHighlightColoredCells.Add(cell);
                        }
                        else
                        {
                            _tmpHighlightCells.Add(cell);
                        }
                    }
                }

                GenDraw.DrawFieldEdges(_tmpHighlightColoredCells, Color.green);
                GenDraw.DrawFieldEdges(_tmpHighlightCells);
            }
        }

        private void MakeAirPuff(Vector3 loc, Map map, float throwAngle)
        {
            if (loc.ToIntVec3().ShouldSpawnMotesAt(map))
            {
                var dataStatic = FleckMaker.GetDataStatic(
                    loc + new Vector3(Rand.Range(-0.005f, 0.005f), 0f, Rand.Range(-0.005f, 0.005f)),
                    map,
                    VVFleckDefOf.VV_Fleck_Steam,
                    Rand.Range(0.7f, 0.9f));

                dataStatic.rotationRate = Rand.RangeInclusive(-240, 240);
                dataStatic.velocityAngle = throwAngle + 90 + Rand.Range(-VerbProps.breathAngleHalf * 0.75f, VerbProps.breathAngleHalf * 0.75f);
                dataStatic.velocitySpeed = VerbProps.propagationSpeed + Rand.Range(-3f, 3f);
                map.flecks.CreateFleck(dataStatic);
            }
        }

        private IEnumerable<IntVec3> GetArcShapedCells(Map map, IntVec3 center, IntVec3 target, float radius)
        {
            var vector = (target - center).ToVector3();
            return GenRadial.RadialCellsAround(center, radius, false)
                .Where(c =>
                {
                    var l = c.ToVector3() - center.ToVector3();
                    if (Vector3.Angle(l, vector) < VerbProps.breathAngleHalf) { return GenSight.LineOfSight(center, c, map, true); }
                    if (Vector3.Angle(c.ToVector3() + new Vector3(-0.5f, 0f, -0.5f) - center.ToVector3(), vector) < VerbProps.breathAngleHalf) { return GenSight.LineOfSight(center, c, map, true); }
                    if (Vector3.Angle(c.ToVector3() + new Vector3(-0.5f, 0f, 0.5f) - center.ToVector3(), vector) < VerbProps.breathAngleHalf) { return GenSight.LineOfSight(center, c, map, true); }
                    if (Vector3.Angle(c.ToVector3() + new Vector3(0.5f, 0f, -0.5f) - center.ToVector3(), vector) < VerbProps.breathAngleHalf) { return GenSight.LineOfSight(center, c, map, true); }
                    if (Vector3.Angle(c.ToVector3() + new Vector3(0.5f, 0f, 0.5f) - center.ToVector3(), vector) < VerbProps.breathAngleHalf) { return GenSight.LineOfSight(center, c, map, true); }

                    return false;
                });
        }
    }
}
