﻿using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class SymbolResolver_ViviSettlement : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;

            Rand.PushState();
            try
            {
                Rand.Seed = map.ConstantRandSeed;

                var faction = resolveParams.faction ?? Find.FactionManager.RandomEnemyFaction();

                #region 폰 생성
                if ((!resolveParams.settlementDontGeneratePawns) ?? true)
                {
                    var singlePawnLord = resolveParams.settlementLord = resolveParams.singlePawnLord ??
                        LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, resolveParams.rect.CenterCell, resolveParams.attackWhenPlayerBecameEnemy ?? false), map);

                    var traverseParms = TraverseParms.For(TraverseMode.PassDoors);
                    var p = resolveParams;
                    p.rect = resolveParams.rect;
                    p.faction = faction;
                    p.singlePawnLord = singlePawnLord;
                    p.pawnGroupKindDef = resolveParams.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement;
                    p.singlePawnSpawnCellExtraPredicate = resolveParams.singlePawnSpawnCellExtraPredicate ?? ((Predicate<IntVec3>)((IntVec3 x) => map.reachability.CanReachMapEdge(x, traverseParms)));
                    if (p.pawnGroupMakerParams == null)
                    {
                        p.pawnGroupMakerParams = new PawnGroupMakerParms();
                        p.pawnGroupMakerParams.tile = map.Tile;
                        p.pawnGroupMakerParams.faction = faction;
                        p.pawnGroupMakerParams.points = (resolveParams.settlementPawnGroupPoints ?? SymbolResolver_Settlement.DefaultPawnsPoints.RandomInRange) * 1.5f;
                        p.pawnGroupMakerParams.inhabitants = true;
                        p.pawnGroupMakerParams.seed = resolveParams.settlementPawnGroupSeed;
                    }

                    resolveParams.bedCount = PawnGroupMakerUtility.GeneratePawnKindsExample(SymbolResolver_PawnGroup.GetGroupMakerParms(map, p)).Count();
                    BaseGen.symbolStack.Push("pawnGroup", p);
                }
                #endregion

                //#region 방어벽
                //{
                //    var p = resolveParams;
                //    p.faction = faction;
                //    p.edgeDefenseWidth = 4;
                //    p.edgeThingMustReachMapEdge = resolveParams.edgeThingMustReachMapEdge ?? true;
                //    BaseGen.symbolStack.Push("edgeDefense", p);
                //}
                //#endregion

                #region 오염 지역이면 주변 정화
                if (ModsConfig.BiotechActive)
                {
                    var p = resolveParams;
                    p.rect = resolveParams.rect.ExpandedBy(7);
                    p.edgeUnpolluteChance = 0.5f;
                    BaseGen.symbolStack.Push("unpollute", p);
                }
                #endregion

                #region 맵 경계 정리
                {
                    var p = resolveParams;
                    p.rect = resolveParams.rect.ContractedBy(4);
                    p.faction = faction;
                    BaseGen.symbolStack.Push("ensureCanReachMapEdge", p);
                }
                #endregion

                #region 기지 생성
                {
                    var p = resolveParams;
                    p.rect = resolveParams.rect.ContractedBy(4);
                    p.faction = faction;
                    p.floorOnlyIfTerrainSupports = resolveParams.floorOnlyIfTerrainSupports ?? true;
                    BaseGen.symbolStack.Push("vv_vivi_basePart", p);
                }
                #endregion

                #region 물 위라면 다리 건설
                {
                    var p = resolveParams;
                    p.floorDef = TerrainDefOf.Bridge;
                    p.floorOnlyIfTerrainSupports = resolveParams.floorOnlyIfTerrainSupports ?? true;
                    p.allowBridgeOnAnyImpassableTerrain = resolveParams.allowBridgeOnAnyImpassableTerrain ?? true;
                    BaseGen.symbolStack.Push("floor", p);
                }
                #endregion
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
