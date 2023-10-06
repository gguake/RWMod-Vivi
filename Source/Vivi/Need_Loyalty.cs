using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Need_Loyalty : Need
    {
        public const float ThresholdViviLow = 0.15f;
        public const float ThresholdViviCritical = 0.05f;
        public const float ThresholdRoyalViviLow = 0.4f;
        public const float ThresholdRoyalViviCritical = 0.25f;

        public float ThresholdLow => (Vivi?.IsRoyal ?? false) ? ThresholdRoyalViviLow : ThresholdViviLow;
        public float ThresholdCritical => (Vivi?.IsRoyal ?? false) ? ThresholdRoyalViviCritical : ThresholdViviCritical;

        public bool IsLow => CurLevelPercentage <= ThresholdLow;
        public bool IsCritical => CurLevelPercentage <= ThresholdCritical;

        public override float MaxLevel => 10000f;

        public override int GUIChangeArrow
        {
            get
            {
                if (NeedOffsetByInterval > 0f)
                {
                    return 1;
                }

                return -1;
            }
        }

        private Vivi Vivi => pawn as Vivi;

        private bool Disabled
        {
            get
            {
                return pawn.Dead || !(pawn is Vivi);
            }
        }

        public override bool ShowOnNeedList => !Disabled && pawn.DevelopmentalStage.Adult();

        private List<Pawn> _exitMapCandidatesList = new List<Pawn>();

        public Need_Loyalty(Pawn pawn)
            : base(pawn)
        {
        }

        public override void SetInitialLevel()
        {
            CurLevelPercentage = 1f;
        }

        public override void NeedInterval()
        {
            CurLevel += NeedOffsetByInterval;

            if (pawn.IsHashIntervalTick(2000))
            {
                if (IsCritical && pawn.IsColonistPlayerControlled && !pawn.InMentalState && !pawn.Downed && pawn.Spawned)
                {
                    if (Vivi.IsRoyal)
                    {
                        if (VVMentalBreakDefOf.GiveUpExit.Worker.BreakCanOccur(pawn))
                        {
                            var allPlayerPawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction_NoCryptosleep;
                            var royalCount = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction_NoCryptosleep.Count(v => v is Vivi vivi && vivi.IsRoyal);
                            if (royalCount > 1)
                            {
                                foreach (var affected in pawn.Map.mapPawns.AllPawnsSpawned.Where(v => v is Vivi vivi && vivi.Faction == pawn.Faction && !vivi.IsRoyal))
                                {
                                    if (affected.needs.TryGetNeed<Need_Loyalty>()?.IsLow ?? false)
                                    {
                                        if (VVMentalBreakDefOf.GiveUpExit.Worker.BreakCanOccur(affected))
                                        {
                                            _exitMapCandidatesList.Add(affected);
                                        }
                                    }
                                }

                                // TODO: 나중에 여왕 비비 따라가도록 수정
                                if (_exitMapCandidatesList.Count >= 2)
                                {
                                    VVMentalBreakDefOf.GiveUpExit.Worker.TryStart(pawn, LocalizeTexts.MentalStateReason_Loyalty.Translate(), false);

                                    foreach (var candidate in _exitMapCandidatesList)
                                    {
                                        VVMentalBreakDefOf.GiveUpExit.Worker.TryStart(candidate, LocalizeTexts.MentalStateReason_Loyalty.Translate(), false);
                                    }
                                }

                                _exitMapCandidatesList.Clear();
                            }
                        }
                    }
                    else
                    {
                        if (VVMentalBreakDefOf.RunWild.Worker.BreakCanOccur(pawn))
                        {
                            VVMentalBreakDefOf.RunWild.Worker.TryStart(pawn, LocalizeTexts.MentalStateReason_Loyalty.Translate(), false);
                        }
                    }
                }
            }
        }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue, float customMargin = -1f, bool drawArrows = true, bool doTooltip = true, Rect? rectForTooltip = null, bool drawLabel = true)
        {
            if (threshPercents == null)
            {
                threshPercents = new List<float>();
            }
            threshPercents.Clear();

            if (!Disabled)
            {
                if (Vivi.IsRoyal)
                {
                    threshPercents.Add(ThresholdRoyalViviCritical);
                    threshPercents.Add(ThresholdRoyalViviLow);
                }
                else
                {
                    threshPercents.Add(ThresholdViviCritical);
                    threshPercents.Add(ThresholdViviLow);
                }
            }

            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip, rectForTooltip, drawLabel);
        }

        public void Notify_InteractWith(float level)
        {
            var value = level * (Vivi.IsRoyal ? 0.3f : 1f) * (level > 0f ? 700f : 300f);
            CurLevel += value;
        }

        private float NeedOffsetByInterval
        {
            get
            {
                if (Disabled || IsFrozen || !pawn.DevelopmentalStage.Adult() || !(pawn is Vivi) || !pawn.IsColonistPlayerControlled || !pawn.Spawned || pawn.Downed || pawn.IsQuestLodger()) { return 0f; }

                var mood = pawn.needs.mood.CurLevelPercentage;
                float offset = (Vivi.IsRoyal ? Mathf.Lerp(-30f, 10f, mood) : Mathf.Lerp(-10f, 5f, mood));
                if (offset < 0)
                {
                    offset *= pawn.GetStatValue(StatDefOf.MentalBreakThreshold);
                }

                return offset;
            }
        }
    }
}
