using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_DreamumTower : CompProperties
    {
        public GraphicData graphicData;
        public int completeProgressTicks;

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

        public float ProgressPct => _progress / Props.completeProgressTicks;
        private float _progress;

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

        private static List<Map> tmpDeadConditionMaps = new List<Map>();
        public override void CompTick()
        {
            var altar = parent as Building_DreamumAltar;
            var manaCharged = altar.ManaChargeRatio;

            if (altar.Stage == DreamumProjectStage.InProgress && manaCharged >= 0.5f)
            {
                _progress += manaCharged;
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
                    LetterDefOf.NeutralEvent,
                    parent);
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
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();

            if (parent is Building_DreamumAltar altar && altar.RequireDreamum)
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

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Messages.Message(
                "MessageConditionCauserDespawned".Translate(parent.def.LabelCap), 
                new TargetInfo(parent.Position, previousMap), 
                MessageTypeDefOf.NeutralEvent);
        }
    }
}
