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
        private VVRaceModSettings _modSettingCache = LoadedModManager.GetMod<VVRaceMod>().GetSettings<VVRaceModSettings>();

        public CompProperties_Mana Props => (CompProperties_Mana)props;

        public int ManaExternalChangeByDay => (int)(_manaExternalChange * 60000f / ManaMapComponent.RefreshManaInterval);

        private float _manaGeneratesByDay;
        private float _manaConsumesByDay;
        private float _manaStored;
        private float _manaExternalChange;

        private bool _manaActivated = true;

        public bool Active => _manaActivated;

        // 〈황금빛 꽃안개〉가 걸린 맵의 마법 식물은 마나가 늘 가득 차 있고 써도 줄지 않는다.
        public bool IsUnlimited => GoldenBloomUtility.IsManaUnlimited(parent);

        public float Stored
        {
            get
            {
                return IsUnlimited ? Props.manaCapacity : _manaStored;
            }
            set
            {
                _manaStored = IsUnlimited ? Props.manaCapacity : Mathf.Clamp(value, 0f, Props.manaCapacity);
            }
        }
        public float StoredPct
        {
            get
            {
                return Stored / Props.manaCapacity;
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

            if (parent.Spawned && IsUnlimited)
            {
                sb.AppendInNewLine(LocalizeString_EndingCore.VV_Inspector_PlantManaUnlimited.Translate());
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction == null || !parent.Faction.HostileTo(Faction.OfPlayerSilentFail))
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
            if (parent.Faction == null || !parent.Faction.HostileTo(Faction.OfPlayerSilentFail))
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
            if (parent.Spawned && parent is Building && _modSettingCache.alwaysShowManaIfSelected)
            {
                parent.Map.GetManaComponent()?.MarkForDrawOverlay();
            }
        }

        public void RefreshMana(ManaMapComponent manaGrid, IntVec3 pos, int ticks)
        {
            if (Props.workOnlySpawned && !parent.Spawned) { return; }

            _manaGeneratesByDay = Props.manaGenerateRule?.CalcManaFlux(parent) * parent.HitPoints / (float)parent.MaxHitPoints ?? 0f;
            _manaConsumesByDay = Props.manaConsumeRule?.CalcManaFlux(parent) ?? 0f;

            if (IsUnlimited)
            {
                RefreshManaUnlimited(manaGrid, pos, ticks);
                return;
            }

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
                // manaChange <= 0 이면 흡수 가능량 전체가 이미 소비에 충당된 상태이므로 환경에서 전액 차감한다.
                if (absorbableMana > 0f)
                {
                    manaGrid.ChangeEnvironmentMana(pos, -absorbableMana);
                }

                if (_manaStored > 0f)
                {
                    var withdrawn = Mathf.Clamp(-manaChange, 0f, _manaStored);
                    manaChange += withdrawn;
                    _manaStored -= withdrawn;
                }
            }

            _manaExternalChange = manaGrid[pos] - beforeExternalMana;
            SetActivated(manaChange >= 0f);
        }

        // 〈황금빛 꽃안개〉: 저장 마나를 가득 채운 채 흡수·소비는 하지 않고, 생산한 마나만 그대로 환경으로 흘려보낸다.
        private void RefreshManaUnlimited(ManaMapComponent manaGrid, IntVec3 pos, int ticks)
        {
            _manaStored = Props.manaCapacity;

            var beforeExternalMana = manaGrid[pos];
            var generatedMana = _manaGeneratesByDay / 60000f * ticks;
            if (generatedMana > 0f)
            {
                manaGrid.ChangeEnvironmentMana(pos, generatedMana);
            }

            _manaExternalChange = manaGrid[pos] - beforeExternalMana;
            SetActivated(true);
        }

        private void SetActivated(bool activated)
        {
            var beforeActivated = _manaActivated;
            _manaActivated = activated;
            if (beforeActivated == _manaActivated) { return; }

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

        public bool ShouldBeLitNow() => Active;
    }
}
