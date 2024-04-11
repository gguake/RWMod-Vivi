using RimWorld.Planet;
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

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            Find.World.renderer.SetDirty<WorldLayer_WorldObjects>();
        }
    }
}
