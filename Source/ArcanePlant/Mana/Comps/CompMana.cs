using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_Mana : CompProperties
    {
        public float manaCapacity;

        public ManaFluxRule manaGenerateRule;
        public ManaFluxRule manaConsumeRule;
        public float manaAbsorbPerDay;

        public CompProperties_Mana()
        {
            compClass = typeof(CompMana);
        }
    }

    public class CompMana : ThingComp, IThingGlower
    {
        public CompProperties_Mana Props => (CompProperties_Mana)props;

        public int ManaExternalChangeByDay => (int)(_manaExternalChange * 60000f / EnvironmentManaGrid.RefreshManaInterval);

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
            if (Props.manaCapacity > 0)
            {
                yield return new ManaGizmo(this);
            }
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            if (Props.manaCapacity > 0)
            {
                yield return new ManaGizmo(this);
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
                parent.Map.GetComponent<EnvironmentManaGrid>()?.MarkForDrawOverlay();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            parent.MapHeld.GetComponent<EnvironmentManaGrid>().RegisterCompMana(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map.GetComponent<EnvironmentManaGrid>().UnregisterCompMana(this);
        }

        public void RefreshMana(EnvironmentManaGrid manaGrid, int ticks)
        {
            _manaGeneratesByDay = Props.manaGenerateRule?.CalcManaFlux(parent) * parent.HitPoints / (float)parent.MaxHitPoints ?? 0f;
            _manaConsumesByDay = Props.manaConsumeRule?.CalcManaFlux(parent) ?? 0f;

            var absorbableMana = Mathf.Clamp(manaGrid[parent.Position], 0f, Props.manaAbsorbPerDay / 60000f * ticks);
            var generatedMana = _manaGeneratesByDay / 60000f * ticks;
            var consumedMana = _manaConsumesByDay / 60000f * ticks;

            float manaChange = absorbableMana + generatedMana - consumedMana;
            //if (consumedMana <= absorbableMana + generatedMana + _manaStored)
            //{
            //    manaChange = absorbableMana + generatedMana - consumedMana;
            //    _manaActivated = true;
            //}
            //else
            //{
            //    manaChange = absorbableMana + generatedMana;
            //    _manaActivated = false;
            //}

            var beforeExternalMana = manaGrid[parent.Position];
            if (manaChange > 0f)
            {
                if (_manaStored < Props.manaCapacity)
                {
                    var deposited = Mathf.Clamp(manaChange, 0f, Props.manaCapacity - _manaStored);
                    manaChange -= deposited;
                    _manaStored += deposited;
                }

                manaGrid.ChangeEnvironmentMana(parent.Position, -absorbableMana + manaChange, direct: true);
            }
            else
            {
                if (absorbableMana > 0f)
                {
                    var absorbed = Mathf.Clamp(-manaChange, 0f, absorbableMana);
                    manaChange += absorbed;

                    manaGrid.ChangeEnvironmentMana(parent.Position, -absorbed, direct: true);
                }

                if (_manaStored > 0f)
                {
                    var withdrawn = Mathf.Clamp(-manaChange, 0f, _manaStored);
                    manaChange += withdrawn;
                    _manaStored -= withdrawn;
                }
            }

            _manaActivated = manaChange >= 0f;
            _manaExternalChange = manaGrid[parent.Position] - beforeExternalMana;
        }

        public bool ShouldBeLitNow() => Active;
    }
}
