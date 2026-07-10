using LudeonTK;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaMapComponent : MapComponent, ICellBoolGiver, IDisposable
    {
        private VVRaceModSettings _modSettingCache = LoadedModManager.GetMod<VVRaceMod>().GetSettings<VVRaceModSettings>();

        public const float ViviFlowerChance = 0.04f;
        public const float EnvironmentManaMax = 1000f;

        public const int DiffuseInterval = 1129;
        public const int RefreshManaInterval = 250;

        private NativeArray<float> _manaReserveGrid;
        private NativeArray<float> _manaGrid;
        private NativeArray<float> _tmpGrid;
        private readonly HashSet<int> _dirtyManaCells = new HashSet<int>();

        private bool _checkFlowerCells;

        private bool _diffusionJobStart;
        private JobHandle _diffusionJobHandle;

        private CellBoolDrawer _cellBoolDrawer;
        private bool _drawManaOverlay;
        public bool manaOverlaySetting;

        public Color Color => new Color(1f, 1f, 1f);

        public float this[IntVec3 cell]
        {
            get
            {
                var idx = map.cellIndices.CellToIndex(cell);
                return Mathf.Clamp(_manaReserveGrid[idx] + _manaGrid[idx], 0f, EnvironmentManaMax);
            }
        }

        public ManaMapComponent(Map map) : base(map)
        {
            _manaReserveGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _tmpGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, _modSettingCache.manaGridOpacity);

            map.events.RoofChanged += Notify_RoofChanged;
        }

        public override void ExposeData()
        {
            if (_diffusionJobStart) { CompleteDiffuseManaJob(); }
            if (_dirtyManaCells.Count > 0) { ApplyDirtyReservedMana(); }

            IOUtility.ScribeNativeFloatArray(ref _manaReserveGrid, "encodedManaReserveGrid");
            IOUtility.ScribeNativeFloatArray(ref _manaGrid, "encodedManaGrid");

            Scribe_Values.Look(ref manaOverlaySetting, "manaOverlaySetting");
        }

        public override void FinalizeInit()
        {
            _tmpGrid.Clear();
            _cellBoolDrawer.SetDirty();

            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings += Notify_ModSettingChanged;

            ApplyAllReservedMana();
        }

        public override void MapComponentDraw()
        {
            if (!Find.ScreenshotModeHandler.Active)
            {
                _drawManaOverlay = _drawManaOverlay || manaOverlaySetting;
                if (_drawManaOverlay)
                {
                    _cellBoolDrawer.MarkForDraw();
                    _drawManaOverlay = false;
                }
            }

            _cellBoolDrawer.CellBoolDrawerUpdate();
        }

        public void MapComponentPreTick()
        {
            var ticksGame = GenTicks.TicksGame;
            var a = ticksGame % DiffuseInterval;
            var b = ticksGame % RefreshManaInterval;

            if ((a == 0 && b != 0) ||
                (a == 1 && b == 1))
            {
                ScheduleDiffuseManaJob();
            }
        }

        private List<(int idx, float mana)> _tmpFlowerCellCandidates = new List<(int idx, float mana)>();
        public override void MapComponentTick()
        {
            if (_diffusionJobStart)
            {
                CompleteDiffuseManaJob();

                if (_checkFlowerCells)
                {
                    _tmpFlowerCellCandidates.Clear();

                    var sum = 0f;
                    for (int i = 0; i < _manaGrid.Length; ++i)
                    {
                        var v = _manaGrid[i];
                        sum += v;
                        if (v >= 100f) { _tmpFlowerCellCandidates.Add((i, v)); }
                    }

                    // 마나 밀도가 높은 셀부터 스폰. 500 이상은 동순위 취급
                    _tmpFlowerCellCandidates.Sort((x, y) => Mathf.Min(y.mana, 500f).CompareTo(Mathf.Min(x.mana, 500f)));

                    var thingDef = ArcanePlantUtility.ViviFlowerDefs.RandomElement();
                    int flowerCount = (int)Mathf.Clamp(sum / 50000f, 1, 20) + Rand.Range(1, 5);
                    foreach (var (cellIndex, _) in _tmpFlowerCellCandidates)
                    {
                        if (flowerCount <= 0) { break; }

                        var c = map.cellIndices.IndexToCell(cellIndex);
                        if (ArcanePlantUtility.TrySpawnViviFlower(map, c, out _, flowerDef: thingDef))
                        {
                            flowerCount--;
                            thingDef = ArcanePlantUtility.ViviFlowerDefs.RandomElement();
                        }
                    }

                    _checkFlowerCells = false;
                }
            }

            if (GenTicks.TicksGame % RefreshManaInterval == 0)
            {
                var gameComponentMana = Current.Game.GetComponent<GameComponent_Mana>();
                if (gameComponentMana != null)
                {
                    foreach (var comp in gameComponentMana.AllManaComps)
                    {
                        var root = comp.parent.SpawnedParentOrMe;
                        if (root != null && root.Map == map)
                        {
                            comp.RefreshMana(this, root.Position, RefreshManaInterval);
                        }
                    }

                    _cellBoolDrawer.SetDirty();
                }
            }

            if (_dirtyManaCells.Count > 0)
            {
                ApplyDirtyReservedMana();
            }
        }

        public void MarkForDrawOverlay()
        {
            if (VVResearchProjectDefOf.VV_ArcaneBotany.IsFinished)
            {
                _drawManaOverlay = true;
            }
        }

        public void ChangeEnvironmentMana(IntVec3 cell, float flux)
        {
            var idx = map.cellIndices.CellToIndex(cell);
            if (_diffusionJobStart)
            {
                _manaReserveGrid[idx] += flux;
            }
            else
            {
                var currentMana = _manaGrid[idx] + _manaReserveGrid[idx];
                var updatedMana = Mathf.Clamp(currentMana + flux, 0f, EnvironmentManaMax);
                _manaReserveGrid[idx] = updatedMana - _manaGrid[idx];
            }
            _dirtyManaCells.Add(idx);
        }

        public void Dispose()
        {
            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings -= Notify_ModSettingChanged;

            if (!_diffusionJobHandle.IsCompleted) { _diffusionJobHandle.Complete(); }

            _tmpGrid.Dispose();
            _manaGrid.Dispose();
            _manaReserveGrid.Dispose();
        }

        private void Notify_ModSettingChanged()
        {
            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, _modSettingCache.manaGridOpacity);
        }

        private void Notify_RoofChanged(IntVec3 c)
        {
            foreach (var thing in c.GetThingList(map))
            {
                var comp = thing.TryGetComp<CompManaGlower>();
                if (comp != null)
                {
                    comp.UpdateLit(map);
                }
            }
        }

        private void ScheduleDiffuseManaJob()
        {
            if (_diffusionJobStart) { return; }

            _checkFlowerCells = Rand.Chance(ViviFlowerChance);

            var job = new ManaDiffusionJob()
            {
                w = map.Size.x,
                h = map.Size.z,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,
            };

            _diffusionJobHandle = job.Schedule(map.cellIndices.NumGridCells, map.Size.x);
            _diffusionJobStart = true;
        }

        private void CompleteDiffuseManaJob()
        {
            _diffusionJobHandle.Complete();

            var previousManaGrid = _manaGrid;
            _manaGrid = _tmpGrid;
            _tmpGrid = previousManaGrid;

            ApplyDirtyReservedMana();
            _diffusionJobStart = false;
        }

        private void ApplyDirtyReservedMana()
        {
            foreach (var idx in _dirtyManaCells)
            {
                _manaGrid[idx] = Mathf.Clamp(_manaGrid[idx] + _manaReserveGrid[idx], 0f, EnvironmentManaMax);
                _manaReserveGrid[idx] = 0f;
            }

            _dirtyManaCells.Clear();
        }

        private void ApplyAllReservedMana()
        {
            for (int i = 0; i < _manaReserveGrid.Length; ++i)
            {
                var reservedMana = _manaReserveGrid[i];
                if (reservedMana == 0f) { continue; }

                _manaGrid[i] = Mathf.Clamp(_manaGrid[i] + reservedMana, 0f, EnvironmentManaMax);
                _manaReserveGrid[i] = 0f;
            }

            _dirtyManaCells.Clear();
        }

        public bool GetCellBool(int index)
        {
            return _manaGrid[index] > 5f;
        }

        public Color GetCellExtraColor(int index)
        {
            if (map.fogGrid.IsFogged(index)) { return new Color(0f, 0f, 0f, 0f); }

            Color color;
            var value = _manaGrid[index] + _manaReserveGrid[index];
            if (value <= 100)
            {
                float normalized = value / 100.0f;
                color = Color.Lerp(new Color(0, 1, 1, 0.1f), new Color(0, 1, 1, 1), normalized);
            }
            else if (value <= 500)
            {
                float normalized = (value - 100) / 400.0f;
                color = Color.Lerp(new Color(0, 1, 1, 1), new Color(0, 0, 1, 1), normalized);
            }
            else
            {
                float normalized = (value - 500) / 500.0f;
                color = Color.Lerp(new Color(0, 0, 1, 1), new Color(0, 1, 0, 1), normalized);
            }

            color.r = Mathf.Round(color.r / 0.05f) * 0.05f;
            color.g = Mathf.Round(color.g / 0.05f) * 0.05f;
            color.b = Mathf.Round(color.b / 0.05f) * 0.05f;
            color.a = Mathf.Round(color.a / 0.05f) * 0.05f;
            return color;
        }
    }
}
