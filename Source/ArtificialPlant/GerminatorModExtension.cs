using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class GerminatorModExtension : DefModExtension
    {
        public IReadOnlyDictionary<ThingDef, int> GerminateIngredients
        {
            get
            {
                if (_germinateIngredients == null)
                {
                    _germinateIngredients = new Dictionary<ThingDef, int>();
                    foreach (var tdc in germinateIngredients)
                    {
                        _germinateIngredients.Add(tdc.thingDef, tdc.count);
                    }
                }

                return _germinateIngredients;
            }
        }

        public IReadOnlyDictionary<ThingDef, int> FixedGerminateIngredients
        {
            get
            {
                if (_fixedGerminateIngredients == null)
                {
                    _fixedGerminateIngredients = new Dictionary<ThingDef, int>();
                    foreach (var tdc in fixedGerminateIngredients)
                    {
                        _fixedGerminateIngredients.Add(tdc.thingDef, tdc.count);
                    }
                }

                return _fixedGerminateIngredients;
            }
        }

        [Unsaved]
        private Dictionary<ThingDef, int> _germinateIngredients = null;

        [Unsaved]
        private Dictionary<ThingDef, int> _fixedGerminateIngredients = null;

        public List<ThingDefCountClass> germinateIngredients;
        public List<ThingDefCountClass> fixedGerminateIngredients;
        public int scheduleCooldown;

        public float fixedGerminateSuccessChance;
        public float germinateSuccessChance;
        public float germinateRareChance;
        public int germinateFailureCriticalWeight;
        public int germinateFailureCropsWeight;
        public List<ThingDefCountClass> germinateFailureCropsTable;

    }

}
