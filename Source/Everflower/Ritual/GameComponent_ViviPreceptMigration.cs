using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace VVRace
{
    public class GameComponent_ViviPreceptMigration : GameComponent
    {
        public GameComponent_ViviPreceptMigration(Game game) { }

        public override void LoadedGame()
        {
            base.LoadedGame();
            MigratePrecepts();
        }

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
    }
}
