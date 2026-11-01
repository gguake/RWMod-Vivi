using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityQueenManaBarrier : CompProperties_AbilityEffect
    {
        public ThingDef shieldDef;
        public float requiredTotalMana = 1000f;
        public int durationTicks = 5000;

        public CompProperties_AbilityQueenManaBarrier()
        {
            compClass = typeof(CompAbilityEffect_QueenManaBarrier);
        }
    }

    // 〈마나 보호막〉: 보호막 반경 안 환경 마나의 합계가 충분하면 그만큼을 끌어모아 저각 보호막을 세운다.
    // 소모량은 각 칸의 마나에 비례해 나눠 빼므로 마나가 많은 칸일수록 많이 줄어든다.
    public class CompAbilityEffect_QueenManaBarrier : CompAbilityEffect
    {
        public new CompProperties_AbilityQueenManaBarrier Props => (CompProperties_AbilityQueenManaBarrier)props;

        private float Radius => Props.shieldDef?.GetCompProperties<CompProperties_ProjectileInterceptor>()?.radius ?? 0f;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) { return false; }

            var map = parent.pawn?.Map;
            if (map == null || !target.Cell.InBounds(map)) { return false; }

            var mana = TotalManaAround(map, target.Cell);
            if (mana < Props.requiredTotalMana)
            {
                if (throwMessages)
                {
                    Messages.Message(
                        LocalizeString_EndingQueen.VV_Message_ManaBarrierNotEnoughMana.Translate(Mathf.FloorToInt(mana), Mathf.FloorToInt(Props.requiredTotalMana)),
                        new LookTargets(target.Cell, map),
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }

                return false;
            }

            return true;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            var map = parent.pawn?.Map;
            if (map == null || !target.Cell.InBounds(map)) { return null; }

            return LocalizeString_EndingQueen.VV_ManaBarrierTargetMana.Translate(Mathf.FloorToInt(TotalManaAround(map, target.Cell)), Mathf.FloorToInt(Props.requiredTotalMana));
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.IsValid && Radius > 0f)
            {
                GenDraw.DrawRadiusRing(target.Cell, Radius);
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var map = parent.pawn?.Map;
            if (map == null || Props.shieldDef == null || !target.Cell.InBounds(map)) { return; }

            DrainMana(map, target.Cell);

            var shield = (ViviShieldDome)ThingMaker.MakeThing(Props.shieldDef);
            shield.Init(null, Props.durationTicks);
            GenSpawn.Spawn(shield, target.Cell, map);

            QueenDefOf.VV_Effecter_QueenManaBarrier?.Spawn(target.Cell, map).Cleanup();
        }

        private void DrainMana(Map map, IntVec3 center)
        {
            var manaComponent = map.GetManaComponent();
            var total = TotalManaAround(map, center);
            if (manaComponent == null || total <= 0f) { return; }

            var ratio = Mathf.Clamp01(Props.requiredTotalMana / total);
            foreach (var cell in GenRadial.RadialCellsAround(center, Radius, true))
            {
                if (!cell.InBounds(map)) { continue; }

                var drain = manaComponent[cell] * ratio;
                if (drain > 0f)
                {
                    manaComponent.ChangeEnvironmentMana(cell, -drain);
                }
            }
        }

        private float TotalManaAround(Map map, IntVec3 center)
        {
            var manaComponent = map.GetManaComponent();
            if (manaComponent == null) { return 0f; }

            var total = 0f;
            foreach (var cell in GenRadial.RadialCellsAround(center, Radius, true))
            {
                if (cell.InBounds(map))
                {
                    total += manaComponent[cell];
                }
            }

            return total;
        }
    }
}
