using RimWorld;
using UnityEngine;

namespace VVRace
{
    public class ITab_ArcanePlantFarmBill : ITab
    {
        public Building_ArcanePlantFarm SelFarm => SelThing as Building_ArcanePlantFarm;

        public override bool IsVisible => SelFarm.Bill != null && SelFarm.Bill.Stage >= GrowingArcanePlantBillStage.Growing;

        public ITab_ArcanePlantFarmBill()
        {
            size = new Vector2(400f, 300f);
            labelKey = LocalizeTexts.ITab_ArcanePlantFarmBill_TabLabel;
        }

        protected override void FillTab()
        {
        }
    }
}
