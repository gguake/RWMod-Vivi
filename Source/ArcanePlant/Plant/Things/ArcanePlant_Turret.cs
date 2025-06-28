using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class ArcanePlant_Turret : ArcanePlant, IAttackTarget, IAttackTargetSearcher, ILoadReferenceable
    {
        public ArcanePlantTurretTop TurretTop => _turretTop;
        protected ArcanePlantTurretTop _turretTop;

        Thing IAttackTarget.Thing => this;
        Thing IAttackTargetSearcher.Thing => this;
        float IAttackTarget.TargetPriorityFactor => 1f;

        public LocalTargetInfo CurrentTarget => _currentTarget;
        public LocalTargetInfo TargetCurrentlyAimingAt => _currentTarget;
        protected LocalTargetInfo _currentTarget;

        LocalTargetInfo IAttackTargetSearcher.LastAttackedTarget => _lastAttackedTarget;
        protected LocalTargetInfo _lastAttackedTarget;

        int IAttackTargetSearcher.LastAttackTargetTick => _lastAttackTargetTick;
        protected int _lastAttackTargetTick;

        public abstract Thing Gun { get; }

        Verb IAttackTargetSearcher.CurrentEffectiveVerb => AttackVerb;
        public Verb AttackVerb => GunCompEq.PrimaryVerb;
        protected CompEquippable GunCompEq => Gun.TryGetComp<CompEquippable>();

        public bool WarmingUp => _burstWarmupTicksLeft > 0;
        protected bool _burstActivated;
        protected int _burstCooldownTicksLeft;
        protected int _burstWarmupTicksLeft;

        public virtual bool Active
        {
            get
            {
                return CompMana?.Active ?? true;
            }
        }

        public ArcanePlant_Turret()
        {
            _turretTop = new ArcanePlantTurretTop(this);
        }

        public virtual bool ThreatDisabled(IAttackTargetSearcher disabledFor) => !Active;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_TargetInfo.Look(ref _currentTarget, "currentTarget");

            Scribe_TargetInfo.Look(ref _lastAttackedTarget, "lastAttackedTarget"); ;
            Scribe_Values.Look(ref _lastAttackTargetTick, "lastAttackTargetTick", 0);

            Scribe_Values.Look(ref _burstActivated, "burstActivated");
            Scribe_Values.Look(ref _burstCooldownTicksLeft, "burstCooldownTicksLeft", 0);
            Scribe_Values.Look(ref _burstWarmupTicksLeft, "burstWarmupTicksLeft", 0);
        }

        public override void PostMake()
        {
            base.PostMake();

            _burstCooldownTicksLeft = def.building.turretInitialCooldownTime.SecondsToTicks();
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

        protected override void Tick()
        {
            if (Active && Spawned && !Destroyed)
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
                    if (_burstCooldownTicksLeft <= 0 && this.IsHashIntervalTick(30))
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

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (Spawned && Active)
            {
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
        }

        public override AcceptanceReport ClaimableBy(Faction by)
        {
            return base.ClaimableBy(by) && !Active;
        }

        protected void TryActivateBurst()
        {
            _burstActivated = true;
            TryStartShootSomething(canBeginBurstImmediately: true);
        }

        protected void TryStartShootSomething(bool canBeginBurstImmediately)
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

        protected LocalTargetInfo TryFindNewTarget()
        {
            var faction = Faction;
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

            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, targetScanFlags, IsValidTarget);
        }

        protected virtual bool IsValidTarget(Thing thing)
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
            if (!Active) { return; }

            AttackVerb.TryStartCastOn(_currentTarget);

            _lastAttackTargetTick = Find.TickManager.TicksGame;
            _lastAttackedTarget = _currentTarget;
        }

        protected virtual void BurstComplete()
        {
            if (def.building.turretBurstCooldownTime >= 0f)
            {
                _burstCooldownTicksLeft = def.building.turretBurstCooldownTime.SecondsToTicks();
            }
            else
            {
                _burstCooldownTicksLeft = AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
            }
        }

        protected virtual void ResetCurrentTarget()
        {
            _currentTarget = LocalTargetInfo.Invalid;
            _burstWarmupTicksLeft = 0;
        }

        protected void UpdateGunVerbs()
        {
            var allVerbs = Gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this;
                verb.castCompleteCallback = BurstComplete;
            }
        }
    }
}
