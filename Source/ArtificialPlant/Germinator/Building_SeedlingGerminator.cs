using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum GerminateScheduleJob
    {
        None,
        Repotting,
        PestControl,
        Weeding,
    }

    [StaticConstructorOnStartup]
    public class Building_SeedlingGerminator : Building
    {
        public GerminateSchedule CurrentSchedule => _currentSchedule;

        private GerminateSchedule _currentSchedule;
        private GerminateSchedule _lastCompletedSchedule;

        private GerminatorModExtension _defModExtension;
        public GerminatorModExtension GerminatorModExtension
        {
            get
            {
                if (_defModExtension == null)
                {
                    _defModExtension = def.GetModExtension<GerminatorModExtension>();
                }

                return _defModExtension;
            }
        }

        private Dictionary<ThingDef, int> _germinateReservedThings = null;
        private Dictionary<ThingDef, int> _germinateIngredients = null;
        public IReadOnlyDictionary<ThingDef, int> GerminateIngredients
        {
            get
            {
                if (_germinateIngredients == null)
                {
                    _germinateIngredients = new Dictionary<ThingDef, int>();
                    foreach (var tdc in GerminatorModExtension.germinateIngredients)
                    {
                        _germinateIngredients.Add(tdc.thingDef, tdc.count);
                    }
                }

                return _germinateIngredients;
            }
        }

        public IEnumerable<(ThingDef def, int count)> RequiredGerminateIngredients
        {
            get
            {
                foreach (var ingredient in GerminateIngredients)
                {
                    _germinateReservedThings.TryGetValue(ingredient.Key, out var reserved);

                    if (reserved < ingredient.Value)
                    {
                        yield return (ingredient.Key, ingredient.Value - reserved);
                    }
                }
            }
        }

        public int GetGerminateRequiredCount(ThingDef def)
        {
            if (GerminateIngredients.TryGetValue(def, out var ingredientCount))
            {
                if (_germinateReservedThings != null && _germinateReservedThings.TryGetValue(def, out var reserved))
                {
                    if (ingredientCount - reserved < 0) { Log.Warning($"invalid germinate ingredients: {ingredientCount} {reserved}"); }
                    return Mathf.Max(ingredientCount - reserved, 0);
                }

                return ingredientCount;
            }

            return 0;
        }

        private static readonly Texture2D GerminateCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_Germinate");

        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _currentSchedule, "currentSchedule");
            Scribe_Deep.Look(ref _lastCompletedSchedule, "lastCompletedSchedule");
            Scribe_Collections.Look(ref _germinateReservedThings, "germinateReservedThings", LookMode.Def, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _currentSchedule?.ResolveBuildingDef(def);
                _lastCompletedSchedule?.ResolveBuildingDef(def);
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (CurrentSchedule != null)
            {
                var schedule = CurrentSchedule;
                switch (schedule.Stage)
                {
                    case GerminateStage.None:
                        {
                            if (sb.Length > 0) { sb.AppendLine(); }
                            sb.Append(LocalizeTexts.InspectorViviGerminatorReserved.Translate());

                            if (GerminatorModExtension.germinateIngredients?.Count > 0)
                            {
                                foreach (var tdc in GerminatorModExtension.germinateIngredients)
                                {
                                    if (sb.Length > 0) { sb.AppendLine(); }
                                    sb.Append($"{tdc.thingDef.LabelCap}: {(_germinateReservedThings.TryGetValue(tdc.thingDef, out var count) ? count : 0)}/{tdc.count}");
                                }
                            }
                        }
                        break;

                    case GerminateStage.GerminateInProgress:
                        {
                            if (sb.Length > 0) { sb.AppendLine(); }
                            sb.Append(LocalizeTexts.InspectorViviGerminatorCompletePeriods.Translate(schedule.TicksToCompleteGerminate.ToStringTicksToPeriod(allowSeconds: false)));

                            if (schedule.CanManageJob)
                            {
                                var currentScheduleDef = schedule.CurrentManageScheduleDef;

                                if (sb.Length > 0) { sb.AppendLine(); }
                                sb.Append(LocalizeTexts.InspectorViviGerminatorManageAvailable.Translate(schedule.CurrentScheduleNumber, currentScheduleDef.LabelCap));

                                if (currentScheduleDef.ingredients?.Count > 0)
                                {
                                    var sbIngredients = new StringBuilder();
                                    foreach (var tdc in currentScheduleDef.ingredients)
                                    {
                                        if (sbIngredients.Length > 0) { sbIngredients.Append(", "); }
                                        sbIngredients.Append($"{tdc.thingDef.LabelCap} x{tdc.count}");
                                    }

                                    if (sb.Length > 0) { sb.AppendLine(); }
                                    sb.Append(LocalizeTexts.InspectorViviGerminatorManageIngredient.Translate(sbIngredients.ToString()));
                                }
                            }
                            else if (schedule.HasNextManageJob)
                            {
                                if (sb.Length > 0) { sb.AppendLine(); }
                                sb.Append(LocalizeTexts.InspectorViviGerminatorManageLastPeriods.Translate(
                                    schedule.TicksToNextManageJob.ToStringTicksToPeriod()));
                            }
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned)
            {
                if (_currentSchedule == null && Find.Selector.SelectedObjectsListForReading
                    .Where(v => v is Building_SeedlingGerminator)
                    .Cast<Building_SeedlingGerminator>()
                    .GroupBy(v => v.def)
                    .Count() == 1)
                {
                    var commandRegisterSchedule = new Command_RegisterGerminateSchedule();
                    commandRegisterSchedule.building = this;
                    commandRegisterSchedule.icon = GerminateCommandTex;
                    commandRegisterSchedule.iconOffset = new Vector2(0f, -0.08f);
                    commandRegisterSchedule.defaultLabel = LocalizeTexts.CommandRegisterGerminateSchedule.Translate();
                    commandRegisterSchedule.defaultDesc = LocalizeTexts.CommandRegisterGerminateScheduleDesc.Translate();
                    yield return commandRegisterSchedule;
                }
                else
                {
                    var commandCancelSchedule = new Command_CancelGerminateSchedule();
                    commandCancelSchedule.building = this;
                    commandCancelSchedule.icon = CancelCommandTex;
                    commandCancelSchedule.defaultLabel = LocalizeTexts.CommandCancelGerminateSchedule.Translate();
                    commandCancelSchedule.defaultDesc = LocalizeTexts.CommandCancelGerminateScheduleDesc.Translate();
                    yield return commandCancelSchedule;
                }

                if (DebugSettings.godMode)
                {
                    if (CurrentSchedule != null && CurrentSchedule.Stage == GerminateStage.GerminateInProgress)
                    {
                        var commandDebugReduceNextTick = new Command_Action();
                        commandDebugReduceNextTick.defaultLabel = "Reduce next manage ticks 1hours";
                        commandDebugReduceNextTick.action = () =>
                        {
                            CurrentSchedule.Debug_ReduceNextManageTick(2500);
                        };
                        yield return commandDebugReduceNextTick;

                        var commandDebugReduceCompleteTick = new Command_Action();
                        commandDebugReduceCompleteTick.defaultLabel = "Reduce complete ticks 1day";
                        commandDebugReduceCompleteTick.action = () =>
                        {
                            CurrentSchedule.Debug_ReduceCompleteTick(2500 * 24);
                        };
                        yield return commandDebugReduceCompleteTick;
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (this.IsHashIntervalTick(60))
            {
                CurrentSchedule?.Tick();
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            CurrentSchedule?.Tick();
        }

        public override void TickLong()
        {
            base.TickLong();

            CurrentSchedule?.Tick();
        }

        public void AddThings(Thing thing)
        {
            var requiredCount = GetGerminateRequiredCount(thing.def);
            if (requiredCount > 0)
            {
                int count = Mathf.Min(requiredCount, thing.stackCount);
                thing.SplitOff(count).Destroy();

                if (_germinateReservedThings.TryGetValue(thing.def, out var reservedCount))
                {
                    _germinateReservedThings[thing.def] = reservedCount + count;
                }
                else
                {
                    _germinateReservedThings.Add(thing.def, count);
                }
            }

            foreach (var kv in RequiredGerminateIngredients)
            {
                Log.Message($"{kv.def} x{kv.count}");
            }

            if (!RequiredGerminateIngredients.Any())
            {
                _germinateReservedThings = null;

                CurrentSchedule.StartGerminate();
            }
        }

        public void ReserveSchedule(GerminateSchedule schedule)
        {
            _currentSchedule = schedule;
            _germinateReservedThings = new Dictionary<ThingDef, int>();
        }

        public void CancelSchedule()
        {
            _currentSchedule = null;

            if (_germinateReservedThings != null)
            {
                foreach (var kv in _germinateReservedThings)
                {
                    var maxStackCount = kv.Key.stackLimit;
                    int stackCount = kv.Value;
                    while (stackCount > 0)
                    {
                        var count = Mathf.Min(maxStackCount, stackCount);
                        var thing = ThingMaker.MakeThing(kv.Key);
                        thing.stackCount = count;
                        stackCount -= count;

                        GenSpawn.Spawn(thing, Position, Map);
                    }
                }

                _germinateReservedThings = null;
            }
        }
    }
}
