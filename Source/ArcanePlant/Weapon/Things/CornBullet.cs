using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CornBulletData : DefModExtension
    {
        public ThingDef scatterBulletDef;
        public ThingDef alternateScatterBulletDef;
        public float alternateScatterBulletChance;
        public float scatterRadius;
        public IntRange scatterCount;
        public bool includeExplosionCells;
    }

    public class CornBullet : Projectile_Explosive
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var parentLauncher = launcher;
            var parentUsedTarget = usedTarget;
            var parentIntendedTarget = intendedTarget;

            var map = Map;
            var impactCell = Position;
            var startDrawPos = DrawPos;

            base.Impact(hitThing, blockedByShield);

            var data = def.GetModExtension<CornBulletData>();
            if (data == null) { return; }
            if (data.scatterBulletDef == null) { return; }

            var cellCandidates = GenRadial.RadialPatternInRadius(data.scatterRadius)
                .Select(cell => impactCell + cell)
                .Where(cell => cell.InBounds(map) && (data.includeExplosionCells || cell.DistanceToSquared(impactCell) > def.projectile.explosionRadius * def.projectile.explosionRadius) && GenSight.LineOfSight(impactCell, cell, map, skipFirstCell: true))
                .ToList();

            if (cellCandidates.Count == 0) { return; }

            var scatterCount = Mathf.FloorToInt(Mathf.Clamp(data.scatterCount.RandomInRange * cellCandidates.Count / (Mathf.PI * (data.scatterRadius * data.scatterRadius - (data.includeExplosionCells ? (def.projectile.explosionRadius * def.projectile.explosionRadius) : 0f))), 0f, cellCandidates.Count));
            var selected = cellCandidates.TakeRandomDistinct(scatterCount);
            if (selected != null && selected.Count > 0)
            {
                foreach (var cell in selected)
                {
                    var flag = ProjectileHitFlags.All;
                    var scatterBulletDef = data.scatterBulletDef;
                    if (data.alternateScatterBulletDef != null && data.alternateScatterBulletChance > 0f && Rand.Chance(data.alternateScatterBulletChance))
                    {
                        scatterBulletDef = data.alternateScatterBulletDef;
                    }

                    var projectile = (Projectile)GenSpawn.Spawn(scatterBulletDef, impactCell, map);
                    projectile.Launch(parentLauncher, startDrawPos, cell, cell, flag);
                }
            }
        }
    }
}
