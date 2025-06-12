using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Shootus : ArcanePlant_Turret, IThingHolder, INotifyHauledTo, IConditionalGraphicProvider
    {
        public override Thing Gun => _innerContainer.Count > 0 ? _innerContainer[0] : null;
        private ThingOwner _innerContainer;

        private Thing _reservedWeapon;
        public Thing ReservedWeapon => _reservedWeapon;

        public int GraphicIndex => _innerContainer.Count > 0 ? 1 : 0;

        protected override bool ShouldFlip => Gun != null && TurretTop.CurRotation >= 180;
        public override bool Active => base.Active && _innerContainer.Count > 0;

        public ArcanePlant_Shootus() : base()
        {
            _innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
            Scribe_References.Look(ref _reservedWeapon, "reservedWeapon");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Gun != null)
                {
                    UpdateGunVerbs();
                }
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _reservedWeapon = null;

            if (_innerContainer.Count > 0)
            {
                _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
                _innerContainer.ClearAndDestroyContents(mode);
            }

            base.DeSpawn(mode);
            ResetCurrentTarget();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (Gun != null)
            {
                var topBaseOffset = ArcanePlantModExtension.turretTopBaseOffset;
                if (ArcanePlantModExtension.turretTopBaseFlippable && TurretTop.CurRotation >= 180)
                {
                    topBaseOffset.x = -topBaseOffset.x;
                }

                if (AttackVerb is Verb_LaunchProjectile verbLaunchProjectile)
                {
                    EquipmentUtility.Recoil(Gun.def, verbLaunchProjectile, out var drawOffset, out var angleOffset, _turretTop.CurRotation);
                    _turretTop.DrawTurret(topBaseOffset + drawOffset, ArcanePlantModExtension.turretTopBaseAngle + angleOffset);
                }
                else
                {
                    _turretTop.DrawTurret(topBaseOffset, ArcanePlantModExtension.turretTopBaseAngle);
                }
            }

            base.DrawAt(drawLoc, flip);
        }

        protected override void BeginBurst()
        {
            base.BeginBurst();

            if (Active)
            {
                var manaPerShoot = ArcanePlantModExtension.consumeManaPerVerbShoot * AttackVerb.verbProps.burstShotCount;
                if (manaPerShoot > 0)
                {
                    AddMana(-manaPerShoot);
                }
            }
        }

        protected override void BurstComplete()
        {
            _burstCooldownTicksLeft = (Gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown) * 2.5f).SecondsToTicks();
        }

        private static readonly Texture2D EquipCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_EquipShootus");
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
                command_unequipWeapon.defaultLabel = LocalizeString_Command.VV_Command_UnequipWeapon.Translate();
                command_unequipWeapon.defaultDesc = new StringBuilder().Append(Gun.LabelCap).Append(": ").Append(gunDef.description.CapitalizeFirst()).ToString();
                command_unequipWeapon.icon = gunDef.uiIcon;
                command_unequipWeapon.iconAngle = gunDef.uiIconAngle;
                command_unequipWeapon.iconOffset = gunDef.uiIconOffset;
                command_unequipWeapon.action = () =>
                {
                    EquipGun(null);
                };

                if (Faction != Faction.OfPlayer)
                {
                    command_unequipWeapon.Disable("CannotOrderNonControlled".Translate());
                }

                yield return command_unequipWeapon;
            }
            else if (ReservedWeapon != null)
            {
                Command_Action command_cancelReserve = new Command_Action();
                command_cancelReserve.icon = CancelCommandTex;
                command_cancelReserve.defaultLabel = LocalizeString_Command.VV_Command_CancelReserveWeapon.Translate();
                command_cancelReserve.defaultDesc = LocalizeString_Command.VV_Command_CancelReserveWeaponDesc.Translate();
                command_cancelReserve.action = () =>
                {
                    _reservedWeapon = null;
                };

                yield return command_cancelReserve;
            }
            else
            {
                var command_equipWeapon = new Command_Action();
                command_equipWeapon.icon = EquipCommandTex;
                command_equipWeapon.iconDrawScale = 1.5f;
                command_equipWeapon.defaultLabel = LocalizeString_Command.VV_Command_EquipWeapon.Translate();
                command_equipWeapon.defaultDesc = LocalizeString_Command.VV_Command_EquipWeaponDesc.Translate();
                command_equipWeapon.action = () =>
                {
                    Find.Targeter.BeginTargeting(new TargetingParameters()
                    {
                        canTargetPawns = false,
                        canTargetBuildings = false,
                        canTargetItems = true,
                        canTargetAnimals = false,
                        canTargetHumans = false,
                        canTargetMechs = false,
                        canTargetEntities = false,
                        canTargetSubhumans = false,
                        canTargetFires = false,
                        canTargetCorpses = false,
                        canTargetSelf = false,
                        canTargetLocations = false,
                        canTargetPlants = false,
                        canTargetBloodfeeders = false,
                        mapObjectTargetsMustBeAutoAttackable = false,
                        validator = (target) =>
                        {
                            if (!target.HasThing || !CanEquipWeapon(target.Thing) || target.Thing.Map.reservationManager.IsReserved(target.Thing))
                            {
                                return false;
                            }

                            foreach (var building in Map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_Shootus))
                            {
                                var shootus = building as ArcanePlant_Shootus;
                                if (shootus == null) { continue; }

                                if (shootus.ReservedWeapon == target.Thing)
                                {
                                    return false;
                                }
                            }

                            return true;
                        },

                    }, (target) =>
                    {
                        if (target.HasThing)
                        {
                            _reservedWeapon = target.Thing;
                        }

                    }, (target) =>
                    {
                        if (target.HasThing)
                        {
                            Widgets.MouseAttachedLabel(target.Label);
                        }
                    });
                };

                yield return command_equipWeapon;
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_setRotationZero = new Command_Action();
                command_setRotationZero.defaultLabel = "DEV: Set rotation zero";
                command_setRotationZero.action = () =>
                {
                    TurretTop.CurRotation = 0;
                };
                yield return command_setRotationZero;

                Command_Action command_addRotation = new Command_Action();
                command_addRotation.defaultLabel = "DEV: Add rotation";
                command_addRotation.action = () =>
                {
                    TurretTop.CurRotation += 10;
                };
                yield return command_addRotation;
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (Spawned && ReservedWeapon != null && Gun == null)
            {
                sb.AppendInNewLine("Queued".Translate());
                sb.Append(": ");
                sb.Append(ReservedWeapon.LabelCap);
            }

            return sb.ToString();
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void Notify_HauledTo(Pawn hauler, Thing thing, int count)
        {
            EquipGun(thing);
        }

        public void ReserveGun(Thing gun)
        {
            _reservedWeapon = gun;
        }

        public void EquipGun(Thing gun)
        {
            _reservedWeapon = null;

            if (gun == null)
            {
                if (_innerContainer.Count > 0)
                {
                    _innerContainer.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
                    _innerContainer.Clear();
                }

                return;
            }
            else if (CanEquipWeapon(gun))
            {
                UpdateGunVerbs();
            }

            DirtyMapMesh(Map);
        }

        protected bool CanEquipWeapon(Thing gun)
        {
            if (gun == null) { return false; }

            var compEquippable = gun.TryGetComp<CompEquippable>();
            if (compEquippable == null || compEquippable.PrimaryVerb == null || compEquippable.PrimaryVerb.IsMeleeAttack)
            {
                return false;
            }

            var compBiocodable = gun.TryGetComp<CompBiocodable>();
            if (compBiocodable != null && (compBiocodable.Biocoded || compBiocodable.Props.biocodeOnEquip))
            {
                return false;
            }

            return true;
        }

    }
}
