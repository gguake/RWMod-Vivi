using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public interface IManaChangeEventReceiver
    {
        void Notify_ManaActivateChanged(bool before, bool current);
    }

    public class CompProperties_Mana : CompProperties
    {
        public float manaCapacity;

        public ManaFluxRule manaGenerateRule;
        public ManaFluxRule manaConsumeRule;
        public float manaAbsorbPerDay;

        public bool workOnlySpawned = true;

        public CompProperties_Mana()
        {
            compClass = typeof(CompMana);
        }
    }

    public class CompMana : ThingComp, IThingGlower
    {
        public CompProperties_Mana Props => (CompProperties_Mana)props;

        public int ManaExternalChangeByDay => (int)(_manaExternalChange * 60000f / ManaMapComponent.RefreshManaInterval);

        private float _manaGeneratesByDay;
        private float _manaConsumesByDay;
        private float _manaStored;
        private float _manaExternalChange;

        private bool _manaActivated = true;

        public bool Active => _manaActivated;

        public float Stored
        {
            get
            {
                return _manaStored;
            }
            set
            {
                _manaStored = Mathf.Clamp(value, 0f, Props.manaCapacity);
            }
        }
        public float StoredPct
        {
            get
            {
                return _manaStored / Props.manaCapacity;
            }
            set
            {
                Stored = Props.manaCapacity * value;
            }
        }
            
        public override void PostPostMake()
        {
            _manaStored = Props.manaCapacity;

            Current.Game.GetComponent<GameComponent_Mana>().RegisterCompMana(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Current.Game.GetComponent<GameComponent_Mana>().UnregisterCompMana(this);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _manaGeneratesByDay, "manaGeneratesByDay");
            Scribe_Values.Look(ref _manaConsumesByDay, "manaConsumesByDay");
            Scribe_Values.Look(ref _manaStored, "manaStored");
            Scribe_Values.Look(ref _manaExternalChange, "manaExternalChange");

            Scribe_Values.Look(ref _manaActivated, "manaActivated");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_manaStored > Props.manaCapacity)
                {
                    _manaStored = Props.manaCapacity;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();
            if (parent.Spawned && (Props.manaGenerateRule != null || Props.manaConsumeRule != null))
            {
                var netChange = _manaGeneratesByDay - _manaConsumesByDay;
                var activeStr = Active ? 
                    LocalizeString_Inspector.VV_Inspector_PlantActive.Translate() : 
                    LocalizeString_Inspector.VV_Inspector_PlantInactive.Translate();

                sb.Append(LocalizeString_Inspector.VV_Inspector_PlantManaFlux.Translate(
                    activeStr,
                    netChange.ToString("+0;-#")));
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction == null || !parent.Faction.HostileTo(Faction.OfPlayer))
            {
                if (Props.manaCapacity > 0)
                {
                    yield return new ManaGizmo(this);
                }
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_addMana = new Command_Action();
                command_addMana.defaultLabel = "DEV: Stored Mana +10%";
                command_addMana.action = () =>
                {
                    StoredPct += 0.1f;
                };

                yield return command_addMana;

                Command_Action command_subtractMana = new Command_Action();
                command_subtractMana.defaultLabel = "DEV: Stored Mana -10%";
                command_subtractMana.action = () =>
                {
                    StoredPct -= 0.1f;
                };

                yield return command_subtractMana;

                Command_Action command_setEnvMana = new Command_Action();
                command_setEnvMana.defaultLabel = "DEV: Set environment mana full";
                command_setEnvMana.action = () =>
                {
                    var manaGrid = parent.MapHeld.GetManaComponent();
                    if (manaGrid != null)
                    {
                        manaGrid.ChangeEnvironmentMana(parent.PositionHeld, 10000f);
                    }
                };

                yield return command_setEnvMana;
            }
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            if (parent.Faction == null || !parent.Faction.HostileTo(Faction.OfPlayer))
            {
                if (Props.manaCapacity > 0)
                {
                    yield return new ManaGizmo(this);
                }
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (Props.manaCapacity > 0f)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaStorage.Translate(),
                    Props.manaCapacity.ToString(),
                    LocalizeString_Stat.VV_StatsReport_ManaStorage_Desc.Translate(),
                    -22200);
            }

            if (Props.manaGenerateRule != null)
            {
                var desc = new StringBuilder();
                desc.Append(LocalizeString_Stat.VV_StatsReport_ManaGenerate_Desc.Translate());
                desc.AppendLine();
                desc.AppendInNewLine(Props.manaGenerateRule.GetRuleString());

                var valueStr = Props.manaGenerateRule.FluxRangeForDisplay.TrueMax.ToString();
                if (Props.manaGenerateRule is ManaFluxRule_Random) { valueStr = "???"; }

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaGenerate.Translate(),
                    valueStr,
                    desc.ToString(),
                    -22201);
            }

            if (Props.manaConsumeRule != null)
            {
                var desc = new StringBuilder();
                desc.Append(LocalizeString_Stat.VV_StatsReport_ManaConsume_Desc.Translate());
                desc.AppendLine();
                desc.AppendInNewLine(Props.manaConsumeRule.GetRuleString());

                var valueStr = Props.manaConsumeRule.FluxRangeForDisplay.TrueMax.ToString();
                if (Props.manaConsumeRule is ManaFluxRule_Random) { valueStr = "???"; }

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaConsume.Translate(),
                    valueStr,
                    desc.ToString(),
                    -22202);
            }

            if (Props.manaAbsorbPerDay > 0f)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaAbsorb.Translate(),
                    Props.manaAbsorbPerDay.ToString(),
                    LocalizeString_Stat.VV_StatsReport_ManaAbsorb_Desc.Translate(),
                    -22203);
            }

        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (parent.Spawned && parent is Building)
            {
                parent.Map.GetManaComponent()?.MarkForDrawOverlay();
            }
        }

        public void RefreshMana(ManaMapComponent manaGrid, IntVec3 pos, int ticks)
        {
            if (Props.workOnlySpawned && !parent.Spawned) { return; }

            _manaGeneratesByDay = Props.manaGenerateRule?.CalcManaFlux(parent) * parent.HitPoints / (float)parent.MaxHitPoints ?? 0f;
            _manaConsumesByDay = Props.manaConsumeRule?.CalcManaFlux(parent) ?? 0f;

            var absorbableMana = Mathf.Clamp(manaGrid[pos], 0f, Props.manaAbsorbPerDay / 60000f * ticks);
            var generatedMana = _manaGeneratesByDay / 60000f * ticks;
            var consumedMana = _manaConsumesByDay / 60000f * ticks;

            float manaChange = absorbableMana + generatedMana - consumedMana;

            var beforeExternalMana = manaGrid[pos];
            if (manaChange > 0f)
            {
                if (_manaStored < Props.manaCapacity)
                {
                    var deposited = Mathf.Clamp(manaChange, 0f, Props.manaCapacity - _manaStored);
                    manaChange -= deposited;
                    _manaStored += deposited;
                }

                manaGrid.ChangeEnvironmentMana(pos, -absorbableMana + manaChange);
            }
            else
            {
                if (absorbableMana > 0f)
                {
                    var absorbed = Mathf.Clamp(-manaChange, 0f, absorbableMana);
                    manaChange += absorbed;

                    manaGrid.ChangeEnvironmentMana(pos, -absorbed);
                }

                if (_manaStored > 0f)
                {
                    var withdrawn = Mathf.Clamp(-manaChange, 0f, _manaStored);
                    manaChange += withdrawn;
                    _manaStored -= withdrawn;
                }
            }

            var beforeActivated = _manaActivated;
            _manaActivated = manaChange >= 0f;
            _manaExternalChange = manaGrid[pos] - beforeExternalMana;

            if (beforeActivated != _manaActivated)
            {
                if (parent is IManaChangeEventReceiver manaChangeEventReceiver)
                {
                    manaChangeEventReceiver.Notify_ManaActivateChanged(beforeActivated, _manaActivated);
                }

                foreach (var comp in parent.AllComps)
                {
                    if (comp is IManaChangeEventReceiver manaChangeEventReceiverComp)
                    {
                        manaChangeEventReceiverComp.Notify_ManaActivateChanged(beforeActivated, _manaActivated);
                    }
                }
            }
        }

        public bool ShouldBeLitNow() => Active;
    }
}
