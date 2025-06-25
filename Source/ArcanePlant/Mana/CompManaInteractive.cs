using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaInteractive : CompProperties
    {
        public float manaGenenerate;
        public float manaConsume;

        public float manaStorage;
        public float manaAbsorbRate;

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
            _manaGeneratesByDay = Props.manaGenenerate;
            _manaConsumesByDay = Props.manaConsume;

            var netManaChange = (_manaGeneratesByDay - _manaConsumesByDay) / 60000f * ticks;
            if (netManaChange > 0)
            {
                // 생산량이 소모량보다 많은 경우 1. 비축 2. 방출
                var storable = Mathf.Max(0f, Props.manaStorage - _manaStored);
                if (netManaChange < storable)
                {
                    _manaStored += netManaChange;
                    netManaChange = 0f;
                }
                else
                {
                    netManaChange -= storable;
                    _manaStored = Props.manaStorage;

                    var cellRect = parent.OccupiedRect();
                    if (cellRect.Size != IntVec2.One)
                    {
                        foreach (var cell in cellRect)
                        {
                            manaGrid.AddMana(cell, netManaChange / cellRect.Count());
                        }
                    }
                    else
                    {
                        manaGrid.AddMana(parent.Position, netManaChange);
                    }

                    netManaChange = 0f;
                }
            }
            else
            {
                var totalAbsorbableMana = parent.OccupiedRect().Cells.Sum(cell => manaGrid[cell]) * Props.manaAbsorbRate;
                var cellRect = parent.OccupiedRect();

                if (totalAbsorbableMana > -netManaChange)
                {
                    foreach (var cell in parent.OccupiedRect())
                    {
                        manaGrid.AddMana(cell, manaGrid[cell] * Props.manaAbsorbRate / totalAbsorbableMana * netManaChange);
                    }

                    netManaChange = 0f;
                }
                else
                {
                    foreach (var cell in parent.OccupiedRect())
                    {
                        manaGrid.AddMana(cell, manaGrid[cell] * -Props.manaAbsorbRate);
                    }

                    netManaChange += totalAbsorbableMana;
                }

                if (-netManaChange < _manaStored)
                {
                    _manaStored += netManaChange;
                    netManaChange = 0f;
                }
                else if (-netManaChange > _manaStored)
                {
                    netManaChange += _manaStored;
                    _manaStored = 0f;
                }
            }

            if (netManaChange > 0)
            {
            }
            else
            {
            }


            _manaActivated = netManaChange >= 0f;
        }
    }
}
