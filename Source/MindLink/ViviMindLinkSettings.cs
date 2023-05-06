using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
                var gizmoMindLinkConnect = new Gizmo_MindLinkConnect(this);
                if (ReservedToConnectTarget != null)
                {
                    gizmoMindLinkConnect.toggleAction = delegate
                    {
                        foreach (var setting in gizmoMindLinkConnect.mindLinkSettings)
                        {
                            setting.reservedToConnectTarget = null;
                        }
                    };
                }
                else
                {
                    var candidates = gene.pawn.Map.mapPawns.FreeColonistsSpawned.Where(
                        p => p != gene.pawn && !p.Dead && p.Spawned && p.GetStatValue(VVStatDefOf.VV_MindLinkStrength) > 0f && p.TryGetMindTransmitter(out var mindTransmitter) && mindTransmitter.CanAddMindLink).ToList();

                    gizmoMindLinkConnect.disabled = !candidates.Any();
                    gizmoMindLinkConnect.toggleAction = delegate
                    {
                        Find.WindowStack.Add(new FloatMenu(candidates.Select(pawn =>
                        {
                            if (pawn.TryGetMindTransmitter(out var mindTransmitter))
                            {
                                return new FloatMenuOption($"{pawn.Name.ToStringShort} ({mindTransmitter.UsedBandwidth} / {mindTransmitter.TotalBandWidth})", delegate
                                {
                                    var remainBandwidth = mindTransmitter.TotalBandWidth - mindTransmitter.UsedBandwidth;

                                    for (int i = 0; i < Mathf.Min(remainBandwidth, gizmoMindLinkConnect.mindLinkSettings.Count); ++i)
                                    {
                                        gizmoMindLinkConnect.mindLinkSettings[i].reservedToConnectTarget = pawn;
                                    }
                                });
                            }

                            return null;

                        }).ToList()));
                    };
                }

                gizmoMindLinkConnect.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                gizmoMindLinkConnect.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                gizmoMindLinkConnect.defaultLabel = (ReservedToConnectTarget != null ? LocalizeTexts.CommandCancelConnectMindLink : LocalizeTexts.CommandConnectMindLink).Translate();
                gizmoMindLinkConnect.defaultDesc = LocalizeTexts.CommandConnectMindLinkDesc.Translate();

                yield return gizmoMindLinkConnect;
            }
            else
            {
                var gizmoMindLinkDisconnect = new Gizmo_MindLinkDisconnect(this);
                gizmoMindLinkDisconnect.toggleAction = delegate
                {
                    var active = !gizmoMindLinkDisconnect.currentlyIsActive;
                    foreach (var setting in gizmoMindLinkDisconnect.mindLinkSettings)
                    {
                        setting.HediffMindLink.disconnectReserved = active;
                    }
                };
                gizmoMindLinkDisconnect.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                gizmoMindLinkDisconnect.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                gizmoMindLinkDisconnect.defaultLabel = (hediff.disconnectReserved ? LocalizeTexts.CommandCancelDisconnectMindLink : LocalizeTexts.CommandDisconnectMindLink).Translate();
                gizmoMindLinkDisconnect.defaultDesc = LocalizeTexts.CommandDisconnectMindLinkDesc.Translate();
                yield return gizmoMindLinkDisconnect;
            }
        }
    }
}
