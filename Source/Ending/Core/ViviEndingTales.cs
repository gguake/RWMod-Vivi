using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ViviEndingTales : IExposable
    {
        public const int VolumeCount = 4;

        private const int RepurchaseBaseCost = 200;
        private const int RepurchaseCostStep = 100;
        private const QualityCategory BookQuality = QualityCategory.Good;

        private static readonly string[] LetterKeys =
        {
            LocalizeString_Letter.VV_Letter_EndingTale1,
            LocalizeString_Letter.VV_Letter_EndingTale2,
            LocalizeString_Letter.VV_Letter_EndingTale3,
            LocalizeString_Letter.VV_Letter_EndingTale4,
        };

        private readonly ViviEndingQuest _ending;

        private int _delivered;
        private int _repurchases;

        public int Delivered => _delivered;
        public bool AllDelivered => _delivered >= VolumeCount;
        public int RepurchaseCost => RepurchaseBaseCost + RepurchaseCostStep * _repurchases;

        public ViviEndingTales(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _delivered, "delivered", 0);
            Scribe_Values.Look(ref _repurchases, "repurchases", 0);
        }

        public static ThingDef VolumeDef(int volume)
        {
            switch (volume)
            {
                case 1: return VVThingDefOf.VV_FairyTaleBookI;
                case 2: return VVThingDefOf.VV_FairyTaleBookII;
                case 3: return VVThingDefOf.VV_FairyTaleBookIII;
                case 4: return VVThingDefOf.VV_FairyTaleBookIV;
                default: return null;
            }
        }

        public bool CanDeliverNext(ArcanePlant_Everflower everflower)
        {
            return !AllDelivered && everflower != null && everflower.AttunementLevel > _delivered;
        }

        public bool DeliverNext(ArcanePlant_Everflower everflower, Faction federation)
        {
            if (AllDelivered) { return false; }

            var map = everflower?.MapHeld ?? Find.AnyPlayerHomeMap;
            if (map == null) { return false; }

            var volume = _delivered + 1;
            var cell = DropBook(volume, map);
            if (!cell.IsValid) { return false; }

            _delivered = volume;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_EndingTaleLabel.Translate(volume),
                LetterKeys[volume - 1].Translate(),
                LetterDefOf.PositiveEvent,
                new LookTargets(cell, map),
                federation,
                _ending.Quest,
                new List<ThingDef>() { VolumeDef(volume) });

            if (AllDelivered)
            {
                _ending.Ruins.Notify_TalesCompleted();
            }

            return true;
        }

        public AcceptanceReport CanRepurchase(Map map)
        {
            if (map == null || !TradeUtility.ColonyHasEnoughSilver(map, RepurchaseCost))
            {
                return "NeedSilverLaunchable".Translate(RepurchaseCost);
            }

            return true;
        }

        public bool Repurchase(int volume, Map map)
        {
            if (volume < 1 || volume > _delivered || !CanRepurchase(map).Accepted) { return false; }

            var cost = RepurchaseCost;
            var cell = DropBook(volume, map);
            if (!cell.IsValid) { return false; }

            TradeUtility.LaunchSilver(map, cost);
            _repurchases++;

            Messages.Message(
                LocalizeString_Message.VV_Message_EndingTaleRedelivered.Translate(VolumeDef(volume).label),
                new LookTargets(cell, map),
                MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private static IntVec3 DropBook(int volume, Map map)
        {
            var def = VolumeDef(volume);
            if (def == null) { return IntVec3.Invalid; }

            var book = ThingMaker.MakeThing(def);
            book.TryGetComp<CompQuality>()?.SetQuality(BookQuality, ArtGenerationContext.Outsider);

            var cell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(cell, map, new[] { book }, forbid: false);
            return cell;
        }

        public void DebugSetDelivered(int delivered)
        {
            _delivered = UnityEngine.Mathf.Clamp(delivered, 0, VolumeCount);
        }
    }
}
