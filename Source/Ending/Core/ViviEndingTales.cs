using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace VVRace
{
    public class ViviEndingTales : IExposable
    {
        public const int VolumeCount = 4;

        private const int RepurchaseBaseCost = 200;
        private const int RepurchaseCostStep = 100;
        private const QualityCategory BookQuality = QualityCategory.Legendary;

        private static readonly AccessTools.FieldRef<Book, string> BookTitleRef =
            AccessTools.FieldRefAccess<Book, string>("title");

        private static readonly AccessTools.FieldRef<Book, string> BookDescriptionFlavorRef =
            AccessTools.FieldRefAccess<Book, string>("descriptionFlavor");

        private static readonly AccessTools.FieldRef<Book, string> BookDescriptionRef =
            AccessTools.FieldRefAccess<Book, string>("description");

        private static readonly MethodInfo GenerateFullDescriptionMethod =
            AccessTools.Method(typeof(Book), "GenerateFullDescription");

        private static readonly AccessTools.FieldRef<BookOutcomeDoer_GiveQuest, bool> GiveQuestHasQuestRef =
            AccessTools.FieldRefAccess<BookOutcomeDoer_GiveQuest, bool>("hasQuest");

        private static readonly AccessTools.FieldRef<BookOutcomeDoer_GiveQuest, QuestScriptDef> GiveQuestDefRef =
            AccessTools.FieldRefAccess<BookOutcomeDoer_GiveQuest, QuestScriptDef>("questDef");

        private static readonly string[] VolumeLabelKeys =
        {
            LocalizeString_Etc.VV_FairyTale1_Label,
            LocalizeString_Etc.VV_FairyTale2_Label,
            LocalizeString_Etc.VV_FairyTale3_Label,
            LocalizeString_Etc.VV_FairyTale4_Label,
        };

        private static readonly string[] VolumeTitleKeys =
        {
            LocalizeString_Etc.VV_FairyTale1_Title,
            LocalizeString_Etc.VV_FairyTale2_Title,
            LocalizeString_Etc.VV_FairyTale3_Title,
            LocalizeString_Etc.VV_FairyTale4_Title,
        };

        private static readonly string[] VolumeDescKeys =
        {
            LocalizeString_Etc.VV_FairyTale1_Desc,
            LocalizeString_Etc.VV_FairyTale2_Desc,
            LocalizeString_Etc.VV_FairyTale3_Desc,
            LocalizeString_Etc.VV_FairyTale4_Desc,
        };

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

        public static string VolumeLabel(int volume)
        {
            return volume >= 1 && volume <= VolumeCount ? VolumeLabelKeys[volume - 1].Translate().Resolve() : null;
        }

        public static string VolumeTitle(int volume)
        {
            return volume >= 1 && volume <= VolumeCount ? VolumeTitleKeys[volume - 1].Translate().Resolve() : null;
        }

        public static string VolumeDesc(int volume)
        {
            return volume >= 1 && volume <= VolumeCount ? VolumeDescKeys[volume - 1].Translate().Resolve() : null;
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
                _ending.Quest);

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
                LocalizeString_Message.VV_Message_EndingTaleRedelivered.Translate(VolumeLabel(volume)),
                new LookTargets(cell, map),
                MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private static Book MakeBook(int volume)
        {
            if (volume < 1 || volume > VolumeCount) { return null; }

            var book = ThingMaker.MakeThing(ThingDefOf.Novel) as Book;
            if (book == null) { return null; }

            book.TryGetComp<CompQuality>()?.SetQuality(BookQuality, ArtGenerationContext.Outsider);

            if (book.BookComp != null)
            {
                foreach (var doer in book.BookComp.Doers)
                {
                    if (doer is BookOutcomeDoer_GiveQuest giveQuest)
                    {
                        GiveQuestHasQuestRef(giveQuest) = false;
                        GiveQuestDefRef(giveQuest) = null;
                    }
                }
            }

            BookTitleRef(book) = VolumeTitle(volume);
            BookDescriptionFlavorRef(book) = VolumeDesc(volume);
            BookDescriptionRef(book) = (string)GenerateFullDescriptionMethod.Invoke(book, null);
            return book;
        }

        private static IntVec3 DropBook(int volume, Map map)
        {
            var book = MakeBook(volume);
            if (book == null) { return IntVec3.Invalid; }

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
