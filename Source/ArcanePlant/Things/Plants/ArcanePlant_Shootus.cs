using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Shootus : ArcanePlant_Turret, IThingHolder, IConditionalGraphicProvider
    {
        public override Thing Gun => _innerContainer.Count > 0 ? _innerContainer[0] : null;
        private ThingOwner _innerContainer;

        private Thing _reservedWeapon;
        public Thing ReservedWeapon => _reservedWeapon;

        public int GraphicIndex => _innerContainer.Count > 0 ? 1 : 0;

        protected override bool CanFlip => false;
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
                if (AttackVerb is Verb_LaunchProjectile verbLaunchProjectile)
                {
                    EquipmentUtility.Recoil(Gun.def, verbLaunchProjectile, out var drawOffset, out var angleOffset, _turretTop.CurRotation);
                    _turretTop.DrawTurret(ArcanePlantModExtension.turretTopBaseOffset + drawOffset, ArcanePlantModExtension.turretTopBaseAngle + angleOffset);
                }
                else
                {
                    _turretTop.DrawTurret(ArcanePlantModExtension.turretTopBaseOffset, ArcanePlantModExtension.turretTopBaseAngle);
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
            _burstCooldownTicksLeft = (Gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown) * 2f).SecondsToTicks();
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
                command_unequipWeapon.defaultLabel = LocalizeTexts.CommandUnequipWeapon.Translate();
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
                command_cancelReserve.defaultLabel = LocalizeTexts.CommandCancelReserveWeapon.Translate();
                command_cancelReserve.defaultDesc = LocalizeTexts.CommandCancelReserveWeaponDesc.Translate();
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
                command_equipWeapon.defaultLabel = LocalizeTexts.CommandEquipWeapon.Translate();
                command_equipWeapon.defaultDesc = LocalizeTexts.CommandEquipWeaponDesc.Translate();
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
                        canTargetMutants = false,
                        canTargetBloodfeeders = false,
                        mapObjectTargetsMustBeAutoAttackable = false,
                        validator = (target) =>
                        {
                            return target.HasThing && CanEquipWeapon(target.Thing);
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
                Command_Action command_addRandomGun = new Command_Action();
                command_addRandomGun.defaultLabel = "DEV: Add random gun";
                command_addRandomGun.action = () =>
                {
                    if (Gun == null)
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        Find.WindowStack.Add(new FloatMenu(DefDatabase<ThingDef>.AllDefsListForReading.Where(v => v.IsRangedWeapon).Select(def => new FloatMenuOption(def.LabelCap, () =>
                        {
                            var gun = ThingMaker.MakeThing(def);
                            if (gun != null)
                            {
                                EquipGun(gun);
                            }

                        })).ToList()));
                    }
                };

                yield return command_addRandomGun;
            }
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void ReserveGun(Thing gun)
        {
            _reservedWeapon = gun;
        }

        public void EquipGun(Thing gun)
        {
            _reservedWeapon = null;

            if (_innerContainer.Count > 0)
            {
                _innerContainer.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
                _innerContainer.Clear();
            }

            if (gun != null && CanEquipWeapon(gun))
            {
                if (!_innerContainer.TryAddOrTransfer(gun))
                {
                    Log.Message($"Treid to add gun to shootus but failed");
                }

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
            if (compBiocodable != null && compBiocodable.Props.biocodeOnEquip)
            {
                return false;
            }

            return true;
        }
    }
}
