using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

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
        public float breathFriendlyFireSafeDistance;

        public float propagationSpeed;

        public VerbProperties_Steamthrower()
        {
            verbClass = typeof(Verb_Steamthrower);
        }
    }

    public class Verb_Steamthrower : Verb, IManaWeaponTiming
    {
        private ManaWeaponCastState manaCastState = new ManaWeaponCastState();
        public ManaWeaponCastState ManaCastState => manaCastState;
        public override float WarmupTime => base.WarmupTime * manaCastState.Multiplier;

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg,
            bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return manaCastState.TryStartCast(this, () => base.TryStartCastOn(castTarg, destTarg,
                surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast));
        }

        public VerbProperties_Steamthrower VerbProps => (VerbProperties_Steamthrower)verbProps;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = ManaWeaponCastState.FindManaComp(this);
                }

                return _manaComp;
            }
        }
        [Unsaved]
        private CompMana _manaComp;

        public bool HasSufficientMana => ManaWeaponCastState.HasSufficientMana(this, ManaComp);

        protected override int ShotsPerBurst => BurstShotCount;

        private IntVec3 _targetCell;

        public override bool Available()
        {
            if (!base.Available()) { return false; }

            return ManaComp != null && (ManaWeaponCastState.AppliesTo(this) || HasSufficientMana);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref manaCastState, "manaCastState");
            if (manaCastState == null) { manaCastState = new ManaWeaponCastState(); }
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

            var manaPerShoot = ManaWeaponCastState.ManaCostPerShot(this);
            if (!ManaWeaponCastState.CanPayShot(this, compMana, manaPerShoot)) { return false; }

            lastShotTick = Find.TickManager.TicksGame;

            var damageAmount = VerbProps.steamDamageAmount * (EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f);
            var steam = GenSpawn.Spawn(VVThingDefOf.VV_SteamProjectile, caster.Position, caster.Map) as SteamProjectile;
            steam.caster = caster;
            steam.intendedTarget = currentTarget.Thing;
            steam.weaponDef = EquipmentSource?.def;
            steam.damageDef = VerbProps.steamDamageDef;
            steam.damageAmount = damageAmount;
            steam.friendlyFireSafeDistance = VerbProps.breathFriendlyFireSafeDistance;
            steam.heatPerCell = VerbProps.steamHeatPerCell;

            steam.range = VerbProps.range;
            steam.propagationSpeed = 20;
            steam.affectCells = new List<IntVec3>();
            FillAffectedCells(_targetCell, steam.affectCells);

            compMana.Stored -= manaPerShoot;
            return true;
        }

        public override void BurstingTick()
        {
            var direction = AimVector(_targetCell).normalized;

            MakeAirPuff(
                caster.TrueCenter() + direction * VerbProps.breathStartOffset, 
                caster.Map, 
                direction.ToAngleFlat());
        }

        public override void WarmupComplete()
        {
            _targetCell = currentTarget.Cell;

            base.WarmupComplete();
        }

        private List<IntVec3> _tmpAffectedCells = new List<IntVec3>();
        private List<IntVec3> _tmpHighlightColoredCells = new List<IntVec3>();
        private List<IntVec3> _tmpHighlightCells = new List<IntVec3>();
        public override void DrawHighlight(LocalTargetInfo target)
        {
            verbProps.DrawRadiusRing(caster.Position, this);

            if (target.IsValid)
            {
                // 실제 사격과 같은 계산을 써서 표시와 피해 범위가 어긋나지 않게 한다.
                FillAffectedCells(target.Cell, _tmpAffectedCells);
                _tmpHighlightColoredCells.Clear();
                _tmpHighlightCells.Clear();

                foreach (var cell in _tmpAffectedCells)
                {
                    if (SteamProjectile.WithinSafeDistance(caster.Position, cell, VerbProps.breathFriendlyFireSafeDistance))
                    {
                        _tmpHighlightColoredCells.Add(cell);
                    }
                    else
                    {
                        _tmpHighlightCells.Add(cell);
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

        private Vector3 AimVector(IntVec3 targetCell)
        {
            var vector = (targetCell - caster.Position).ToVector3();
            return vector == Vector3.zero ? caster.Rotation.FacingCell.ToVector3() : vector;
        }

        private static readonly Vector3[] ConeSamplePoints =
        {
            Vector3.zero,
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
        };

        private List<IntVec3> _tmpOrigins = new List<IntVec3>();
        private List<IntVec3> _tmpConeCells = new List<IntVec3>();
        private HashSet<IntVec3> _tmpReachedCells = new HashSet<IntVec3>();
        private List<IntVec3> _tmpSpreadCells = new List<IntVec3>();

        /// <summary>
        /// 증기가 닿는 칸을 시전자와 가까운 순서로 채운다. SteamProjectile이 이 순서대로 퍼뜨린다.
        /// </summary>
        private void FillAffectedCells(IntVec3 targetCell, List<IntVec3> result)
        {
            result.Clear();

            var map = caster.Map;
            var center = caster.Position;
            var aim = AimVector(targetCell);

            // 증기는 시전자 칸과 바로 옆 칸에서 뿜어져 나온다. 벽이나 문틀 옆에서 기울여 쏘는 자리에서도 닿게 하기 위함이다.
            _tmpOrigins.Clear();
            _tmpOrigins.Add(center);
            for (int i = 0; i < 4; ++i)
            {
                var origin = center + GenAdj.CardinalDirections[i];
                if (origin.InBounds(map) && origin.CanBeSeenOverFast(map))
                {
                    _tmpOrigins.Add(origin);
                }
            }

            _tmpConeCells.Clear();
            _tmpReachedCells.Clear();
            foreach (var cell in GenRadial.RadialCellsAround(center, verbProps.range, false))
            {
                if (!cell.InBounds(map) || !InCone(center, cell, aim)) { continue; }

                _tmpConeCells.Add(cell);
                for (int i = 0; i < _tmpOrigins.Count; ++i)
                {
                    if (GenSight.LineOfSight(_tmpOrigins[i], cell, map, true))
                    {
                        _tmpReachedCells.Add(cell);
                        break;
                    }
                }
            }

            // 좁은 문틈을 지난 증기는 부채꼴 안에서 옆으로 한 칸 번진다.
            _tmpSpreadCells.Clear();
            for (int i = 0; i < _tmpConeCells.Count; ++i)
            {
                var cell = _tmpConeCells[i];
                if (_tmpReachedCells.Contains(cell)) { continue; }

                for (int j = 0; j < 4; ++j)
                {
                    var adjacent = cell + GenAdj.CardinalDirections[j];
                    if (_tmpReachedCells.Contains(adjacent) && adjacent.CanBeSeenOverFast(map))
                    {
                        _tmpSpreadCells.Add(cell);
                        break;
                    }
                }
            }
            _tmpReachedCells.AddRange(_tmpSpreadCells);

            for (int i = 0; i < _tmpConeCells.Count; ++i)
            {
                if (_tmpReachedCells.Contains(_tmpConeCells[i]))
                {
                    result.Add(_tmpConeCells[i]);
                }
            }
        }

        private bool InCone(IntVec3 center, IntVec3 cell, Vector3 aim)
        {
            var offset = (cell - center).ToVector3();
            for (int i = 0; i < ConeSamplePoints.Length; ++i)
            {
                if (Vector3.Angle(offset + ConeSamplePoints[i], aim) < VerbProps.breathAngleHalf) { return true; }
            }

            return false;
        }
    }
}
