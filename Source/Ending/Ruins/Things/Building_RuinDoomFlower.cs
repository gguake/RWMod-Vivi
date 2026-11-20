using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class Building_RuinDoomFlower : Building
    {
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            var prevMap = Map;
            var pos = Position;
            base.Destroy(mode);

            if (mode == DestroyMode.KillFinalize && prevMap != null)
            {
                var ruinsComp = prevMap.GetComponent<MapComponent_ViviAncientRuins>();
                if (ruinsComp != null && ruinsComp.IsActiveRuinsMap && !ruinsComp.IsTrialCleared)
                {
                    ruinsComp.Notify_RuinDoomFlowerDestroyed(pos);
                }
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(LocalizeString_Inspector.VV_Inspector_RuinDoomFlowerHint.Translate());
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode && Spawned)
            {
                var ruinsComp = Map?.GetComponent<MapComponent_ViviAncientRuins>();
                if (ruinsComp != null && ruinsComp.IsActiveRuinsMap && !ruinsComp.IsTrialCleared)
                {
                    yield return EndingVfxUtility.DevAction("DEV: Show path guide", () => ruinsComp.SpawnPathGuide());
                }
            }
        }
    }
}
