using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_DreamumTower : CompProperties
    {
        public int completeProgressTicks;

        public GraphicData graphicData;
        public EffecterDef growthEffect;
        public List<float> graphicChangeProgressPct;

        public FloatRange bigThreatActivatePct;

        public GameConditionDef hazeConditionDef;
        public float hazeActivatePct;
        public int hazeWorldRadiusMax;

        public CompProperties_DreamumTower()
        {
            compClass = typeof(CompDreamumTower);
        }
    }

    public class CompDreamumTower : ThingComp
    {
        public CompProperties_DreamumTower Props => (CompProperties_DreamumTower)props;

        public bool BlockDiplomacy => _bigThreatActivateNotified;

        public float ProgressPct => _progress / Props.completeProgressTicks;
        private float _progress;
        private int _previousGraphicIndex;

        private bool _bigThreatActivateNotified;
        private bool _hazeActivateNotified;
        private Dictionary<Map, GameCondition> _causedConditions = new Dictionary<Map, GameCondition>();

        public float HazeWorldRadius
        {
            get
            {
                return Mathf.Lerp(0, Props.hazeWorldRadiusMax, (ProgressPct - Props.hazeActivatePct) / (1f - Props.hazeActivatePct));
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _progress, "dreamumProgress");
            Scribe_Values.Look(ref _previousGraphicIndex, "previousGraphicIndex");

            Scribe_Values.Look(ref _bigThreatActivateNotified, "bigThreatActivateNotified");
            Scribe_Values.Look(ref _hazeActivateNotified, "hazeActivateNotified");

            Scribe_Collections.Look(ref _causedConditions, "causedConditions", LookMode.Reference, LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                _causedConditions.RemoveAll((KeyValuePair<Map, GameCondition> x) => x.Value == null);
                foreach (var causedCondition in _causedConditions)
                {
                    causedCondition.Value.conditionCauser = parent;
                }
            }
        }

        private static List<Settlement> tmpSettlements = new List<Settlement>();
        private static List<Map> tmpDeadConditionMaps = new List<Map>();
        public override void CompTick()
        {
            var altar = parent as Building_DreamumAltar;
            var manaCharged = altar.ManaChargeRatio;

            if (altar.Stage == DreamumProjectStage.InProgress && manaCharged >= 0.5f)
            {
                _progress += manaCharged;
            }

            if (_previousGraphicIndex != altar.GraphicIndex)
            {
                _previousGraphicIndex = altar.GraphicIndex;
                if (Props.growthEffect != null)
                {
                    Props.growthEffect.SpawnAttached(parent, parent.Map);
                }
            }

            if ((int)_progress >= Props.completeProgressTicks)
            {
                _progress = Props.completeProgressTicks;
                altar.CompleteProgress();
            }

            if (!_bigThreatActivateNotified && ProgressPct >= Props.bigThreatActivatePct.TrueMin)
            {
                _bigThreatActivateNotified = true;

                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_DreamumAltarThreatActivatedLabel.Translate(),
                    LocalizeString_Letter.VV_Letter_DreamumAltarThreatActivated.Translate(),
                    LetterDefOf.ThreatBig,
                    parent);

                MakeViviFactionAlly();
                MakeNonViviFactionEnemy();
            }

            if (ProgressPct >= Props.hazeActivatePct)
            {
                if (!_hazeActivateNotified)
                {
                    _hazeActivateNotified = true;

                    Find.LetterStack.ReceiveLetter(
                        LocalizeString_Letter.VV_Letter_DreamumHazeActivatedLabel.Translate(),
                        LocalizeString_Letter.VV_Letter_DreamumHazeActivated.Translate(),
                        LetterDefOf.NeutralEvent,
                        parent);
                }

                if (parent.IsHashIntervalTick(GenTicks.TickLongInterval))
                {
                    foreach (var map in Find.Maps)
                    {
                        if (InAoE(map.Tile))
                        {
                            EnforceConditionOn(map);
                        }
                    }

                    tmpDeadConditionMaps.Clear();
                    foreach (var kv in _causedConditions)
                    {
                        if (kv.Value.Expired || !kv.Key.GameConditionManager.ConditionIsActive(kv.Value.def))
                        {
                            tmpDeadConditionMaps.Add(kv.Key);
                        }
                    }
                    foreach (var map in tmpDeadConditionMaps)
                    {
                        _causedConditions.Remove(map);
                    }
                }

                if (parent.IsHashIntervalTick(15000))
                {
                    tmpSettlements.Clear();
                    tmpSettlements.AddRange(Find.WorldObjects.Settlements.Where(v => !v.HasMap && !(v.Faction.def.allowedCultures?.Contains(VVCultureDefOf.VV_ViviCulture) ?? false)));

                    var tile = parent.Tile;
                    foreach (var settlement in tmpSettlements)
                    {
                        if (Find.WorldGrid.ApproxDistanceInTiles(tile, settlement.Tile) <= HazeWorldRadius)
                        {
                            var destroyedSettlement = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                            destroyedSettlement.Tile = settlement.Tile;
                            destroyedSettlement.SetFaction(settlement.Faction);
                            Find.WorldObjects.Add(destroyedSettlement);

                            if (!HasAnyOtherBase(settlement))
                            {
                                settlement.Faction.defeated = true;
                            }

                            settlement.Destroy();
                        }
                    }
                }
            }
        }

        public override void PostDraw()
        {
            if (!(parent is Building_DreamumAltar altar) || altar.Stage < DreamumProjectStage.Prepare || altar.RequireDreamum)
            {
                return;
            }

            var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
            var drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            var mat = Props.graphicData.Graphic.MatAt(parent.Rotation, parent);
            Graphics.DrawMesh(
                mesh,
                drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation),
                Quaternion.identity,
                mat,
                layer: 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                Command_Action command_addprogress1day = new Command_Action();
                command_addprogress1day.defaultLabel = "DEV: add progress 1 day";
                command_addprogress1day.action = () =>
                {
                    _progress += 60000;
                };

                yield return command_addprogress1day;
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Messages.Message(
                LocalizeString_Message.VV_Message_DreamumAltarDestroyed.Translate(parent.def.LabelCap),
                new TargetInfo(parent.Position, previousMap),
                MessageTypeDefOf.NegativeEvent);
        }

        private bool InAoE(int tile)
        {
            return Find.WorldGrid.ApproxDistanceInTiles(tile, parent.Tile) < HazeWorldRadius;
        }

        private GameCondition EnforceConditionOn(Map map)
        {
            var gameCondition = GetConditionInstance(map);
            if (gameCondition == null)
            {
                gameCondition = GameConditionMaker.MakeCondition(Props.hazeConditionDef);
                gameCondition.Duration = gameCondition.TransitionTicks;
                gameCondition.conditionCauser = parent;
                gameCondition.suppressEndMessage = true;
                map.gameConditionManager.RegisterCondition(gameCondition);
                _causedConditions.Add(map, gameCondition);
            }
            else
            {
                gameCondition.TicksLeft = gameCondition.TransitionTicks;
            }

            return gameCondition;
        }

        private GameCondition GetConditionInstance(Map map)
        {
            if (!_causedConditions.TryGetValue(map, out var value))
            {
                value = map.GameConditionManager.GetActiveCondition(Props.hazeConditionDef);
                if (value != null)
                {
                    _causedConditions.Add(map, value);
                    value.suppressEndMessage = true;
                }
            }
            return value;
        }

        private bool HasAnyOtherBase(Settlement defeatedFactionBase)
        {
            var settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (settlement.Faction == defeatedFactionBase.Faction && settlement != defeatedFactionBase)
                {
                    return true;
                }
            }

            return false;
        }

        private void MakeViviFactionAlly()
        {
            foreach (var faction in Find.FactionManager.AllFactionsListForReading.Where(v => !v.IsPlayer && v.def.allowedCultures != null && v.def.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture)))
            {
                Find.FactionManager.OfPlayer.TryAffectGoodwillWith(faction, 100 - faction.GoodwillWith(Find.FactionManager.OfPlayer));
            }
        }

        private void MakeNonViviFactionEnemy()
        {
            foreach (var faction in Find.FactionManager.AllFactionsListForReading.Where(v => !v.IsPlayer && !v.Hidden && !v.temporary && !v.def.permanentEnemy && !v.defeated))
            {
                if (faction.def.allowedCultures != null && faction.def.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture)) { continue; }

                Find.FactionManager.OfPlayer.TryAffectGoodwillWith(faction, Find.FactionManager.OfPlayer.GoodwillToMakeHostile(faction));
            }
        }
    }
}
