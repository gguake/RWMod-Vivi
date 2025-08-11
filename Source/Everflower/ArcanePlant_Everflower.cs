using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Everflower : ArcanePlant, IGatherableTarget
    {
        public static readonly Material TeleportLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f));

        private static readonly Texture2D TeleportCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_MoveEverflower");
        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public CompEverflower EverflowerComp
        {
            get
            {
                if (_everflowerComp == null)
                {
                    _everflowerComp = GetComp<CompEverflower>();
                }
                return _everflowerComp;
            }
        }
        private CompEverflower _everflowerComp;

        public int AttunementLevel => EverflowerComp.AttunementLevel;

        public bool HasRitualCooldown => _ritualCooldownTick > GenTicks.TicksGame;
        public int CurRitualCooldownTicks => _ritualCooldownTick;

        private int _ritualCooldownTick;
        private int _lastRitualTick;

        public IntVec3? ReservedTeleportCell => _reserveTeleportCell;
        private IntVec3? _reserveTeleportCell;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _ritualCooldownTick, "ritualCooldownTick");
            Scribe_Values.Look(ref _lastRitualTick, "lastRitualTick");
            Scribe_Values.Look(ref _reserveTeleportCell, "reserveTeleportCell");
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Unreserve();
            base.Destroy(mode);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction) => EverflowerComp.AttunementLevel == 0;

        public override ushort PathWalkCostFor(Pawn p)
        {
            if (EverflowerComp.AttunementLevel >= 3) { return 36; }

            return 0;
        }

        private List<FloatMenuOption> _tmpFloatMenuOptions = new List<FloatMenuOption>();
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            bool ValidateCell(LocalTargetInfo target)
            {
                var cell = target.Cell;
                if (cell.DistanceToSquared(Position) > Mathf.Pow(EverflowerComp.Props.teleportRange, 2f)) { return false; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, cell, def)) { return false; }

                return true;
            }

            if (Spawned && (Faction?.IsPlayer ?? false) && EverflowerComp.AttunementLevel >= 1)
            {
                if (!_reserveTeleportCell.HasValue)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = LocalizeString_Command.VV_Command_ReserveTeleportEverflower.Translate(),
                        defaultDesc = LocalizeString_Command.VV_Command_ReserveTeleportEverflowerDesc.Translate(),
                        icon = TeleportCommandTex,
                        action = () =>
                        {
                            Find.Targeter.BeginTargeting(
                                TargetingParameters.ForCell(),
                                action: (target) =>
                                {
                                    _reserveTeleportCell = target.Cell;
                                },
                                highlightAction: (target) =>
                                {
                                    if (ValidateCell(target))
                                    {
                                        GenDraw.DrawTargetHighlight(target.Cell);
                                    }
                                },
                                targetValidator: ValidateCell,
                                mouseAttachment: null,
                                onUpdateAction: (target) =>
                                {
                                    GenDraw.DrawRadiusRing(Position, EverflowerComp.Props.teleportRange);
                                });
                        }
                    };
                }
                else
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = LocalizeString_Command.VV_Command_CancelReservationEverflower.Translate(),
                        defaultDesc = LocalizeString_Command.VV_Command_CancelReservationEverflowerDesc.Translate(),
                        icon = CancelCommandTex,
                        action = () =>
                        {
                            _reserveTeleportCell = default;
                        }
                    };
                }

                if (DebugSettings.godMode)
                {
                    if (HasRitualCooldown)
                    {
                        yield return new Command_Action()
                        {
                            defaultLabel = $"DEV: Reset ritual cooldown",
                            action = () =>
                            {
                                _ritualCooldownTick = GenTicks.TicksGame + 1;
                                _lastRitualTick = GenTicks.TicksGame - 1;
                            }
                        };
                    }
                }
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (_reserveTeleportCell.HasValue)
            {
                var a = this.TrueCenter();
                var b = _reserveTeleportCell.Value.ToVector3Shifted();
                GenDraw.DrawLineBetween(a, b, TeleportLineMat);
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.SurgicalCut)
            {
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void Notify_ArcanePlantPotDespawned()
        {
            if (TeleportRandomly())
            {
                return;
            }

            base.Notify_ArcanePlantPotDespawned();
        }

        public void Notify_TeleportJobCompleted()
        {
            if (_reserveTeleportCell.HasValue)
            {
                if (!Teleport(_reserveTeleportCell.Value))
                {
                    TeleportRandomly();
                }

                _reserveTeleportCell = null;
            }
        }

        public bool TeleportRandomly()
        {
            var cells = Map.AllCells.Where(c => c != Position && ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, c, def)).ToList();
            if (cells.Count > 0)
            {
                if (Teleport(cells.RandomElement()))
                {
                    Messages.Message(LocalizeString_Message.VV_Message_EverflowerRandomTeleported.Translate(), MessageTypeDefOf.NeutralEvent);
                    return true;
                }
            }

            return false;
        }

        public bool Teleport(IntVec3 cell)
        {
            if (!cell.IsValid || !cell.InBounds(Map) || !ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, cell, def))
            {
                return false;
            }

            var previousPosition = Position;
            Position = cell;

            EffecterDef effecterDef = null;
            switch (EverflowerComp.AttunementLevel)
            {
                case 1:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_1_Level;
                    break;
                case 2:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_2_Level;
                    break;
                case 3:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_3_Level;
                    break;
                case 4:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_4_Level;
                    break;
            }

            if (effecterDef != null)
            {
                effecterDef.Spawn(previousPosition, Map);
                effecterDef.SpawnAttached(this, Map);
            }

            return true;
        }

        public bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (!pawn.IsVivi() || pawn.ageTracker.AgeBiologicalYears < 13) { return false; }

            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerImpact))
            {
                return false;
            }

            return true;
        }

        public void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (pawn.IsVivi())
            {
                var ticks = Mathf.Clamp(
                    Mathf.CeilToInt(Rand.Range(60000 * 5, 60000 * 10) * pawn.ageTracker.BiologicalTicksPerTick), 
                    0, 
                    (int)(pawn.ageTracker.AgeBiologicalTicks - 13.01f * 60000 * 60));

                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;

                    Map.GetManaComponent().ChangeEnvironmentMana(Position, ticks / 60000f * 50);
                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }

        public void Notify_RitualComplete(float quality)
        {
            var cooldown = Mathf.CeilToInt(EverflowerComp.Props.ritualCooldownCurve.Evaluate(quality));

            if (cooldown > 0)
            {
                _lastRitualTick = GenTicks.TicksGame;
                _ritualCooldownTick = _lastRitualTick + cooldown;
            }

            Unreserve();
        }

        public void Unreserve()
        {
            _reserveTeleportCell = default;
        }
    }
}
