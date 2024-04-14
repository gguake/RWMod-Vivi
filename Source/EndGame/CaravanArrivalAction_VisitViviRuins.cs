using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
    public class CaravanArrivalAction_VisitViviRuins : CaravanArrivalAction
    {
        private MapParent _target;

        public override string Label => LocalizeString_Gizmo.VV_Gizmo_VisitViviRuins.Translate(_target.Label);

        public override string ReportString => "CaravanVisiting".Translate(_target.Label);

        public CaravanArrivalAction_VisitViviRuins()
        {
        }

        public CaravanArrivalAction_VisitViviRuins(WorldObjectCompViviRuins targetComp)
        {
            _target = (MapParent)targetComp.parent;
        }

        public override FloatMenuAcceptanceReport StillValid(Caravan caravan, int destinationTile)
        {
            var floatMenuAcceptanceReport = base.StillValid(caravan, destinationTile);
            if (!floatMenuAcceptanceReport)
            {
                return floatMenuAcceptanceReport;
            }

            if (_target != null && _target.Tile != destinationTile)
            {
                return false;
            }

            return CanVisit(_target);
        }

        public override void Arrived(Caravan caravan)
        {
            if (!_target.HasMap)
            {
                LongEventHandler.QueueLongEvent(delegate
                {
                    DoArrivalAction(caravan);
                }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
            }
            else
            {
                DoArrivalAction(caravan);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _target, "target");
        }

        private void DoArrivalAction(Caravan caravan)
        {
            bool hasMap = _target.HasMap;
            if (!hasMap)
            {
                _target.SetFaction(Faction.OfPlayer);
            }

            var orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(_target.Tile, null);
            var lookTargets = new LookTargets(caravan.PawnsListForReading);
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.UnloadIndividually);

            if (!hasMap)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_ViviRuinsFoundLabel.Translate(),
                    LocalizeString_Letter.VV_Letter_ViviRuinsFound.Translate(), 
                    LetterDefOf.PositiveEvent, 
                    new GlobalTargetInfo(_target.Map.Center, _target.Map));
            }
            else
            {
                Find.LetterStack.ReceiveLetter(
                    "LetterLabelCaravanEnteredMap".Translate(_target), 
                    "LetterCaravanEnteredMap".Translate(caravan.Label, _target).CapitalizeFirst(), 
                    LetterDefOf.NeutralEvent, 
                    lookTargets);
            }
        }

        public static FloatMenuAcceptanceReport CanVisit(MapParent worldObject)
        {
            if (worldObject == null || !worldObject.Spawned || worldObject.GetComponent<WorldObjectCompViviRuins>() == null)
            {
                return false;
            }

            if (worldObject.EnterCooldownBlocksEntering())
            {
                return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(worldObject.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
            }

            return true;
        }
    }
}
