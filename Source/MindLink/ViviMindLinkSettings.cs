using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ViviMindLinkSettings : IExposable
    {
        public Hediff_MindLink HediffMindLink => TryGetHediffMindLink(out var hediff) ? hediff : null;

        public ViviSpecializationDef AssignedSpecialization
        {
            get
            {
                return assignedSpecialization ?? VVViviSpecializationDefOf.VV_NoSpecialization;
            }
            set
            {
                assignedSpecialization = value;

                gene.pawn.InterruptCurrentJob();
            }
        }

        public Pawn ReservedToConnectPawn => reservedToConnectPawn;

        private Gene_Vivi gene;
        private ViviSpecializationDef assignedSpecialization;
        private Pawn reservedToConnectPawn;

        public ViviMindLinkSettings(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref assignedSpecialization, "assignedSpecialization");
            Scribe_References.Look(ref reservedToConnectPawn, "reservedToConnectPawn");
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

        public void Notify_MindLinkConnected()
        {
            reservedToConnectPawn = null;
        }

        public IEnumerable<Gizmo> GetGizmos()
        {
            var hediff = HediffMindLink;
            if (hediff == null)
            {
                var command_mindLink = new Command_Toggle();
                command_mindLink.isActive = () => reservedToConnectPawn != null;

                if (reservedToConnectPawn != null)
                {
                    command_mindLink.toggleAction = delegate
                    {
                        reservedToConnectPawn = null;
                    };
                }
                else
                {
                    var candidates = gene.pawn.Map.mapPawns.FreeColonistsSpawned.Where(p => p != gene.pawn && p.HasMindTransmitter() && !p.Dead && p.Spawned).ToList();
                    command_mindLink.disabled = !candidates.Any();
                    command_mindLink.toggleAction = delegate
                    {
                        Find.WindowStack.Add(new FloatMenu(candidates.Select(pawn =>
                        {
                            pawn.TryGetMindTransmitter(out var mindTransmitter);
                            return new FloatMenuOption($"{pawn.Name.ToStringShort} ({mindTransmitter?.UsedBandwidth} / {mindTransmitter?.TotalBandWidth})", delegate
                            {
                                reservedToConnectPawn = pawn;
                            });

                        }).ToList()));
                    };
                }

                command_mindLink.icon = TexCommand.HoldOpen;
                command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                command_mindLink.defaultLabel = (reservedToConnectPawn != null ? LocalizeTexts.CommandCancelConnectMindLink : LocalizeTexts.CommandConnectMindLink).Translate();
                command_mindLink.defaultDesc = LocalizeTexts.CommandConnectMindLinkDesc.Translate();
                yield return command_mindLink;
            }
            else
            {
                var command_mindLink = new Command_Toggle();
                command_mindLink.isActive = () => hediff.wantToDisconnect;
                command_mindLink.toggleAction = delegate
                {
                    hediff.wantToDisconnect = !hediff.wantToDisconnect;
                };
                command_mindLink.icon = TexCommand.SelectCarriedPawn;
                command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                command_mindLink.defaultLabel = (hediff.wantToDisconnect ? LocalizeTexts.CommandCancelDisconnectMindLink : LocalizeTexts.CommandDisconnectMindLink).Translate();
                command_mindLink.defaultDesc = LocalizeTexts.CommandDisconnectMindLinkDesc.Translate();
                yield return command_mindLink;
            }
        }
    }
}
