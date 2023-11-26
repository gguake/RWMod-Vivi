using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class TurretPlant : ArtificialPlant, IAttackTarget, IAttackTargetSearcher, ILoadReferenceable
    {
        public Thing Thing => this;

        public float TargetPriorityFactor => 1f;

        private LocalTargetInfo _currentTarget;
        public LocalTargetInfo CurrentTarget => _currentTarget;
        public LocalTargetInfo TargetCurrentlyAimingAt => _currentTarget;

        public Verb CurrentEffectiveVerb => AttackVerb;

        protected LocalTargetInfo _lastAttackedTarget;
        public LocalTargetInfo LastAttackedTarget => _lastAttackedTarget;

        protected int _lastAttackTargetTick;
        public int LastAttackTargetTick => _lastAttackTargetTick;

        protected Thing _gun;
        public CompEquippable GunCompEq => _gun.TryGetComp<CompEquippable>();
        public Verb AttackVerb => GunCompEq.PrimaryVerb;


        protected TurretPlantTop _turretTop;
        public TurretPlantTop TurretTop => _turretTop;

        private bool _burstActivated;
        private int _burstCooldownTicksLeft;
        private int _burstWarmupTicksLeft;

        public bool Active
        {
            get
            {
                return EnergyChargeRatio > 0f;
            }
        }

        public bool WarmingUp => _burstWarmupTicksLeft > 0;

        public TurretPlant()
        {
            _turretTop = new TurretPlantTop(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref _lastAttackedTarget, "lastAttackedTarget");
            Scribe_Values.Look(ref _lastAttackTargetTick, "lastAttackTargetTick", 0);

            Scribe_TargetInfo.Look(ref _currentTarget, "currentTarget");
            Scribe_Deep.Look(ref _gun, "gun");

            Scribe_Values.Look(ref _burstActivated, "burstActivated");
            Scribe_Values.Look(ref _burstCooldownTicksLeft, "burstCooldownTicksLeft", 0);
            Scribe_Values.Look(ref _burstWarmupTicksLeft, "burstWarmupTicksLeft", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_gun == null)
                {
                    Log.Error("Turret had null gun after loading. Recreating.");
                    MakeGun();
                }
                else
                {
                    UpdateGunVerbs();
                }
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            _burstCooldownTicksLeft = def.building.turretInitialCooldownTime.SecondsToTicks();
            MakeGun();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _turretTop.SetRotationFromOrientation();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            ResetCurrentTarget();
        }

        public override void Tick()
        {
            base.Tick();

            if (Active && Spawned)
            {
                GunCompEq.verbTracker.VerbsTick();
                if (AttackVerb.state == VerbState.Bursting)
                {
                    return;
                }

                _burstActivated = false;

                if (WarmingUp)
                {
                    _burstWarmupTicksLeft--;
                    if (_burstWarmupTicksLeft == 0)
                    {
                        BeginBurst();
                    }
                }
                else
                {
                    if (_burstCooldownTicksLeft > 0)
                    {
                        _burstCooldownTicksLeft--;
                    }
                    if (_burstCooldownTicksLeft <= 0 && this.IsHashIntervalTick(10))
                    {
                        TryStartShootSomething(canBeginBurstImmediately: true);
                    }
                }

                _turretTop.TurretTopTick();
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        public override void Draw()
        {
            var drawOffset = Vector3.zero;
            float angleOffset = 0f;

            _turretTop.DrawTurret(drawOffset, angleOffset);
            base.Draw();

        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            float range = AttackVerb.verbProps.range;
            if (range < 90f)
            {
                GenDraw.DrawRadiusRing(base.Position, range);
            }

            float num = AttackVerb.verbProps.EffectiveMinRange(allowAdjacentShot: true);
            if (num < 90f && num > 0.1f)
            {
                GenDraw.DrawRadiusRing(base.Position, num);
            }

            if (WarmingUp)
            {
                int degreesWide = (int)((float)_burstWarmupTicksLeft * 0.5f);
                GenDraw.DrawAimPie(this, _currentTarget, degreesWide, (float)def.size.x * 0.5f);
            }
        }

        public override bool ClaimableBy(Faction by, StringBuilder reason = null)
        {
            if (!base.ClaimableBy(by, reason))
            {
                return false;
            }

            if (Active)
            {
                return false;
            }

            return true;
        }

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (!Active)
            {
                return true;
            }

            return false;
        }

        public void MakeGun()
        {
            _gun = ThingMaker.MakeThing(def.building.turretGunDef);
            UpdateGunVerbs();
        }

        public void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (!Spawned || (AttackVerb.ProjectileFliesOverhead() && base.Map.roofGrid.Roofed(base.Position)) || !AttackVerb.Available())
            {
                ResetCurrentTarget();
                return;
            }

            _currentTarget = TryFindNewTarget();
            if (_currentTarget.IsValid)
            {
                var randomInRange = def.building.turretBurstWarmupTime.RandomInRange;
                if (randomInRange > 0f)
                {
                    _burstWarmupTicksLeft = randomInRange.SecondsToTicks();
                }
                else if (canBeginBurstImmediately)
                {
                    BeginBurst();
                }
                else
                {
                    _burstWarmupTicksLeft = 1;
                }
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        public LocalTargetInfo TryFindNewTarget()
        {
            var attackTargetSearcher = this;
            var faction = attackTargetSearcher.Thing.Faction;
            var range = AttackVerb.verbProps.range;

            if (Rand.Value < 0.5f && AttackVerb.ProjectileFliesOverhead() && faction.HostileTo(Faction.OfPlayer) && Map.listerBuildings.allBuildingsColonist.Where(building =>
            {
                var verbRange = AttackVerb.verbProps.EffectiveMinRange(building, this);
                var distance = building.Position.DistanceToSquared(Position);
                return verbRange * verbRange < distance && distance < range * range;

            }).TryRandomElement(out var result))
            {
                return result;
            }

            var targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (!AttackVerb.ProjectileFliesOverhead())
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToAll;
                targetScanFlags |= TargetScanFlags.LOSBlockableByGas;
            }

            if (AttackVerb.IsIncendiary_Ranged())
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }

            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, targetScanFlags, IsValidTarget);
        }

        private bool IsValidTarget(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (Faction == Faction.OfPlayer && pawn.IsPrisoner)
                {
                    return false;
                }

                if (AttackVerb.ProjectileFliesOverhead())
                {
                    var roofDef = Map.roofGrid.RoofAt(thing.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        return false;
                    }
                }

                return !GenAI.MachinesLike(Faction, pawn);
            }
            return true;
        }

        protected virtual void BeginBurst()
        {
            AttackVerb.TryStartCastOn(_currentTarget);

            _lastAttackTargetTick = Find.TickManager.TicksGame;
            _lastAttackedTarget = _currentTarget;

        }

        protected void BurstComplete()
        {
            _burstCooldownTicksLeft = BurstCooldownTime().SecondsToTicks();
        }

        protected float BurstCooldownTime()
        {
            if (def.building.turretBurstCooldownTime >= 0f)
            {
                return def.building.turretBurstCooldownTime;
            }

            return AttackVerb.verbProps.defaultCooldownTime;
        }

        private void ResetCurrentTarget()
        {
            _currentTarget = LocalTargetInfo.Invalid;
            _burstWarmupTicksLeft = 0;
        }

        private void UpdateGunVerbs()
        {
            var allVerbs = _gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this;
                verb.castCompleteCallback = () =>
                {
                    _burstCooldownTicksLeft = BurstCooldownTime().SecondsToTicks();
                };
            }
        }
    }
}
