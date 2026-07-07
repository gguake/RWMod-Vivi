using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ITab_ArcanePlant : ITab
    {
        private const float RowGap = 2f;
        private const float SectionGap = 10f;
        private const float RowPadding = 6f;
        private const float RuleIndent = 14f;
        private const float ValueColumnWidth = 104f;

        private static readonly Vector2 WinSize = new Vector2(460f, 450f);
        private static readonly Color PositiveValueColor = new Color(0.5f, 1f, 0.5f);
        private static readonly Color NegativeValueColor = ColorLibrary.RedReadable;

        private Vector2 _scrollPosition;
        private float _contentHeight;

        protected ArcanePlant SelPlant => SelThing as ArcanePlant;

        public override bool IsVisible
        {
            get
            {
                if (Find.Selector.NumSelected != 1) { return false; }
                if (!(SelThing is ArcanePlant plant)) { return false; }
                if (plant.Faction != null && plant.Faction.HostileTo(Faction.OfPlayerSilentFail)) { return false; }

                return true;
            }
        }

        public ITab_ArcanePlant()
        {
            size = WinSize;
            labelKey = LocalizeString_ITab.VV_ITab_ArcanePlant_TabLabel;
        }

        protected override void FillTab()
        {
            var plant = SelPlant;
            if (plant == null) { return; }

            var outRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(_contentHeight, outRect.height));

            Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);

            using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
            {
                var curY = 0f;
                DrawFunctionSection(viewRect.width, ref curY, plant);
                DrawManaSection(viewRect.width, ref curY, plant);
                DrawSynergySection(viewRect.width, ref curY, plant);
                DrawAppliedOverrideSection(viewRect.width, ref curY, plant);
                DrawWeaponSection(viewRect.width, ref curY, plant);

                _contentHeight = curY;
            }

            Widgets.EndScrollView();
        }

        #region 고유 기능

        private void DrawFunctionSection(float width, ref float curY, ArcanePlant plant)
        {
            var descriptions = CollectFunctionDescriptions(plant).ToList();
            if (descriptions.Count == 0) { return; }

            DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ArcanePlant_SectionFunction.Translate());

            var alternate = false;
            foreach (var description in descriptions)
            {
                DrawTextRow(width, ref curY, description, ref alternate);
            }
        }

        private IEnumerable<string> CollectFunctionDescriptions(ArcanePlant plant)
        {
            foreach (var description in plant.GetUniqueFunctionDescriptions())
            {
                yield return description;
            }

            foreach (var comp in plant.AllComps)
            {
                if (comp is IArcanePlantFunctionProvider provider)
                {
                    foreach (var description in provider.GetFunctionDescriptions())
                    {
                        yield return description;
                    }
                }
                else if (comp is CompPowerBattery battery)
                {
                    yield return LocalizeString_PlantFunction.VV_PlantFunction_Battery.Translate(
                        battery.Props.storedEnergyMax.ToString("F0"),
                        battery.Props.efficiency.ToStringPercent());
                }
            }
        }

        #endregion

        #region 마나

        private void DrawManaSection(float width, ref float curY, ArcanePlant plant)
        {
            var manaComp = plant.ManaComp;
            if (manaComp == null) { return; }

            var props = manaComp.Props;
            DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ArcanePlant_SectionMana.Translate());

            var alternate = false;
            if (props.manaCapacity > 0f)
            {
                DrawValueRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_ManaStored.Translate(),
                    $"{manaComp.Stored:F0} / {props.manaCapacity:F0}",
                    Color.white,
                    ref alternate);
            }

            var activeString = manaComp.Active ?
                LocalizeString_Inspector.VV_Inspector_PlantActive.Translate() :
                LocalizeString_Inspector.VV_Inspector_PlantInactive.Translate();

            DrawValueRow(
                width, ref curY,
                LocalizeString_ITab.VV_ITab_ArcanePlant_ManaState.Translate(),
                activeString,
                manaComp.Active ? PositiveValueColor : NegativeValueColor,
                ref alternate);

            if (props.manaGenerateRule != null)
            {
                // CompMana의 캐시(250틱 주기 갱신) 대신 실시간 계산값을 사용해 규칙별 행과 일치시킨다.
                var healthPct = (float)plant.HitPoints / plant.MaxHitPoints;
                var generatesByDay = plant.Spawned ?
                    Mathf.RoundToInt(props.manaGenerateRule.CalcManaFlux(plant) * healthPct) : 0;

                DrawValueRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_ManaGenerate.Translate(),
                    PerDay(FormatSigned(generatesByDay)),
                    generatesByDay > 0 ? PositiveValueColor : Color.gray,
                    ref alternate);

                foreach (var rule in EnumerateLeafRules(props.manaGenerateRule))
                {
                    var flux = plant.Spawned ? rule.CalcManaFlux(plant) : 0;
                    DrawValueRow(
                        width, ref curY,
                        rule.GetRuleLabel(),
                        PerDay(FormatSigned(flux)),
                        flux > 0 ? PositiveValueColor : Color.gray,
                        ref alternate,
                        indent: RuleIndent,
                        tooltip: rule.GetRuleString());
                }

                if (plant.HitPoints < plant.MaxHitPoints)
                {
                    DrawTextRow(
                        width, ref curY,
                        LocalizeString_ITab.VV_ITab_ArcanePlant_ManaGenerateDamaged.Translate(healthPct.ToStringPercent()).Colorize(NegativeValueColor),
                        ref alternate,
                        indent: RuleIndent);
                }
            }

            if (props.manaConsumeRule != null)
            {
                var consumesByDay = plant.Spawned ? props.manaConsumeRule.CalcManaFlux(plant) : 0;

                DrawValueRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_ManaConsume.Translate(),
                    PerDay(FormatSigned(-consumesByDay)),
                    consumesByDay > 0 ? NegativeValueColor : Color.gray,
                    ref alternate);

                foreach (var rule in EnumerateLeafRules(props.manaConsumeRule))
                {
                    var flux = plant.Spawned ? rule.CalcManaFlux(plant) : 0;
                    DrawValueRow(
                        width, ref curY,
                        rule.GetRuleLabel(),
                        PerDay(FormatSigned(-flux)),
                        flux > 0 ? NegativeValueColor : Color.gray,
                        ref alternate,
                        indent: RuleIndent,
                        tooltip: rule.GetRuleString());
                }
            }

            if (props.manaAbsorbPerDay > 0f)
            {
                DrawValueRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_ManaAbsorbLimit.Translate(),
                    PerDay(props.manaAbsorbPerDay.ToString("F0")),
                    Color.white,
                    ref alternate);
            }

            if (plant.Spawned)
            {
                var manaGrid = plant.Map.GetManaComponent();
                if (manaGrid != null)
                {
                    var externalChange = manaComp.ManaExternalChangeByDay;
                    DrawValueRow(
                        width, ref curY,
                        LocalizeString_ITab.VV_ITab_ArcanePlant_EnvManaOutput.Translate(),
                        PerDay(FormatSigned(externalChange)),
                        externalChange > 0 ? PositiveValueColor : (externalChange < 0 ? NegativeValueColor : Color.gray),
                        ref alternate);

                    DrawValueRow(
                        width, ref curY,
                        LocalizeString_ITab.VV_ITab_ArcanePlant_EnvManaHere.Translate(),
                        $"{manaGrid[plant.Position]:F0} / {ManaMapComponent.EnvironmentManaMax:F0}",
                        Color.white,
                        ref alternate);
                }
            }
        }

        // 상수 규칙은 기본 수치로 취급해 목록에 표시하지 않는다. (합계에는 포함)
        private IEnumerable<ManaFluxRule> EnumerateLeafRules(ManaFluxRule rule)
        {
            if (rule is ManaFluxRule_CompositeSum composite)
            {
                foreach (var child in composite.rules)
                {
                    foreach (var leaf in EnumerateLeafRules(child))
                    {
                        yield return leaf;
                    }
                }
            }
            else if (!(rule is ManaFluxRule_Constant))
            {
                yield return rule;
            }
        }

        #endregion

        #region 시너지

        private void DrawSynergySection(float width, ref float curY, ArcanePlant plant)
        {
            var comp = plant.GetComp<CompArcanePlantBulletOverride>();
            if (comp == null) { return; }

            DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ArcanePlant_SectionSynergy.Translate());

            var alternate = false;
            if (comp.Props.replacers != null)
            {
                foreach (var data in comp.Props.replacers)
                {
                    DrawTextRow(
                        width, ref curY,
                        LocalizeString_ITab.VV_ITab_ArcanePlant_SynergyReplace.Translate(
                            data.targetTurretDef.label,
                            data.replacer.chance.ToStringPercent(),
                            data.replacer.bulletDef.label),
                        ref alternate);
                }
            }

            if (comp.Props.modifiers != null)
            {
                foreach (var data in comp.Props.modifiers)
                {
                    DrawTextRow(
                        width, ref curY,
                        LocalizeString_ITab.VV_ITab_ArcanePlant_SynergyModify.Translate(
                            data.targetTurretDef.label,
                            BuildModifierString(data.modifier)),
                        ref alternate);
                }
            }
        }

        private void DrawAppliedOverrideSection(float width, ref float curY, ArcanePlant plant)
        {
            if (!(plant is ArcanePlant_Turret turret) || !turret.HasAnyBulletOverrides) { return; }

            DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ArcanePlant_SectionAppliedOverrides.Translate());

            var alternate = false;
            foreach (var pair in turret.BulletReplacerPairs)
            {
                DrawTextRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_AppliedReplace.Translate(
                        pair.Key.LabelCap,
                        pair.Value.chance.ToStringPercent(),
                        pair.Value.bulletDef.label),
                    ref alternate);
            }

            foreach (var pair in turret.BulletModifierPairs)
            {
                DrawTextRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_AppliedModify.Translate(
                        pair.Key.LabelCap,
                        BuildModifierString(pair.Value)),
                    ref alternate);
            }
        }

        private string BuildModifierString(BulletModifier modifier)
        {
            var parts = new List<string>();
            if (modifier.additionalDamage != 0)
            {
                parts.Add(LocalizeString_ITab.VV_ITab_ArcanePlant_BulletDamageBonus.Translate(
                    FormatSigned(modifier.additionalDamage)));
            }

            if (modifier.additionalArmorPenetration != 0f)
            {
                var sign = modifier.additionalArmorPenetration > 0f ? "+" : "";
                parts.Add(LocalizeString_ITab.VV_ITab_ArcanePlant_BulletArmorPenetrationBonus.Translate(
                    sign + modifier.additionalArmorPenetration.ToStringPercent()));
            }

            return parts.ToCommaList();
        }

        #endregion

        #region 장착 무기

        private void DrawWeaponSection(float width, ref float curY, ArcanePlant plant)
        {
            if (!(plant is ArcanePlant_Shootus shootus)) { return; }

            DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ArcanePlant_SectionWeapon.Translate());

            var alternate = false;
            var gun = shootus.Gun;
            if (gun != null)
            {
                const float rowHeight = 32f;
                var rowRect = new Rect(0f, curY, width, rowHeight);
                Widgets.DrawLightHighlight(rowRect);

                var iconRect = new Rect(RowPadding, curY + 2f, 28f, 28f);
                Widgets.ThingIcon(iconRect, gun);

                var labelRect = new Rect(iconRect.xMax + 6f, curY, width - iconRect.xMax - 36f, rowHeight);
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(labelRect, gun.LabelCap);
                }

                Widgets.InfoCardButton(width - 28f, curY + 4f, gun);

                if (Mouse.IsOver(rowRect))
                {
                    TooltipHandler.TipRegion(rowRect, gun.DescriptionDetailed);
                }

                curY += rowHeight + RowGap;
            }
            else if (shootus.ReservedWeapon != null)
            {
                DrawTextRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_WeaponReserved.Translate(shootus.ReservedWeapon.LabelCap),
                    ref alternate);
            }
            else
            {
                DrawTextRow(
                    width, ref curY,
                    LocalizeString_ITab.VV_ITab_ArcanePlant_WeaponNone.Translate(),
                    ref alternate);
            }
        }

        #endregion

        #region 공용 그리기

        private void DrawSectionTitle(float width, ref float curY, string title)
        {
            if (curY > 0f)
            {
                curY += SectionGap;
            }

            Widgets.ListSeparator(ref curY, width, title);
            curY += 3f;
        }

        private void DrawTextRow(float width, ref float curY, string text, ref bool alternate, float indent = 0f)
        {
            var textWidth = width - RowPadding * 2f - indent;
            var height = Text.CalcHeight(text, textWidth) + 4f;

            var rowRect = new Rect(0f, curY, width, height);
            if (alternate)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Widgets.Label(new Rect(RowPadding + indent, curY + 2f, textWidth, height - 4f), text);

            curY += height + RowGap;
            alternate = !alternate;
        }

        private void DrawValueRow(float width, ref float curY, string label, string value, Color valueColor, ref bool alternate, float indent = 0f, string tooltip = null)
        {
            var labelWidth = width - ValueColumnWidth - RowPadding * 2f - indent;
            var height = Mathf.Max(Text.CalcHeight(label, labelWidth) + 4f, 24f);

            var rowRect = new Rect(0f, curY, width, height);
            if (alternate)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Widgets.Label(new Rect(RowPadding + indent, curY + 2f, labelWidth, height - 4f), label);

            using (new TextBlock(TextAnchor.MiddleRight, valueColor))
            {
                Widgets.Label(new Rect(width - ValueColumnWidth - RowPadding, curY, ValueColumnWidth, height), value);
            }

            if (!tooltip.NullOrEmpty() && Mouse.IsOver(rowRect))
            {
                TooltipHandler.TipRegion(rowRect, tooltip);
            }

            curY += height + RowGap;
            alternate = !alternate;
        }

        private string PerDay(string value)
        {
            return LocalizeString_ITab.VV_ITab_ArcanePlant_PerDay.Translate(value);
        }

        private static string FormatSigned(int value)
        {
            return value.ToString("+#;-#;0");
        }

        #endregion
    }
}
