using RimWorld;
using Verse;

namespace VVRace
{
    public class CompUseEffect_RuinPuzzleFlower : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            var ruinsComp = parent?.Map?.GetComponent<MapComponent_ViviAncientRuins>();
            if (ruinsComp == null || !ruinsComp.IsActiveRuinsMap) { return false; }

            if (ruinsComp.IsHexagonSolved)
            {
                return LocalizeString_Etc.VV_FailReason_RuinPuzzleSolved.Translate();
            }

            if (p == null || p.Dead || !p.IsVivi())
            {
                return LocalizeString_Etc.VV_FailReason_FairyFlowerNotVivi.Translate();
            }

            return true;
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            if (parent is Building_RuinPuzzleFairyFlower flower)
            {
                flower.Map?.GetComponent<MapComponent_ViviAncientRuins>()?.InteractPuzzleFlower(usedBy, flower);
            }
        }
    }
}
