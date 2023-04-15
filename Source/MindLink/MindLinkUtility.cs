﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class MindLinkUtility
    {
        public static Color MindLinkedPawnColor = new Color(0.8f, 0.95f, 0.95f);

        public static bool HasMindTransmitter(this Pawn pawn)
        {
            return pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_MindTransmitter);
        }

        public static bool TryGetMindTransmitter(this Pawn pawn, out Hediff_MindTransmitter hediff)
        {
            if (pawn?.health?.hediffSet == null) { hediff = null; return false; }

            hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_MindTransmitter) as Hediff_MindTransmitter;
            return hediff != null;
        }

        public static bool TryGetMindLink(this Pawn pawn, out Hediff_MindLink hediff)
        {
            if (pawn?.health?.hediffSet == null) { hediff = null; return false; }

            hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_MindLink) as Hediff_MindLink;
            return hediff != null;
        }

        public static bool TryGetMindLinkMaster(this Pawn pawn, out Pawn master)
        {
            master = GetMindLinkMaster(pawn);
            return master != null;
        }

        public static Pawn GetMindLinkMaster(this Pawn pawn)
        {
            return pawn.TryGetMindLink(out var mindLink) ? mindLink.linker : null;
        }

        public static bool IsMindLinkedVivi(this Pawn pawn)
        {
            return pawn.HasViviGene() && pawn.IsMindLinked();
        }

        public static bool IsMindLinked(this Pawn pawn)
        {
            return pawn.GetMindLinkMaster() != null;
        }

        public static bool CanMakeNewMindLink(this Pawn pawn)
        {
            return pawn.HasViviGene() && !pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_PsychicConfusion);
        }

        public static AcceptanceReport CanDraftVivi(this Pawn pawn)
        {
            // 정착민이 아니거나 비비가 아니면 false
            if (!pawn.IsColonist || !pawn.HasViviGene()) { return false; }

            if (pawn.HasMindTransmitter())
            {
                return true;
            }
            else if (pawn.TryGetMindLinkMaster(out var master) && master.TryGetMindTransmitter(out var mindTransmitter))
            {
                return mindTransmitter.CanControlLinkedPawnsNow;
            }

            return true;
        }

        public static void ApplyTimeAssignmentToLinkedPawns(this Pawn pawn, int hour, TimeAssignmentDef timeAssignmentDef)
        {
            if (pawn.TryGetMindTransmitter(out var mindTransmitter))
            {
                foreach (var linked in mindTransmitter.LinkedPawns)
                {
                    linked.timetable?.SetAssignment(hour, timeAssignmentDef);
                }
            }
        }

        public static bool InViviCommandRange(this Pawn linked, LocalTargetInfo target) 
            => InViviCommandRange(linked, target.Cell);

        public static bool InViviCommandRange(this Pawn linked, IntVec3 target)
        {
            var master = linked.GetMindLinkMaster();
            if (master != null && master.TryGetMindTransmitter(out var mindTransmitter))
            {
                if (linked.MapHeld != master.MapHeld)
                {
                    return false;
                }

                if (mindTransmitter.CanCommandTo(target))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static IEnumerable<FloatMenuOption> GetViviSpecializeFloatMenuOptions(Pawn pawn)
        {
            foreach (var def in DefDatabase<ViviSpecializationDef>.AllDefsListForReading.OrderBy(def => def.uiOrder))
            {
                FloatMenuOption floatMenuOption = new FloatMenuOption(def.LabelCap, delegate
                {
                    if (pawn.TryGetViviGene(out var vivi) && vivi.ViviMindLinkSettings != null)
                    {
                        if (vivi.ViviMindLinkSettings.AssignedSpecialization == VVViviSpecializationDefOf.VV_NoSpecialization)
                        {
                            vivi.ViviMindLinkSettings.AssignedSpecialization = def;
                        }
                        else
                        {
                            // 이미 설정된 특화 헤디프에 경험치가 일정이상 쌓여있다면 확인창을 띄운다
                            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(vivi.ViviMindLinkSettings.AssignedSpecialization.hediff);
                            if (hediff.Severity > 0.5f)
                            {
                                var dialog_MessageBox = Dialog_MessageBox.CreateConfirmation(LocalizeTexts.ConfirmPopupSpecializationChanged.Translate(vivi.ViviMindLinkSettings.AssignedSpecialization.Named("SPEC")), () =>
                                {
                                    vivi.ViviMindLinkSettings.AssignedSpecialization = def;
                                });

                                dialog_MessageBox.buttonBText = "CancelButton".Translate();
                                Find.WindowStack.Add(dialog_MessageBox);
                            }
                            else
                            {
                                vivi.ViviMindLinkSettings.AssignedSpecialization = def;
                            }

                        }
                    }

                }, def.uiIcon, Color.white);

                floatMenuOption.tooltip = new TipSignal(def.description, def.index ^ 0xABCDEF);
                yield return floatMenuOption;
            }
        }
    }
}
