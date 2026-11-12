using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace VVRace
{
    /// <summary>
    /// CultureDef, MemeDef의 스타일 태그 가중치를 게임 시작 직후에 복사해 두기 위한 용도
    /// 바닐라의 IdeoStyleTracker.RecalculateAvailableStyleItems는 밈과 같은 태그를 가진 문화 Def의 태그 객체를
    /// 직접 고치기 때문에, 이념 표가 한 번이라도 계산된 뒤의 Def 값은 원래 값이 아니다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class StyleTagWeightSnapshot
    {
        public struct TagWeight
        {
            public string tag;
            public float baseWeight;
            public float weightFactor;
        }

        private static readonly Dictionary<Def, List<TagWeight>> _snapshot = new Dictionary<Def, List<TagWeight>>();

        /// <summary>복사에 실패했으면 false. 이때는 가중치를 알 수 없으니 표를 건드리면 안 된다.</summary>
        public static bool Valid { get; private set; }

        static StyleTagWeightSnapshot()
        {
            try
            {
                var baseWeight = AccessTools.FieldRefAccess<StyleItemTagWeighted, float>("baseWeight");
                var weightFactor = AccessTools.FieldRefAccess<StyleItemTagWeighted, float>("weightFactor");

                void Capture(Def def, List<StyleItemTagWeighted> tags)
                {
                    if (tags == null) { return; }

                    var list = new List<TagWeight>(tags.Count);
                    foreach (var tag in tags)
                    {
                        if (tag?.Tag == null) { continue; }

                        list.Add(new TagWeight { tag = tag.Tag, baseWeight = baseWeight(tag), weightFactor = weightFactor(tag) });
                    }

                    _snapshot[def] = list;
                }

                foreach (var culture in DefDatabase<CultureDef>.AllDefsListForReading)
                {
                    Capture(culture, culture.styleItemTags);
                }
                foreach (var meme in DefDatabase<MemeDef>.AllDefsListForReading)
                {
                    Capture(meme, meme.styleItemTags);
                }

                Valid = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to snapshot style tag weights: {ex}");
            }
        }

        public static List<TagWeight> Get(Def def)
        {
            return def != null && _snapshot.TryGetValue(def, out var list) ? list : null;
        }
    }

    public class GameComponent_BackCompatibilityMigration : GameComponent
    {
        private const int CollidedSlotCount = 2;
        private const string PackageId = "gguake.race.vivi";
        private static readonly string[] FormerSubclassHairDefNames = { "VV_HairH", "VV_HairI" };

        private bool _hairTableRepaired;

        public GameComponent_BackCompatibilityMigration(Game game) { }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            _hairTableRepaired = true;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            MigratePrecepts();
            RepairHairTablesOnce();
            RepairViviEndogenes();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _hairTableRepaired, "hairTableRepaired", defaultValue: false);
        }

        #region 날씨 변경 의식 교리
        private static void MigratePrecepts()
        {
            var ideoManager = Find.IdeoManager;
            if (ideoManager == null) { return; }

            var migratedCount = 0;
            foreach (var ideo in ideoManager.IdeosListForReading)
            {
                var precepts = ideo.PreceptsListForReading;
                for (int i = 0; i < precepts.Count; i++)
                {
                    var precept = precepts[i];
                    if (precept == null) { continue; }

                    // 날씨 변경 의식이 커스텀 서브클래스가 아닌 base Precept_Ritual로 로드된 경우 교체.
                    if (precept.def == VVPreceptDefOf.VV_ChangeWeatherRitual && !(precept is Precept_RitualChangeWeather))
                    {
                        try
                        {
                            var migrated = new Precept_RitualChangeWeather();
                            CopyAllFields(precept, migrated);
                            precepts[i] = migrated;
                            migratedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[ViViRace] failed to migrate precept {precept.def?.defName}: {ex}");
                        }
                    }
                }
            }

            if (migratedCount > 0)
            {
                Log.Message($"[ViViRace] migrated {migratedCount} change-weather ritual precept(s) to {nameof(Precept_RitualChangeWeather)}.");
            }
        }

        private static void CopyAllFields(Precept source, Precept dest)
        {
            // Precept_Ritual 및 그 상위 타입(Precept)에 선언된 모든 인스턴스 필드를 복사한다.
            for (var type = typeof(Precept_Ritual); type != null && type != typeof(object); type = type.BaseType)
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var field in fields)
                {
                    field.SetValue(dest, field.GetValue(source));
                }
            }
        }
        #endregion

        #region 비비 생식 유전자
        private static void RepairViviEndogenes()
        {
            var xenotype = VVXenotypeDefOf.VV_Vivi;
            if (xenotype?.genes == null) { return; }

            // 조회 결과는 공유 목록이므로 유전자 변경 중 다시 조회되어도 안전하도록 복사하고 중복을 없앤다.
            var repairedCount = 0;
            foreach (var pawn in PawnsFinder.All_AliveOrDead)
            {
                var genes = pawn?.genes;
                if (genes == null || genes.Xenotype != xenotype) { continue; }

                try
                {
                    var repaired = false;
                    foreach (var geneDef in xenotype.genes)
                    {
                        // 같은 이종 유전자가 있어도 생식 유전자는 별도로 있어야 한다.
                        if (genes.HasEndogene(geneDef)) { continue; }

                        if (genes.AddGene(geneDef, xenogene: false) != null)
                        {
                            repaired = true;
                        }
                    }

                    if (repaired) { repairedCount++; }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to repair endogenes of {pawn}: {ex}");
                }
            }

            if (repairedCount > 0)
            {
                Log.Message($"[ViViRace] repaired endogenes of {repairedCount} Vivi pawn(s).");
            }
        }
        #endregion

        #region 이념 헤어 표
        private void RepairHairTablesOnce()
        {
            if (_hairTableRepaired) { return; }

            // 성공 여부와 무관하게 한 번만 시도한다. 플레이어가 이후에 손본 값을 매번 덮어쓰면 안 된다.
            _hairTableRepaired = true;

            // 이 모드 없이 만든 세이브에는 겹친 칸이 없으니 건드리지 않는다.
            if (!SaveWasMadeWithThisMod()) { return; }

            try
            {
                RepairHairTables();
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to repair ideoligion hair tables: {ex}");
            }
        }

        // 세이브 헤더의 모드 목록을 알 수 없으면 보정하는 쪽을 택한다.
        private static bool SaveWasMadeWithThisMod()
        {
            var modIds = ScribeMetaHeaderUtility.loadedModIdsList;
            if (modIds == null || modIds.Count == 0) { return true; }

            var packageId = LoadedModManager.GetMod<VVRaceMod>().Content.PackageId;
            foreach (var modId in modIds)
            {
                if (modId == null) { continue; }

                var id = modId.ToLowerInvariant();
                if (id.EqualsIgnoreCase(packageId)) { return true; }
            }

            return false;
        }

        private static void RepairHairTables()
        {
            if (!StyleTagWeightSnapshot.Valid) { return; }

            var ideos = Find.IdeoManager?.IdeosListForReading;
            if (ideos == null || ideos.Count == 0) { return; }

            var targets = new List<HairDef>();
            var allHairs = DefDatabase<HairDef>.AllDefsListForReading;
            for (int i = 0; i < CollidedSlotCount && i < allHairs.Count; ++i)
            {
                targets.Add(allHairs[i]);
            }
            foreach (var defName in FormerSubclassHairDefNames)
            {
                var def = DefDatabase<HairDef>.GetNamedSilentFail(defName);
                if (def != null && !targets.Contains(def))
                {
                    targets.Add(def);
                }
            }

            var repairedCount = 0;
            foreach (var ideo in ideos)
            {
                // 문화가 없으면 바닐라의 표 재계산도 할 수 없다.
                if (ideo?.style == null || ideo.culture == null) { continue; }

                var weights = CollectTagWeights(ideo);
                foreach (var hair in targets)
                {
                    ideo.style.SetFrequency(hair, PawnStyleItemChooser.GetStyleItemFrequency(Likelihood(hair, weights)));
                    ideo.style.SetGender(hair, hair.styleGender);
                }

                ideo.style.EnsureAtLeastOneStyleItemAvailable();
                repairedCount++;
            }

            if (repairedCount > 0)
            {
                Log.Message($"[ViViRace] repaired hair style entries ({targets.ToStringSafeEnumerable()}) of {repairedCount} ideoligion(s).");
            }
        }

        // IdeoStyleTracker.RecalculateAvailableStyleItems와 같은 규칙: 같은 태그는 baseWeight를 더하고 weightFactor를 곱한다.
        private static Dictionary<string, (float baseWeight, float weightFactor)> CollectTagWeights(Ideo ideo)
        {
            var weights = new Dictionary<string, (float baseWeight, float weightFactor)>();

            void Add(Def def)
            {
                var tags = StyleTagWeightSnapshot.Get(def);
                if (tags == null) { return; }

                foreach (var tag in tags)
                {
                    if (weights.TryGetValue(tag.tag, out var current))
                    {
                        weights[tag.tag] = (current.baseWeight + tag.baseWeight, current.weightFactor * tag.weightFactor);
                    }
                    else
                    {
                        weights.Add(tag.tag, (tag.baseWeight, tag.weightFactor));
                    }
                }
            }

            Add(ideo.culture);
            if (ideo.memes != null)
            {
                foreach (var meme in ideo.memes)
                {
                    Add(meme);
                }
            }

            return weights;
        }

        // PawnStyleItemChooser.StyleItemChoiceLikelihoodFromTags와 같은 계산.
        private static float Likelihood(StyleItemDef styleItem, Dictionary<string, (float baseWeight, float weightFactor)> weights)
        {
            var baseWeight = 0f;
            var weightFactor = 1f;
            if (styleItem.styleTags != null)
            {
                foreach (var tag in styleItem.styleTags)
                {
                    if (tag != null && weights.TryGetValue(tag, out var weight))
                    {
                        baseWeight += weight.baseWeight;
                        weightFactor *= weight.weightFactor;
                    }
                }
            }

            return Math.Max(0f, baseWeight * weightFactor);
        }
        #endregion
    }
}
