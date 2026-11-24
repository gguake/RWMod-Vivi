using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ApocryphaBarrier : CompProperties_ProjectileInterceptor
    {
        public CompProperties_ApocryphaBarrier()
        {
            compClass = typeof(CompApocryphaBarrier);
        }
    }

    [StaticConstructorOnStartup]
    public class CompApocryphaBarrier : CompProjectileInterceptor
    {
        private const string ShaderPath = "Map/VV_AsphodelBarrier";
        private const float EdgeMargin = 1.5f;
        private const float FadeInPerTick = 1f / 54f;
        private const float FadeOutPerTick = 1f / 24f;
        private const int ImpactFlashTicks = 40;

        private static readonly Material BarrierMat = LoadBarrierMaterial();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
        private static readonly int ImpactAngleId = Shader.PropertyToID("_ImpactAngle");
        private static readonly int ImpactAlphaId = Shader.PropertyToID("_ImpactAlpha");

        private static readonly AccessTools.FieldRef<CompProjectileInterceptor, int> LastInterceptTicksRef =
            AccessTools.FieldRefAccess<CompProjectileInterceptor, int>("lastInterceptTicks");
        private static readonly AccessTools.FieldRef<CompProjectileInterceptor, float> LastInterceptAngleRef =
            AccessTools.FieldRefAccess<CompProjectileInterceptor, float>("lastInterceptAngle");

        [Unsaved]
        private CompApocrypha _apocrypha;
        [Unsaved]
        private float _fade;

        private CompApocrypha Apocrypha => _apocrypha ?? (_apocrypha = parent.TryGetComp<CompApocrypha>());

        public bool ShouldBeActive => Apocrypha != null && Apocrypha.IsAsphodelActive && Apocrypha.IsQuestBossFight;

        public float BarrierFade => _fade;

        private static Material LoadBarrierMaterial()
        {
            var shader = ShaderDatabase.LoadShader(ShaderPath);
            return shader != null ? MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white) : null;
        }

        public void SyncActive(bool instantFade = false)
        {
            var active = ShouldBeActive;
            currentHitPoints = active ? -1 : 0;
            if (instantFade)
            {
                _fade = active ? 1f : 0f;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SyncActive(instantFade: respawningAfterLoad);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SyncActive(instantFade: true);
            }
        }

        public override void CompTick()
        {
            SyncActive();
            base.CompTick();

            if (Active)
            {
                _fade = Mathf.Min(1f, _fade + FadeInPerTick);
            }
            else
            {
                _fade = Mathf.Max(0f, _fade - FadeOutPerTick);
            }
        }

        public override void Notify_LordDestroyed()
        {
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
        }

        public override string CompInspectStringExtra()
        {
            return ShouldBeActive ? base.CompInspectStringExtra() : null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!ShouldBeActive) { yield break; }

            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
        }

        public override void PostDraw()
        {
            if (BarrierMat == null || _fade <= 0.001f || !parent.Spawned || WorldComponent_GravshipController.GravshipRenderInProgess)
            {
                return;
            }

            var radius = Props.radius;
            if (radius <= 0f) { return; }

            var halfSize = radius + EdgeMargin;
            var drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.03f;

            var color = Props.color;
            color.a = _fade;

            var impactTicksAgo = Find.TickManager.TicksGame - LastInterceptTicksRef(this);
            var impactAlpha = impactTicksAgo >= 0 && impactTicksAgo < ImpactFlashTicks
                ? Mathf.Clamp01(1f - impactTicksAgo / (float)ImpactFlashTicks)
                : 0f;
            var impactAngleRad = (LastInterceptAngleRef(this) + 180f) * Mathf.Deg2Rad;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(RadiusId, radius);
            PropertyBlock.SetFloat(HalfSizeId, halfSize);
            PropertyBlock.SetFloat(ImpactAngleId, impactAngleRad);
            PropertyBlock.SetFloat(ImpactAlphaId, impactAlpha);

            var diameter = halfSize * 2f;
            var matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(diameter, 1f, diameter));
            Graphics.DrawMesh(MeshPool.plane10, matrix, BarrierMat, 0, null, 0, PropertyBlock);
        }
    }
}
