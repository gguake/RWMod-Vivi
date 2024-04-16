using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviRuins : MapParent
    {
        private Material cachedPostGenerateMat;

        public override Texture2D ExpandingIcon
        {
            get
            {
                if (!HasMap || Faction == null)
                {
                    return base.ExpandingIcon;
                }

                return Faction.def.FactionIcon;
            }
        }

        public override Color ExpandingIconColor
        {
            get
            {
                if (!HasMap || Faction == null)
                {
                    return base.ExpandingIconColor;
                }

                return Faction.Color;
            }
        }

        public override Material Material
        {
            get
            {
                if (!HasMap || Faction == null)
                {
                    return base.Material;
                }

                if (cachedPostGenerateMat == null)
                {
                    cachedPostGenerateMat = MaterialPool.MatFrom(
                        Faction.def.settlementTexturePath, 
                        ShaderDatabase.WorldOverlayTransparentLit, 
                        Faction.Color, 
                        WorldMaterials.WorldObjectRenderQueue);
                }

                return cachedPostGenerateMat;
            }
        }

        public Building_DreamumAltar Altar
        {
            get
            {
                if (_altar == null)
                {
                    _altar = Map.listerThings.ThingsOfDef(VVThingDefOf.VV_DreamumAltar).FirstOrDefault() as Building_DreamumAltar;
                }
                return _altar;
            }
        }
        private Building_DreamumAltar _altar;

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            Find.World.renderer.SetDirty<WorldLayer_WorldObjects>();
        }

        public override IEnumerable<IncidentTargetTagDef> IncidentTargetTags()
        {
            foreach (var tag in base.IncidentTargetTags())
            {
                yield return tag;
            }

            if (!HasMap) { yield break; }

            var altar = Altar;
            if (altar != null && altar.ShouldBigThreats)
            {
                yield return IncidentTargetTagDefOf.Map_RaidBeacon;
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (!HasMap) { return; }

            var altar = Altar;
            if (altar != null && altar.ShouldDreamumHaze)
            {
                var radius = (int)altar.CompDreamumTower.HazeWorldRadius;
                if (radius > 0)
                {
                    GenDraw.DrawWorldRadiusRing(Tile, radius);
                }
            }
        }
    }
}
