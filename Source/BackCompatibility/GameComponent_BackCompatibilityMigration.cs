using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace VVRace
{
    /// <summary>
    /// 문화·밈 Def의 스타일 태그 가중치를 게임 시작 직후에 복사해 둔다.
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

    /// <summary>
    /// 구버전 세이브를 불러올 때 필요한 보정을 모아 둔 컴포넌트.
    ///
    /// 1. 날씨 변경 의식 교리 (매 로드, 멱등)
    ///    VV_ChangeWeatherRitual이 커스텀 서브클래스가 아닌 base Precept_Ritual로 로드된 경우 Precept_RitualChangeWeather로 바꾼다.
    ///
    /// 2. 이념 헤어 표 (세이브당 한 번)
    ///    VV_HairH / VV_HairI가 HairDef를 상속한 Def 클래스(RPEF.ScaleableHairDef)였던 시절에 만든 세이브를 한 번 보정한다.
    ///
    /// 예전 세이브에 저장된 GameComponent_ViviPreceptMigration 노드는 BackCompatibilityPatch가 이 타입으로 연결한다.
    /// 그 노드에는 헤어 보정 플래그가 없으므로 헤어 보정이 한 번 실행된다.
    ///
    /// 헤어 표 보정 상세: 그때는 두 헤어의 Def.index가 0, 1이어서 이념의 헤어 표(DefMap, 인덱스 순서대로 저장됨)에서
    /// DefDatabase&lt;HairDef&gt;의 첫 두 헤어(바닐라 BriefPonytail, Princess)와 같은 칸을 썼다.
    /// 지금은 두 헤어가 제 칸을 갖지만, 예전 세이브에는
    ///  - 첫 두 헤어의 칸에 비비 헤어 값이 덮어써져 있고
    ///  - 비비 헤어의 새 칸은 기본값(Never)으로 로드된다.
    /// 그래서 이 네 헤어의 빈도·성별만 문화와 밈의 태그로 다시 계산한다.
    /// 어느 값이 플레이어가 직접 고른 것인지는 세이브만으로 가릴 수 없어서, 네 헤어는 계산값으로 되돌린다.
    /// </summary>
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

        // 세이브 헤더의 모드 목록을 알 수 없으면 보정하는 쪽을 택한다. (보정 대상 세이브를 놓치는 쪽이 더 나쁘다)
        private static bool SaveWasMadeWithThisMod()
        {
            var modIds = ScribeMetaHeaderUtility.loadedModIdsList;
            if (modIds == null || modIds.Count == 0) { return true; }

            foreach (var modId in modIds)
            {
                if (modId == null) { continue; }

                var id = modId.ToLowerInvariant();
                if (id.EndsWith("_steam")) { id = id.Substring(0, id.Length - "_steam".Length); }
                if (id == PackageId) { return true; }
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
