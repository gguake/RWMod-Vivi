using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ViviMindLinkSettings : IExposable
    {
        public Hediff_MindLink HediffMindLink
        {
            get
            {
                if (cacheDirty)
                {
                    cacheDirty = false;
                    cachedHediff = TryGetHediffMindLink(out var hediff) ? hediff : null;
                }

                return cachedHediff;
            }
        }

        public ViviSpecializationDef AssignedSpecialization
        {
            get
            {
                return assignedSpecialization ?? VVViviSpecializationDefOf.VV_NoSpecialization;
            }
            set
            {
                if (assignedSpecialization == value) { return; }

                var beforeSpecialization = assignedSpecialization;
                if (beforeSpecialization?.hediff != null)
                {
                    var hediff = gene.pawn.health?.hediffSet?.GetFirstHediffOfDef(beforeSpecialization.hediff);
                    if (hediff != null)
                    {
                        gene.pawn.health.RemoveHediff(hediff);
                    }
                }

                assignedSpecialization = value;
                var newHediff = assignedSpecialization.hediff;
                if (newHediff != null)
                {
                    gene.pawn.health.AddHediff(newHediff);
                }

                gene.pawn.InterruptCurrentJob();
            }
        }

        public Pawn ReservedToConnectTarget => reservedToConnectTarget;
        public bool ReservedToDisconnect => HediffMindLink?.disconnectReserved ?? false;

        private Gene_Vivi gene;
        private ViviSpecializationDef assignedSpecialization;
        private Pawn reservedToConnectTarget;

        private bool cacheDirty = true;
        private Hediff_MindLink cachedHediff;

        public ViviMindLinkSettings(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public void Tick(int ticks)
        {
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref assignedSpecialization, "assignedSpecialization");
            Scribe_References.Look(ref reservedToConnectTarget, "reservedToConnectTarget");
        }

        public void ResolveReferences(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public bool TryGetHediffMindLink(out Hediff_MindLink hediff)
        {
            hediff = gene.pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_MindLink) as Hediff_MindLink;
            return hediff != null;
        }

        public void Notify_MindLinkCreated()
        {
            reservedToConnectTarget = null;
            cacheDirty = true;

            if (assignedSpecialization == null)
            {
                if (gene.pawn.kindDef is PawnKindDef_Vivi pawnKindDefExt && pawnKindDefExt.initialSpecializationDef != null)
                {
                    AssignedSpecialization = pawnKindDefExt.initialSpecializationDef;

                    var hediff = gene.pawn.health.hediffSet.GetFirstHediff<Hediff_Specialization>();
                    if (hediff != null)
                    {
                        hediff.specializedTicks = pawnKindDefExt.initialSpecializationTicks.RandomInRange;
                    }
                }
                else
                {
                    AssignedSpecialization = VVViviSpecializationDefOf.VV_NoSpecialization;
                }
            }
        }

        public void Notify_MindLinkRemoved()
        {
            cachedHediff = null;
            cacheDirty = true;
        }

        public IEnumerable<Gizmo> GetGizmos()
        {
            var hediff = HediffMindLink;
            if (hediff == null)
            {
                var command_mindLink = new Command_Toggle();
                command_mindLink.isActive = () => reservedToConnectTarget != null;

                if (reservedToConnectTarget != null)
                {
                    command_mindLink.toggleAction = delegate
                    {
                        reservedToConnectTarget = null;
                    };
                }
                else
                {
                    var candidates = gene.pawn.Map.mapPawns.FreeColonistsSpawned.Where(
                        p => p != gene.pawn && !p.Dead && p.Spawned && p.GetStatValue(VVStatDefOf.VV_MindLinkStrength) > 0f && p.TryGetMindTransmitter(out var mindTransmitter) && mindTransmitter.CanAddMindLink).ToList();
                    command_mindLink.disabled = !candidates.Any();
                    command_mindLink.toggleAction = delegate
                    {
                        Find.WindowStack.Add(new FloatMenu(candidates.Select(pawn =>
                        {
                            pawn.TryGetMindTransmitter(out var mindTransmitter);
                            return new FloatMenuOption($"{pawn.Name.ToStringShort} ({mindTransmitter?.UsedBandwidth} / {mindTransmitter?.TotalBandWidth})", delegate
                            {
                                reservedToConnectTarget = pawn;
                            });

                        }).ToList()));
                    };
                }

                command_mindLink.icon = TexCommand.HoldOpen;
                command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                command_mindLink.defaultLabel = (reservedToConnectTarget != null ? LocalizeTexts.CommandCancelConnectMindLink : LocalizeTexts.CommandConnectMindLink).Translate();
                command_mindLink.defaultDesc = LocalizeTexts.CommandConnectMindLinkDesc.Translate();
                yield return command_mindLink;
            }
            else
            {
                var command_mindLink = new Command_Toggle();
                command_mindLink.isActive = () => hediff.disconnectReserved;
                command_mindLink.toggleAction = delegate
                {
                    hediff.disconnectReserved = !hediff.disconnectReserved;
                };
                command_mindLink.icon = TexCommand.SelectCarriedPawn;
                command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                command_mindLink.defaultLabel = (hediff.disconnectReserved ? LocalizeTexts.CommandCancelDisconnectMindLink : LocalizeTexts.CommandDisconnectMindLink).Translate();
                command_mindLink.defaultDesc = LocalizeTexts.CommandDisconnectMindLinkDesc.Translate();
                yield return command_mindLink;
            }
        }
    }
}
