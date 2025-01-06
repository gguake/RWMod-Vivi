using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Designator_FortifyHoneycombWall : Designator
    {
        public override int DraggableDimensions => 2;

        public Designator_FortifyHoneycombWall()
        {
            defaultLabel = LocalizeString_Command.VV_Designator_FortifyHoneycombWall.Translate();
            defaultDesc = LocalizeString_Command.VV_Designator_FortifyHoneycombWallDesc.Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/SmoothSurface");

            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;

            soundSucceeded = SoundDefOf.Designate_SmoothSurface;
            hotKey = KeyBindingDefOf.Misc5;
        }

        public override bool Visible => VVResearchProjectDefOf.VV_Geosteel.IsFinished;

        protected override DesignationDef Designation => VVDesignationDefOf.VV_FortifyHoneycombWall;

        protected override bool DoTooltip => true;

        public override void DesignateThing(Thing t)
        {
            if (t != null && t.Spawned && t.def == VVThingDefOf.VV_ViviHoneycombWall)
            {
                t.Map.designationManager.AddDesignation(new Designation(t, Designation));
                t.Map.designationManager.TryRemoveDesignationOn(t, DesignationDefOf.Deconstruct);
            }
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            DesignateThing(c.GetEdifice(base.Map));
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (t == null)
            {
                return AcceptanceReport.WasRejected;
            }

            if (!t.Spawned)
            {
                return AcceptanceReport.WasRejected;
            }

            if (t.Faction != null && t.Faction != Faction.OfPlayer)
            {
                return AcceptanceReport.WasRejected;
            }

            if (t.def != VVThingDefOf.VV_ViviHoneycombWall)
            {
                return LocalizeString_Message.VV_Message_MustDesignateHoneycombWall.Translate();
            }

            if (base.Map.designationManager.DesignationOn(t, Designation) != null)
            {
                return LocalizeString_Message.VV_Message_HoneycombWallBeingFortified.Translate();
            }

            return AcceptanceReport.WasAccepted;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(base.Map))
            {
                return false;
            }

            if (c.Fogged(base.Map))
            {
                return false;
            }

            var edifice = c.GetEdifice(base.Map);
            return CanDesignateThing(edifice);
        }

        public override void DrawMouseAttachments()
        {
            var requiredAmount = VVThingDefOf.VV_ViviHardenHoneycombWall.CostList.FirstOrDefault(tdc => tdc.thingDef == VVThingDefOf.VV_Geosteel).count;
            if (requiredAmount <= 0)
            {
                return;
            }

            var center = Event.current.mousePosition + new Vector2(19f, 17f);
            var dragger = Find.DesignatorManager.Dragger;
            int selectedCount = ((!dragger.Dragging) ? 1 : dragger.DragCells.Count());

            Widgets.ThingIcon(new Rect(center.x, center.y, 27f, 27f), VVThingDefOf.VV_Geosteel);
            var labelRect = new Rect(center.x + 29f, center.y, 999f, 29f);

            int requiredCount = selectedCount * requiredAmount;
            var text = requiredCount.ToString();
            if (base.Map.resourceCounter.GetCount(VVThingDefOf.VV_Geosteel) < requiredCount)
            {
                GUI.color = Color.red;
                text += " (" + "NotEnoughStoredLower".Translate() + ")";
            }

            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(labelRect, text);
            }
        }
    }
}
