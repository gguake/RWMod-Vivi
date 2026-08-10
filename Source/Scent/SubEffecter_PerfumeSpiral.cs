using UnityEngine;
using Verse;

namespace VVRace
{
    public class SubEffecter_PerfumeSpiral : SubEffecter
    {
        private int nextSpawnTick;

        public SubEffecter_PerfumeSpiral(SubEffecterDef def, Effecter parent) : base(def, parent)
        {
        }

        public override void SubEffectTick(TargetInfo A, TargetInfo B)
        {
            var totalTicks = Mathf.Max(1, parent.def.maintainTicks);
            var elapsedTicks = totalTicks - parent.ticksLeft;
            if (elapsedTicks < nextSpawnTick)
            {
                return;
            }

            nextSpawnTick = elapsedTicks + Mathf.Max(1, def.ticksBetweenMotes);
            var progress = totalTicks <= 1
                ? 1f
                : Mathf.Clamp01(elapsedTicks / (totalTicks - 1f));
            SpawnSpiralFlecks(B.IsValid ? B : A, progress);
        }

        private void SpawnSpiralFlecks(TargetInfo target, float progress)
        {
            var map = target.Map;
            if (map == null || def.fleckDef == null)
            {
                return;
            }

            var angle = progress * 360f;
            var radius = Mathf.Lerp(def.positionRadiusMin, def.positionRadius, progress);
            var direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            var spiralPosition = target.CenterVector3 + direction * radius;
            var scale = Mathf.Lerp(def.scale.min, def.scale.max, progress);
            var link = def.fleckDef.useAttachLink ? new FleckAttachLink(target) : FleckAttachLink.Invalid;

            for (var i = 0; i < def.burstCount.RandomInRange; i++)
            {
                var spawnPosition = spiralPosition + Gen.RandomHorizontalVector(Mathf.Max(0.04f, scale * 0.08f));
                if (!spawnPosition.ShouldSpawnMotesAt(map, def.fleckDef.drawOffscreen))
                {
                    continue;
                }

                map.flecks.CreateFleck(new FleckCreationData
                {
                    def = def.fleckDef,
                    spawnPosition = spawnPosition,
                    scale = scale * Rand.Range(0.85f, 1.15f),
                    rotation = def.rotation.RandomInRange + angle,
                    rotationRate = def.rotationRate.RandomInRange,
                    velocityAngle = angle + def.angle.RandomInRange,
                    velocitySpeed = def.speed.RandomInRange,
                    instanceColor = EffectiveColor,
                    ageTicksOverride = -1,
                    link = link
                });
            }
        }
    }
}
