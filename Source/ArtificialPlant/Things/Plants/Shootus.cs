using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Shootus : ArtificialPlant, IAttackTarget, IAttackTargetSearcher, ILoadReferenceable, IThingHolder, IConditionalGraphicProvider
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

        protected ShootusTop _shootusTop;
        public ShootusTop ShootusTop => _shootusTop;

        private ThingOwner _innerContainer;
        public CompEquippable GunCompEq => Gun?.TryGetComp<CompEquippable>();
        public Verb AttackVerb => GunCompEq?.PrimaryVerb;

        private Thing _reservedWeapon;
        public Thing ReservedWeapon => _reservedWeapon;

        private bool _burstActivated;
        private int _burstCooldownTicksLeft;
        private int _burstWarmupTicksLeft;

        protected override bool CanFlip => false;

        public bool Active
        {
            get
            {
                return Gun != null && EnergyChargeRatio > 0f;
            }
        }

        public bool WarmingUp => _burstWarmupTicksLeft > 0;

        public Thing Gun
        {
            get
            {
                if (_innerContainer.Count > 0)
                {
                    return _innerContainer[0];
                }

                return null;
            }
            protected set
            {
                if (value == null)
                {
                    _innerContainer.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
                    _innerContainer.Clear();
                }
                else
                {
                    var comp = value.TryGetComp<CompEquippable>();
                    if (comp == null || comp.PrimaryVerb == null || comp.PrimaryVerb.IsMeleeAttack)
                    {
                        return;
                    }

                    if (_innerContainer.Count > 0)
                    {
                        _innerContainer.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
                        _innerContainer.Clear();
                    }

                    if (!_innerContainer.TryAddOrTransfer(value))
                    {
                        Log.Message($"Treid to add gun to shootus but failed");
                    }

                    UpdateGunVerbs();
                }

                DirtyMapMesh(Map);
            }
        }

        public int GraphicIndex => Gun != null ? 1 : 0;

        public Shootus()
        {
            _innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
            _shootusTop = new ShootusTop(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref _lastAttackedTarget, "lastAttackedTarget");
            Scribe_Values.Look(ref _lastAttackTargetTick, "lastAttackTargetTick", 0);

            Scribe_TargetInfo.Look(ref _currentTarget, "currentTarget");

            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
            Scribe_References.Look(ref _reservedWeapon, "reservedWeapon");

            Scribe_Values.Look(ref _burstActivated, "burstActivated");
            Scribe_Values.Look(ref _burstCooldownTicksLeft, "burstCooldownTicksLeft", 0);
            Scribe_Values.Look(ref _burstWarmupTicksLeft, "burstWarmupTicksLeft", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Gun != null)
                {
                    UpdateGunVerbs();
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _shootusTop.SetRotationFromOrientation();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _reservedWeapon = null;

            if (Gun != null)
            {
                _innerContainer.TryDrop(Gun, ThingPlaceMode.Near, out _);
                Gun = null;
            }

            base.DeSpawn(mode);
            ResetCurrentTarget();
        }

        public override void Tick()
        {
            base.Tick();

            if (Active && Spawned && Gun != null)
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

                _shootusTop.TurretTopTick();
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var drawOffset = Vector3.zero;
            float angleOffset = 0f;

            if (Gun != null)
            {
                _shootusTop.DrawTurret(drawOffset, angleOffset);
            }

            base.DrawAt(drawLoc, flip);

        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (Gun == null) { return; }

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
                GenDraw.DrawAimPie(this, _currentTarget, degreesWide, 0f);
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

        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Gun != null)
            {
                var gunDef = Gun.def;
                var command_unequipWeapon = new Command_Action();
                command_unequipWeapon.defaultLabel = LocalizeTexts.CommandUnequipWeapon.Translate();
                command_unequipWeapon.defaultDesc = new StringBuilder().Append(Gun.LabelCap).Append(": ").Append(gunDef.description.CapitalizeFirst()).ToString();
                command_unequipWeapon.icon = gunDef.uiIcon;
                command_unequipWeapon.iconAngle = gunDef.uiIconAngle;
                command_unequipWeapon.iconOffset = gunDef.uiIconOffset;
                command_unequipWeapon.action = () =>
                {
                    EquipWeapon(null);
                };

                if (Faction != Faction.OfPlayer)
                {
                    command_unequipWeapon.Disable("CannotOrderNonControlled".Translate());
                }

                yield return command_unequipWeapon;
            }

            if (ReservedWeapon != null)
            {
                Command_Action command_cancelReserve = new Command_Action();
                command_cancelReserve.icon = CancelCommandTex;
                command_cancelReserve.defaultLabel = LocalizeTexts.CommandCancelReserveWeapon.Translate();
                command_cancelReserve.defaultDesc = LocalizeTexts.CommandCancelReserveWeaponDesc.Translate();
                command_cancelReserve.action = () =>
                {
                    _reservedWeapon = null;
                };

                yield return command_cancelReserve;
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_addRandomGun = new Command_Action();
                command_addRandomGun.defaultLabel = "DEV: Add random gun";
                command_addRandomGun.action = () =>
                {
                    if (Gun == null)
                    {
                        Find.WindowStack.Add(new FloatMenu(DefDatabase<ThingDef>.AllDefsListForReading.Where(v => v.IsRangedWeapon).Select(def => new FloatMenuOption(def.LabelCap, () =>
                        {
                            var gun = ThingMaker.MakeThing(def);
                            if (gun != null)
                            {
                                Gun = gun;
                            }

                        })).ToList()));
                    }
                };

                yield return command_addRandomGun;
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_reservedWeapon != null)
            {
                sb.AppendInNewLine(LocalizeTexts.InspectorViviShootusWeaponReserved.Translate(_reservedWeapon.LabelShort));
            }

            if (Gun != null)
            {
                sb.AppendInNewLine(LocalizeTexts.InspectorViviShootusWeaponEquipped.Translate(Gun.LabelShort));
            }

            return sb.ToString();
        }

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (!Active)
            {
                return true;
            }

            return false;
        }

        public bool CanAcceptWeaponNow(Thing thing)
        {
            if (Gun != null || _reservedWeapon != null) { return false; }

            var def = thing.def;
            if (!def.IsRangedWeapon) { return false; }

            var compEquippable = thing.TryGetComp<CompEquippable>();
            if (compEquippable == null || compEquippable.PrimaryVerb == null || compEquippable.PrimaryVerb.IsMeleeAttack || compEquippable.PrimaryVerb.ProjectileFliesOverhead())
            {
                return false;
            }
            
            var compBiocodable = thing.TryGetComp<CompBiocodable>();
            if (compBiocodable == null || compBiocodable.Biocoded)
            {
                return false;
            }

            return true;
        }

        public void ReserveWeapon(Thing thing)
        {
            _reservedWeapon = thing;
        }

        public void EquipWeapon(Thing thing)
        {
            _reservedWeapon = null;
            Gun = thing;
        }

        public void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (!Spawned || !Active || !AttackVerb.Available())
            {
                ResetCurrentTarget();
                return;
            }

            _currentTarget = TryFindNewTarget();
            if (_currentTarget.IsValid)
            {
                var randomInRange = AttackVerb.verbProps.warmupTime;
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
            if (!Active) { return LocalTargetInfo.Invalid; }

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
            if (!Active) { return; }

            var energy = ArtificialPlantModExtension.verbShootEnergy;
            if (energy > 0)
            {
                AddEnergy(-energy);
            }

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
            return Gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
        }

        private void ResetCurrentTarget()
        {
            _currentTarget = LocalTargetInfo.Invalid;
            _burstWarmupTicksLeft = 0;
        }

        private void UpdateGunVerbs()
        {
            if (Gun == null)
            {
                return;
            }

            var allVerbs = Gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this;
                verb.castCompleteCallback = () =>
                {
                    if (Spawned && Gun != null)
                    {
                        _burstCooldownTicksLeft = BurstCooldownTime().SecondsToTicks();
                    }
                };
            }
        }
    }
}
