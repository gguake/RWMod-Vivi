using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaGrid : MapComponent, ICellBoolGiver, IDisposable
    {
        public const int RefreshManaInterval = 250;
        public const int DiffuseInterval = 2503;

        private const string ManaGridExposeName = "encodedManaGrid";

        private List<Thing> _manaProducers = new List<Thing>();
        private List<Thing> _manaConsumers = new List<Thing>();

        private NativeArray<float> _manaGrid;

        private JobHandle _jobHandle;
        private NativeArray<float> _tmpArray;

        private CellBoolDrawer _cellBoolDrawer;

        public Color Color => new Color(1f, 1f, 1f);

        public float this[IntVec3 cell] => _manaGrid[map.cellIndices.CellToIndex(cell)];

        public ManaGrid(Map map) : base(map)
        {
            _manaGrid = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Persistent);

            _cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3634, 0.4f);

            map.events.BuildingSpawned += Notify_BuildingSpawned;
            map.events.BuildingDespawned += Notify_BuildingDespawned;
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string encodedManaGrid = null;
                Scribe_Values.Look(ref encodedManaGrid, ManaGridExposeName);
                if (encodedManaGrid != null && encodedManaGrid.Length != 0)
                {
                    var bytes = IOUtility.DeserializeGZipBase64String(encodedManaGrid);
                    if (bytes.Length == map.cellIndices.NumGridCells * sizeof(float))
                    {
                        for (int i = 0; i < map.cellIndices.NumGridCells; ++i)
                        {
                            _manaGrid[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
                        }
                    }
                    else
                    {
                        Log.Warning($"Failed to load mana grid since map size not matchded : {bytes.Length} != {map.cellIndices.NumGridCells * 4}");
                    }
                }
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                var byteLength = map.cellIndices.NumGridCells * sizeof(float);
                var manaGridBytes = new byte[byteLength];
                unsafe
                {
                    var ptr = (IntPtr)_manaGrid.GetUnsafePtr();
                    Marshal.Copy(ptr, manaGridBytes, 0, byteLength);
                }

                var encodedManaGrid = IOUtility.SerializeGZipBase64String(manaGridBytes);
                Scribe_Values.Look(ref encodedManaGrid, ManaGridExposeName);
            }
        }

        public override void FinalizeInit()
        {
            _cellBoolDrawer.SetDirty();
        }

        public override void MapComponentUpdate()
        {
        }

        public override void MapComponentDraw()
        {
            // TODO: DEBUG
            if (!Find.ScreenshotModeHandler.Active)
            {
                _cellBoolDrawer.MarkForDraw();
            }

            _cellBoolDrawer.CellBoolDrawerUpdate();
        }

        public void MapComponentPreTick()
        {
            ScheduleDiffuseMana();
        }

        public override void MapComponentTick()
        {
            if (GenTicks.TicksGame % RefreshManaInterval == 0)
            {
                Refresh();
            }

            if (!_jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                _tmpArray.CopyTo(_manaGrid);
                _tmpArray.Dispose();

                _jobHandle = default;
                _tmpArray = default;
            }
        }

        public void AddMana(IntVec3 cell, float flux)
        {
            var idx = map.cellIndices.CellToIndex(cell);
            _manaGrid[idx] = Mathf.Max(0f, _manaGrid[idx] + flux);
        }

        public void Dispose()
        {
            _manaGrid.Dispose();
        }

        private void Refresh()
        {
            foreach (var plant in _manaProducers)
            {
                AddMana(plant.Position, 50f);
            }

            _cellBoolDrawer.SetDirty();
        }

        private void ScheduleDiffuseMana()
        {
            _tmpArray = new NativeArray<float>(map.cellIndices.NumGridCells, Allocator.Temp);
            var job = new ManaDiffusionJob()
            {
                mapSize = map.Size.ToIntVec2,
                manaGrid = _manaGrid,
                outputGrid = _tmpArray,
            };

            _jobHandle = job.Schedule(map.cellIndices.NumGridCells, Mathf.Max(map.Size.x, map.Size.z));
        }

        private void Notify_BuildingSpawned(Building building)
        {
            if (building is ArcanePlant)
            {
                _manaProducers.Add(building);
                _manaConsumers.Add(building);

                _cellBoolDrawer.SetDirty();
            }
        }

        private void Notify_BuildingDespawned(Building building)
        {
            if (building is ArcanePlant)
            {
                _manaProducers.Remove(building);
                _manaConsumers.Remove(building);

                _cellBoolDrawer.SetDirty();
            }
        }

        public bool GetCellBool(int index)
        {
            return _manaGrid[index] > 0.1f;
        }

        public Color GetCellExtraColor(int index)
        {
            Color color;
            var value = _manaGrid[index];
            if (value < 200)
            {
                float normalized = value / 300.0f;
                color = Color.Lerp(new Color(0, 0.84f, 1, 0), new Color(0, 0.84f, 1, 1), normalized);
            }
            else
            {
                float normalized = (value - 300) / 300.0f;
                color = Color.Lerp(new Color(0, 0.84f, 1, 1), new Color(0, 0, 1, 1), normalized);
            }

            color.r = Mathf.Round(color.r / 0.04f) * 0.04f;
            color.g = Mathf.Round(color.g / 0.04f) * 0.04f;
            color.b = Mathf.Round(color.b / 0.04f) * 0.04f;
            color.a = Mathf.Round(color.a / 0.04f) * 0.04f;
            return color;
        }
    }
}
