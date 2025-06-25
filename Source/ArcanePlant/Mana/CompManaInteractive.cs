using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaInteractive : CompProperties
    {
        public float manaGeneneratePerDay;
        public float manaConsumePerDay;
        public float manaAbsorbPerDay;

        public float manaStorage;

        public CompProperties_ManaInteractive()
        {
            compClass = typeof(CompManaInteractive);
        }
    }

    public class CompManaInteractive : ThingComp
    {
        public CompProperties_ManaInteractive Props => (CompProperties_ManaInteractive)props;

        private float _manaGeneratesByDay;
        private float _manaConsumesByDay;
        private float _manaStored;

        private bool _manaActivated;

        public bool Active => _manaActivated;

        public float Stored => _manaStored;
        public float StoredPct => _manaStored / Props.manaStorage;

        public override void PostPostMake()
        {
            _manaStored = Props.manaStorage;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _manaGeneratesByDay, "manaGeneratesByDay");
            Scribe_Values.Look(ref _manaConsumesByDay, "manaConsumesByDay");
            Scribe_Values.Look(ref _manaStored, "manaStored");

            Scribe_Values.Look(ref _manaActivated, "manaActivated");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_manaStored > Props.manaStorage)
                {
                    _manaStored = Props.manaStorage;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();
            sb.AppendInNewLine($"Active: {Active}");
            if (Props.manaStorage > 0f)
            {
                sb.AppendInNewLine($"Stored Mana: {_manaStored}");
            }

            return sb.ToString();
        }

        public void RefreshMana(ManaGrid manaGrid, int ticks)
        {
            // CalcGeneratedMana, CalcConsumedMana
            _manaGeneratesByDay = Props.manaGeneneratePerDay;
            _manaConsumesByDay = Props.manaConsumePerDay;

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

            if (manaChange > 0f)
            {
                if (_manaStored < Props.manaStorage)
                {
                    var deposited = Mathf.Clamp(manaChange, 0f, Props.manaStorage - _manaStored);
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
        }
    }
}
