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

        public ManaFluxRule manaGenenerateRule;
        public ManaFluxRule manaConsumeRule;
        public float manaAbsorbPerDay;

        public CompProperties_Mana()
        {
            compClass = typeof(CompMana);
        }
    }

    public class CompMana : ThingComp
    {
        public CompProperties_Mana Props => (CompProperties_Mana)props;

        private float _manaGeneratesByDay;
        private float _manaConsumesByDay;
        private float _manaStored;
        private float _manaExternalChange;

        private bool _manaActivated;

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
        public float StoredPct => _manaStored / Props.manaCapacity;

        public override void PostPostMake()
        {
            _manaStored = 0;
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
            if (Active)
            {
                sb.Append(LocalizeString_Inspector.VV_Inspector_PlantActive);
            }
            else
            {
                sb.Append(LocalizeString_Inspector.VV_Inspector_PlantInactive);
            }

            sb.Append(", ");
            sb.Append(LocalizeString_Inspector.VV_Inspector_PlantMana.Translate((int)_manaStored, Props.manaCapacity));
            if (parent.Spawned)
            {
                var netChange = _manaGeneratesByDay - _manaConsumesByDay;
                sb.Append(" ");
                sb.Append(LocalizeString_Inspector.VV_Inspector_PlantManaFlux.Translate(netChange.ToString("+0;-#")));

                if (DebugSettings.godMode)
                {
                    sb.AppendLine();
                    sb.Append($"external mana flux: {_manaExternalChange}");
                }
            }

            return sb.ToString();
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

            if (Props.manaGenenerateRule != null)
            {

            }

            if (Props.manaConsumeRule != null)
            {

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

        public void RefreshMana(ManaGrid manaGrid, int ticks)
        {
            _manaGeneratesByDay = Props.manaGenenerateRule?.CalcManaFlux(parent) * parent.HitPoints / (float)parent.MaxHitPoints ?? 0f;
            _manaConsumesByDay = Props.manaConsumeRule?.CalcManaFlux(parent) ?? 0f;

            var absorbableMana = Mathf.Clamp(manaGrid[parent.Position], 0f, Props.manaAbsorbPerDay / 60000f * ticks);
            var generatedMana = _manaGeneratesByDay / 60000f * ticks;
            var consumedMana = _manaConsumesByDay / 60000f * ticks;

            float manaChange;
            if (consumedMana <= absorbableMana + generatedMana + _manaStored)
            {
                _manaActivated = true;
                manaChange = absorbableMana + generatedMana - consumedMana;
            }
            else
            {
                _manaActivated = false;
                manaChange = absorbableMana + generatedMana;
            }

            var beforeExternalMana = manaGrid[parent.Position];
            if (manaChange > 0f)
            {
                if (_manaStored < Props.manaCapacity)
                {
                    var deposited = Mathf.Clamp(manaChange, 0f, Props.manaCapacity - _manaStored);
                    manaChange -= deposited;
                    _manaStored += deposited;
                }

                manaGrid.AddMana(parent.Position, -absorbableMana + manaChange);
            }
            else
            {
                if (absorbableMana > 0f)
                {
                    var absorbed = Mathf.Clamp(-manaChange, 0f, absorbableMana);
                    manaChange += absorbed;

                    manaGrid.AddMana(parent.Position, -absorbed);
                }

                if (_manaStored > 0f)
                {
                    var withdrawn = Mathf.Clamp(-manaChange, 0f, _manaStored);
                    manaChange += withdrawn;
                    _manaStored -= withdrawn;
                }
            }

            _manaExternalChange = manaGrid[parent.Position] - beforeExternalMana;
        }
    }
}
