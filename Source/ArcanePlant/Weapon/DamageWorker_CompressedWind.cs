using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class DamageWorker_CompressedWind : DamageWorker
    {
        private static readonly List<Projectile> _tmpProjectiles = new List<Projectile>();

        public override void ExplosionStart(Explosion explosion, List<IntVec3> cellsToAffect)
        {
            base.ExplosionStart(explosion, cellsToAffect);
            BlowAwayProjectiles(explosion, cellsToAffect);

            // 아직 지우지 않은 칸의 가스가 다시 유입되지 않도록 폭발 시작 시 모든 칸의 가스를 한꺼번에 지운다.
            for (int i = 0; i < cellsToAffect.Count; i++)
            {
                ClearGas(explosion.Map, cellsToAffect[i]);
            }
        }

        // 투사체·가스만 날려 보내고 칸 안의 대상에는 아무 영향도 주지 않는다(공격 판정도 없음).
        protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
        {
        }

        private static void BlowAwayProjectiles(Explosion explosion, List<IntVec3> cells)
        {
            var map = explosion.Map;
            for (int i = 0; i < cells.Count; i++)
            {
                var things = map.thingGrid.ThingsListAtFast(cells[i]);
                for (int j = 0; j < things.Count; j++)
                {
                    if (things[j] is Projectile projectile && CanBlowAway(projectile))
                    {
                        _tmpProjectiles.Add(projectile);
                    }
                }
            }

            var center = explosion.Position.ToVector3Shifted();
            for (int i = 0; i < _tmpProjectiles.Count; i++)
            {
                var projectile = _tmpProjectiles[i];
                if (projectile.Destroyed) { continue; }

                ThrowGust(map, center, projectile.ExactPosition);
                projectile.Destroy();
            }

            _tmpProjectiles.Clear();
        }

        // 초정신 바늘은 돌아올 때 사용자의 사격 대기 자세를 직접 해제해야 하므로 외부에서 파괴하지 않는다.
        private static bool CanBlowAway(Projectile projectile)
        {
            return !projectile.Destroyed
                && projectile.def.destroyable
                && !projectile.def.projectile.flyOverhead
                && !(projectile is Needle);
        }

        private static void ThrowGust(Map map, Vector3 center, Vector3 position)
        {
            if (!position.ShouldSpawnMotesAt(map)) { return; }

            var away = (position - center).Yto0();
            var data = FleckMaker.GetDataStatic(position, map, VVFleckDefOf.VV_Fleck_CompressedWindGust, Rand.Range(0.5f, 0.7f));
            data.velocityAngle = away.sqrMagnitude > 0.01f ? away.AngleFlat() : Rand.Range(0f, 360f);
            data.velocitySpeed = Rand.Range(4f, 5f);
            data.rotation = data.velocityAngle;
            map.flecks.CreateFleck(data);
        }

        private static void ClearGas(Map map, IntVec3 cell)
        {
            if (!map.gasGrid.AnyGasAt(cell)) { return; }

            map.gasGrid.ClearCellUnsafe(cell);
            map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
        }
    }
}
