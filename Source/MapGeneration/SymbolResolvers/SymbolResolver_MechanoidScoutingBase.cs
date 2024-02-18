//using RimWorld;
//using RimWorld.BaseGen;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Verse;
//using Verse.AI.Group;

//namespace VVRace
//{
//    public class SymbolResolver_MechanoidScoutingBase : SymbolResolver
//    {
//        public static readonly IntVec2 MainRoomSize = new IntVec2(17, 17);
//        public static readonly IntVec2 InnerRoomSize = new IntVec2(7, 7);
//        public static readonly IntVec2 Size = new IntVec2(MainRoomSize.x + 27, MainRoomSize.z + 27);

//        public override void Resolve(ResolveParams resolveParam)
//        {
//            var center = resolveParam.rect.CenterCell;
//            var fullRect = CellRect.CenteredOn(center, Size.x, Size.z);
//            var mainRoomRect = CellRect.CenteredOn(center, MainRoomSize.x, MainRoomSize.z);
//            var mainRoomInnerRect = mainRoomRect.ContractedBy(1);
//            var decorateRect = mainRoomInnerRect.ContractedBy(2);
//            var innerRoomRect = CellRect.CenteredOn(center, InnerRoomSize.x, InnerRoomSize.z);
//            var innerRoomInnerRect = innerRoomRect.ContractedBy(1);
//            var defenceRect1 = mainRoomRect.ExpandedBy(5);
//            var defenceRect2 = mainRoomRect.ExpandedBy(9);

//            #region 메카노이드
//            {
//                var lordJob = new LordJob_DefendPoint(
//                    center, 
//                    45f,
//                    false,
//                    false);
                
//                var lord = LordMaker.MakeNewLord(Faction.OfMechanoids, lordJob, BaseGen.globalSettings.map);

//                // 내부에 아포크리톤 1기 배치
//                {
//                    var p = resolveParam;
//                    p.rect = new CellRect(innerRoomInnerRect.minX + 1, innerRoomInnerRect.minZ, 1, 1);
//                    p.faction = resolveParam.faction;
//                    p.singlePawnKindDef = PawnKindDefOf.Mech_Apocriton;
//                    p.singlePawnLord = lord;
//                    BaseGen.symbolStack.Push("pawn", p);
//                }

//                // 외곽에 디아볼루스 4기 배치
//                foreach (var position in fullRect.ContractedBy(3).Corners.Select(v => v.ToIntVec2))
//                {
//                    var p = resolveParam;
//                    p.rect = new CellRect(position.x, position.z, 1, 1);
//                    p.faction = resolveParam.faction;
//                    p.singlePawnKindDef = PawnKindDefOf.Mech_Diabolus;
//                    p.singlePawnLord = lord;
//                    BaseGen.symbolStack.Push("pawn", p);
//                }

//                // 나머지는 threatPoints에 따라 적당히 배치
//                var pawnKindDefs = PawnUtility.GetCombatPawnKindsForPoints(
//                    MechClusterGenerator.MechKindSuitableForCluster,
//                    resolveParam.threatPoints ?? 0f,
//                    (PawnKindDef pk) => 1f / pk.combatPower).ToArray();

//                var spawningRect = fullRect;
//                var north = IntVec3.North.ToVector3();

//                if (!MapGenerator.TryGetVar<List<CellRect>>("UsedRects", out var usedRects))
//                {
//                    usedRects = new List<CellRect>();
//                    MapGenerator.SetVar("UsedRects", usedRects);
//                }

//                for (int i = 0; i < pawnKindDefs.Length; i++)
//                {
//                    var p = resolveParam;
//                    var spawnCell = IntVec3.Invalid;
//                    var t = Math.Min(spawningRect.Width, spawningRect.Height) / 2f;
//                    var vect = north.RotatedBy(360f / pawnKindDefs.Length * i) * t;

//                    if (CellFinder.TryFindRandomCellNear(
//                        spawningRect.CenterCell + vect.ToIntVec3(),
//                        BaseGen.globalSettings.map,
//                        10,
//                        c => !usedRects.Any((CellRect r) => r.Contains(c)), out var result) && SiteGenStepUtility.TryFindSpawnCellAroundOrNear(spawningRect, result, BaseGen.globalSettings.map, out spawnCell))
//                    {
//                        p.rect = CellRect.CenteredOn(spawnCell, 1, 1);
//                        p.faction = Faction.OfMechanoids;
//                        p.singlePawnKindDef = pawnKindDefs[i];
//                        p.singlePawnLord = lord;
//                        BaseGen.symbolStack.Push("pawn", p);
//                    }
//                }
//            }
//            #endregion

//            #region 외부 방어 진지2
//            {
//                var p = resolveParam;
//                p.rect = defenceRect2;
//                p.wallStuff = ThingDefOf.Steel;
//                p.faction = resolveParam.faction;
//                BaseGen.symbolStack.Push("vv_edgebarricade", p);
//            }
//            #endregion

//            #region 외부 방어 진지1
//            {
//                var p = resolveParam;
//                p.rect = defenceRect1;
//                p.wallStuff = ThingDefOf.Steel;
//                p.faction = resolveParam.faction;
//                BaseGen.symbolStack.Push("vv_edgebarricade", p);
//            }
//            #endregion

//            #region 기지 내벽 문
//            {
//                var wallRect = innerRoomRect;
//                var doorRects = new List<CellRect>()
//                {
//                    new CellRect(wallRect.minX, wallRect.minZ + wallRect.Height / 2, 1, 1),
//                    new CellRect(wallRect.maxX, wallRect.minZ + wallRect.Height / 2, 1, 1)
//                };

//                foreach (var doorRect in doorRects)
//                {
//                    var p = resolveParam;
//                    p.rect = doorRect;
//                    p.singleThingDef = ThingDefOf.Door;
//                    p.singleThingStuff = ThingDefOf.Steel;
//                    p.faction = resolveParam.faction;
//                    BaseGen.symbolStack.Push("thing", p);
//                }
//            }
//            #endregion

//            #region 기지 스캐너
//            {
//                var p = resolveParam;
//                p.rect = new CellRect(innerRoomRect.CenterCell.x, innerRoomRect.CenterCell.z, 1, 1);
//                p.singleThingDef = VVThingDefOf.VV_MechScanner;
//                p.faction = resolveParam.faction;
//                if (resolveParam.sitePart.parms is SitePartParams_MechanoidScoutingBase sitePartParms)
//                {
//                    p.postThingSpawn = sitePartParms.terminalSpawned;
//                }
//                else
//                {
//                    Log.Error($"SitePartParms is not SitePartParams_MechanoidScoutingBase. something wrong.");
//                }
//                BaseGen.symbolStack.Push("thing", p);
//            }
//            #endregion

//            #region 기지 내벽 방 램프
//            {
//                var positions = new List<IntVec2>()
//                {
//                    innerRoomInnerRect.TopLeft.ToIntVec2,
//                    innerRoomInnerRect.TopRight.ToIntVec2,
//                    innerRoomInnerRect.BottomLeft.ToIntVec2,
//                    innerRoomInnerRect.BottomRight.ToIntVec2,
//                };

//                foreach (var position in positions)
//                {
//                    var p = resolveParam;
//                    p.rect = new CellRect(position.x, position.z, 1, 1);
//                    p.singleThingDef = ThingDefOf.AncientLamp;
//                    p.faction = resolveParam.faction;
//                    BaseGen.symbolStack.Push("thing", p);
//                }
//            }
//            #endregion

//            #region 기지 내벽 방
//            {
//                var p = resolveParam;
//                p.rect = innerRoomRect;
//                p.wallStuff = ThingDefOf.Steel;
//                p.noRoof = false;
//                p.faction = resolveParam.faction;
//                BaseGen.symbolStack.Push("emptyRoom", p);
//            }
//            #endregion

//            #region 기지 외벽 문
//            {
//                var wallRect = mainRoomRect;
//                var doorRects = new List<CellRect>()
//                {
//                    new CellRect(wallRect.minX + wallRect.Width / 2, wallRect.minZ, 1, 1),
//                    new CellRect(wallRect.minX + wallRect.Width / 2, wallRect.maxZ, 1, 1),
//                    new CellRect(wallRect.minX, wallRect.minZ + wallRect.Height / 2, 1, 1),
//                    new CellRect(wallRect.maxX, wallRect.minZ + wallRect.Height / 2, 1, 1)
//                };

//                foreach (var doorRect in doorRects)
//                {
//                    var p = resolveParam;
//                    p.rect = doorRect;
//                    p.singleThingDef = ThingDefOf.Door;
//                    p.singleThingStuff = ThingDefOf.Steel;
//                    p.faction = resolveParam.faction;
//                    BaseGen.symbolStack.Push("thing", p);
//                }
//            }
//            #endregion

//            #region 기지 외벽 방 램프
//            {
//                foreach (var position in mainRoomInnerRect.Corners.Select(v => v.ToIntVec2))
//                {
//                    var p = resolveParam;
//                    p.rect = new CellRect(position.x, position.z, 1, 1);
//                    p.singleThingDef = ThingDefOf.AncientLamp;
//                    p.faction = resolveParam.faction;
//                    BaseGen.symbolStack.Push("thing", p);
//                }
//            }
//            #endregion

//            #region 기지 외벽 방 장식
//            {
//                {
//                    var thingDef = ThingDefOf.AncientToxifierGenerator;
//                    var left = decorateRect.minX;
//                    var right = decorateRect.maxX - (thingDef.size.x - 1);

//                    for (int i = 0; i < 2; ++i)
//                    {
//                        {
//                            var p = resolveParam;
//                            p.rect = new CellRect(left, decorateRect.minZ, 1, 1);
//                            p.singleThingDef = thingDef;
//                            p.faction = resolveParam.faction;
//                            BaseGen.symbolStack.Push("thing", p);
//                        }
//                        {
//                            var p = resolveParam;
//                            p.rect = new CellRect(right, decorateRect.minZ, 1, 1);
//                            p.singleThingDef = thingDef;
//                            p.faction = resolveParam.faction;
//                            BaseGen.symbolStack.Push("thing", p);
//                        }
//                        left += thingDef.size.x + 1;
//                        right -= thingDef.size.x + 1;
//                    }
//                }

//                {
//                    var thingDef = ThingDefOf.AncientStandardRecharger;
//                    {
//                        var p = resolveParam;
//                        p.rect = new CellRect(decorateRect.minX + 1, decorateRect.maxZ, 1, 1);
//                        p.singleThingDef = thingDef;
//                        p.faction = resolveParam.faction;
//                        BaseGen.symbolStack.Push("thing", p);
//                    }
//                    {
//                        var p = resolveParam;
//                        p.rect = new CellRect(decorateRect.maxX + 1 - (thingDef.size.x - 1), decorateRect.maxZ, 1, 1);
//                        p.singleThingDef = thingDef;
//                        p.faction = resolveParam.faction;
//                        BaseGen.symbolStack.Push("thing", p);
//                    }
//                }
//            }
//            #endregion

//            #region 기지 외벽 방
//            {
//                var p = resolveParam;
//                p.rect = mainRoomRect;
//                p.wallStuff = ThingDefOf.Steel;
//                p.floorDef = TerrainDefOf.PavedTile;
//                p.noRoof = false;
//                p.faction = resolveParam.faction;
//                BaseGen.symbolStack.Push("emptyRoom", p);
//            }
//            #endregion

//            #region 외부 정리
//            {
//                var p = resolveParam;
//                p.rect = fullRect;
//                BaseGen.symbolStack.Push("clear", p);
//            }
//            #endregion
//        }
//    }
//}
