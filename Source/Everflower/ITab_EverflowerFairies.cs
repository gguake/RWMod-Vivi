using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ITab_EverflowerFairies : ITab_ContentsBase
    {
        private List<Thing> _tmpList = new List<Thing>();
        public override IList<Thing> container
        {
            get
            {
                _tmpList.Clear();
                var everflower = SelThing as ArcanePlant_Everflower;
                if (everflower != null && everflower.EverflowerComp.InnerFairyPawns.Any())
                {
                    _tmpList.AddRange(everflower.EverflowerComp.InnerFairyPawns);
                }

                return _tmpList;
            }
        }

        public ITab_EverflowerFairies()
        {
            labelKey = LocalizeString_ITab.VV_ITab_EverflowerFairies_Label.Translate();
            containedItemsKey = LocalizeString_ITab.VV_ITab_EverflowerFairies_ItemKey.Translate();
            canRemoveThings = false;
        }

        public override bool IsVisible
        {
            get
            {
                if (!base.IsVisible) { return false; }

                var everflower = SelThing as ArcanePlant_Everflower;
                return everflower != null && everflower.EverflowerComp.InnerFairyPawns.Any();
            }
        }
    }
}
