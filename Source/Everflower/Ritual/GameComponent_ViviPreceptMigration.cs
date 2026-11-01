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
