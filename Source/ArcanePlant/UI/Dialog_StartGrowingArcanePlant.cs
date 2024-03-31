using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Dialog_StartGrowingArcanePlant : Window
    {
        public override Vector2 InitialSize => new Vector2(400f, 650f);

        private Building_ArcanePlantFarm _billGiver;
        private GrowArcanePlantBill _bill;

        private Action<GrowArcanePlantBill> _callback;

        public Dialog_StartGrowingArcanePlant(
            Building_ArcanePlantFarm billGiver, 
            Action<GrowArcanePlantBill> callback)
        {
            _billGiver = billGiver;
            _bill = new GrowArcanePlantBill(billGiver);

            _callback = callback;
        }

        public override void DoWindowContents(Rect inRect)
        {
        }
    }
}
