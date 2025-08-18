using LudeonTK;
using System;
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

        private bool _checkFlowerCells;
        private NativePriorityQueue<int, float, FloatMinComparer> _flowerCellQueue;

        private bool _shouldUpdate;
        private bool _updateJobStart;
        private bool _diffusionJobStart;
        private JobHandle _updateJobHandle;
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
                return _manaReserveGrid[idx] + _manaGrid[idx];
            }
        }

        public ManaMapComponent(Map map) : base(map)
        {
            _manaReserveGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _tmpGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);
            _flowerCellQueue = new NativePriorityQueue<int, float, FloatMinComparer>(map.cellIndices.NumGridCells, default(FloatMinComparer), Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, _modSettingCache.manaGridOpacity);

            map.events.RoofChanged += Notify_RoofChanged;
        }

        public override void ExposeData()
        {
            if (_updateJobStart) { CompleteUpdateManaJob(); }
            if (_diffusionJobStart) { CompleteDiffuseManaJob(); }

            IOUtility.ScribeNativeFloatArray(ref _manaReserveGrid, "encodedManaReserveGrid");
            IOUtility.ScribeNativeFloatArray(ref _manaGrid, "encodedManaGrid");

            Scribe_Values.Look(ref _shouldUpdate, "shouldUpdateMana");
            Scribe_Values.Look(ref manaOverlaySetting, "manaOverlaySetting");
        }

        public override void FinalizeInit()
        {
            _tmpGrid.Clear();
            _cellBoolDrawer.SetDirty();

            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings += Notify_ModSettingChanged;
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
            if (_updateJobStart)
            {
                CompleteUpdateManaJob();
            }

            var ticksGame = GenTicks.TicksGame;
            var a = ticksGame % DiffuseInterval;
            var b = ticksGame % RefreshManaInterval;

            if ((a == 0 && b != 0) ||
                (a == 1 && b == 1))
            {
                ScheduleDiffuseManaJob();
            }
        }

        public override void MapComponentTick()
        {
            if (_diffusionJobStart)
            {
                CompleteDiffuseManaJob();

                if (_checkFlowerCells)
                {
                    var sum = 0f;
                    for (int i = 0; i < _manaGrid.Length; ++i)
                    {
                        sum += _manaGrid[i];
                    }

                    var thingDef = ArcanePlantUtility.ViviFlowerDefs.RandomElement();
                    int flowerCount = (int)Mathf.Clamp(sum / 50000f, 1, 20) + Rand.Range(1, 5);
                    while (flowerCount > 0)
                    {
                        if (_flowerCellQueue.Count == 0) { break; }

                        _flowerCellQueue.Dequeue(out var cellIndex, out _);

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

            if (_shouldUpdate)
            {
                ScheduleUpdateManaJob();
                _shouldUpdate = false;
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
                _manaReserveGrid[idx] = flux;
                _shouldUpdate = true;
            }
            else
            {
                _manaGrid[idx] = Mathf.Clamp(_manaGrid[idx] + flux, 0f, EnvironmentManaMax);
            }
        }

        public void Dispose()
        {
            LoadedModManager.GetMod<VVRaceMod>().OnWriteSettings -= Notify_ModSettingChanged;

            if (!_updateJobHandle.IsCompleted) { _updateJobHandle.Complete(); }
            if (!_diffusionJobHandle.IsCompleted) { _diffusionJobHandle.Complete(); }

            _flowerCellQueue.Dispose();
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

        private void ScheduleUpdateManaJob()
        {
            if (_updateJobStart) { return; }

            var job = new ManaUpdateJob()
            {
                manaReserveGrid = _manaReserveGrid,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,
            };

            _updateJobHandle = job.Schedule(map.cellIndices.NumGridCells, map.Size.x);
            _updateJobStart = true;
        }

        private void ScheduleDiffuseManaJob()
        {
            if (_diffusionJobStart) { return; }

            _checkFlowerCells = Rand.Chance(ViviFlowerChance);
            if (_checkFlowerCells)
            {
                _flowerCellQueue.Clear();
            }

            var job = new ManaDiffusionJob()
            {
                w = map.Size.x,
                h = map.Size.z,
                manaGrid = _manaGrid,
                outputGrid = _tmpGrid,

                checkFlowerCells = _checkFlowerCells,
                flowerCellQueue = _flowerCellQueue,
            };

            _diffusionJobHandle = job.Schedule(map.cellIndices.NumGridCells, map.Size.x);
            _diffusionJobStart = true;
        }

        private void CompleteUpdateManaJob()
        {
            _updateJobHandle.Complete();

            _tmpGrid.CopyTo(_manaGrid);
            _manaReserveGrid.Clear();
            _updateJobStart = false;
        }

        private void CompleteDiffuseManaJob()
        {
            _diffusionJobHandle.Complete();

            _tmpGrid.CopyTo(_manaGrid);
            _diffusionJobStart = false;
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
