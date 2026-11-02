using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class QuestPart_ViviFederationAid : QuestPartActivable
    {
        private const int TickInterval = 250;
        private static readonly IntRange IntervalTicks = new IntRange(90000, 150000);
        private static readonly IntRange CountRange = new IntRange(3, 6);

        public ArcanePlant_Everflower anchor;

        private int _nextTick = -1;

        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);
            _nextTick = Find.TickManager.TicksGame + IntervalTicks.RandomInRange;
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            var now = Find.TickManager.TicksGame;
            if (now % TickInterval != 0 || now < _nextTick) { return; }

            _nextTick = now + IntervalTicks.RandomInRange;

            var map = anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;
            if (map != null)
            {
                TrySendReinforcements(map);
            }
        }

        private void TrySendReinforcements(Map map)
        {
            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation == null) { return; }

            var kinds = new List<PawnKindDef>(3);
            if (VVPawnKindDefOf.VV_ViviGuard != null)
            {
                kinds.Add(VVPawnKindDefOf.VV_ViviGuard);
            }
            if (VVPawnKindDefOf.VV_ViviFirefighter != null)
            {
                kinds.Add(VVPawnKindDefOf.VV_ViviFirefighter);
            }
            if (VVPawnKindDefOf.VV_ViviSniper != null)
            {
                kinds.Add(VVPawnKindDefOf.VV_ViviSniper);
            }
            if (kinds.Count == 0) { return; }

            var altar = ViviEndingUtility.AltarOf(anchor);
            var center = altar != null && altar.Spawned && altar.Map == map ? altar.Position : anchor.PositionHeld;

            var count = CountRange.RandomInRange;
            var pawns = new List<Pawn>(count);
            try
            {
                for (int i = 0; i < count; ++i)
                {
                    var request = new PawnGenerationRequest(kinds.RandomElement(), federation, PawnGenerationContext.NonPlayer, map.Tile, mustBeCapableOfViolence: true);
                    pawns.Add(PawnGenerator.GeneratePawn(request));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to generate federation reinforcements: {ex}");
            }

            if (pawns.Count == 0) { return; }

            DropPodUtility.DropThingsNear(center, map, pawns.Cast<Thing>(), canRoofPunch: false, forbid: false, allowFogged: false, faction: federation);
            LordMaker.MakeNewLord(federation, new LordJob_AssistColony(federation, center), map, pawns);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_EndingFederationAidLabel.Translate(),
                LocalizeString_Letter.VV_Letter_EndingFederationAid.Translate(pawns.Count.Named("COUNT")),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawns),
                federation,
                quest);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref _nextTick, "nextTick", -1);
        }
    }
}
