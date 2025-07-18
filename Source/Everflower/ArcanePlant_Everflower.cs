using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EverflowerRitualReservation : IExposable
    {
        public ArcanePlant_Everflower flower;
        public EverflowerRitualDef ritualDef;
        public Pawn casterPawn;
        public Pawn targetPawn;
        public IntVec3 targetCell;

        public EverflowerRitualReservation(ArcanePlant_Everflower flower)
        {
            this.flower = flower;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref ritualDef, "ritualDef");
            Scribe_References.Look(ref casterPawn, "casterPawn");
            Scribe_References.Look(ref targetPawn, "targetPawn");
        }
    }

    [StaticConstructorOnStartup]
    public class ArcanePlant_Everflower : ArcanePlant, IGatherableTarget
    {
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

        public Pawn CurReservedPawn => _reservedRitual?.casterPawn;
        public EverflowerRitualDef CurReservedRitual => _reservedRitual?.ritualDef;

        public EverflowerRitualReservation CurReservationInfo => _reservedRitual;
        private EverflowerRitualReservation _reservedRitual;

        public bool HasRitualCooldown => _ritualCooldownTick > GenTicks.TicksGame;
        private int _ritualCooldownTick;
        private int _lastRitualTick;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _reservedRitual, "reservedRitual", new object[] { this });

            Scribe_Values.Look(ref _ritualCooldownTick, "ritualCooldownTick");
            Scribe_Values.Look(ref _lastRitualTick, "lastRitualTick");
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Unreserve(true);
            base.Destroy(mode);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction) => false;

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

            if (Spawned && (Faction?.IsPlayer ?? false))
            {
                if (CurReservedPawn == null)
                {
                    foreach (var ritualDef in DefDatabase<EverflowerRitualDef>.AllDefsListForReading.Where(v => AttunementLevel >= v.attuneLevel).OrderBy(v => v.uiOrder))
                    {
                        Command_Action command;
                        if (ritualDef.globalCooldown > 0)
                        {
                            command = new Command_ActionWithCooldown()
                            {
                                cooldownPercentGetter = () => HasRitualCooldown ? (float)(GenTicks.TicksGame - _lastRitualTick) / (_ritualCooldownTick - _lastRitualTick) : 1
                            };

                            if (HasRitualCooldown)
                            {
                                var cooldownTicksRemaining = _ritualCooldownTick - GenTicks.TicksGame;
                                command.Disable(LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(cooldownTicksRemaining.ToStringSecondsFromTicks()));
                            }
                        }
                        else
                        {
                            command = new Command_Action();
                        }

                        command.defaultLabel = ritualDef.LabelCap;
                        command.defaultDesc = ritualDef.description;
                        command.icon = ritualDef.uiIcon;

                        if (this.IsBurning())
                        {
                            command.Disable("BurningLower".Translate());
                        }
                        else if (this.IsForbidden(Faction.OfPlayer))
                        {
                            command.Disable("ForbiddenLower".Translate());
                        }

                        if (!command.Disabled)
                        {
                            command.action = () =>
                            {
                                _tmpFloatMenuOptions.Clear();

                                foreach (var pawn in ritualDef.Worker.GetCandidates(this))
                                {
                                    var report = ritualDef.Worker.CanRitual(this, pawn);
                                    if (report.Accepted)
                                    {
                                        _tmpFloatMenuOptions.Add(new FloatMenuOption(pawn.Label, () =>
                                        {
                                            ritualDef.Worker.StartRitual(this, pawn, (ritualReservation) =>
                                            {
                                                if (ritualReservation != null)
                                                {
                                                    _reservedRitual = ritualReservation;
                                                }
                                            });

                                        }, pawn, Color.white));
                                    }
                                    else
                                    {
                                        if (!report.Reason.NullOrEmpty())
                                        {
                                            _tmpFloatMenuOptions.Add(new FloatMenuOption($"{pawn.Label}: {report.Reason}", null, pawn, Color.white));
                                            continue;
                                        }
                                    }
                                }

                                if (!_tmpFloatMenuOptions.Any())
                                {
                                    _tmpFloatMenuOptions.Add(new FloatMenuOption(LocalizeString_Gizmo.VV_Gizmo_OptionLabel_NoRoyalVivi.Translate(), null));
                                }

                                Find.WindowStack.Add(new FloatMenu(_tmpFloatMenuOptions));
                            };
                        }

                        yield return command;
                    }
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
                            Unreserve(true);
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
                    (int)(pawn.ageTracker.AgeBiologicalTicks - 13 * 60000 * 60)) - 1;

                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;

                    Map.GetManaComponent().ChangeEnvironmentMana(Position, ticks / 60000f * 50);
                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }

        public void Notify_RitualComplete(Pawn pawn)
        {
            Messages.Message(
                LocalizeString_Message.VV_Message_AttunementRitualComplete.Translate(_reservedRitual.ritualDef.LabelCap.Named("RITUAL")),
                MessageTypeDefOf.NeutralEvent);

            if (_reservedRitual.ritualDef.globalCooldown > 0)
            {
                _lastRitualTick = GenTicks.TicksGame;
                _ritualCooldownTick = _lastRitualTick + _reservedRitual.ritualDef.globalCooldown;
            }

            Unreserve();
        }

        public void Unreserve(bool cancelled = false)
        {
            if (_reservedRitual == null || _reservedRitual.casterPawn == null) { return; }

            if (cancelled)
            {
                Messages.Message(LocalizeString_Message.VV_Message_ReserveEverflowerCancelled.Translate(_reservedRitual.casterPawn.Named("PAWN")), MessageTypeDefOf.NeutralEvent, historical: false);
            }

            _reservedRitual = null;
        }
    }
}
