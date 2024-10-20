using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace CaveTweaks
{
    [HarmonyPatchCategory("cavetweaks_cavegeneration")]
    class CaveGeneration
    {
        public static ICoreServerAPI ServerAPI { get; set; } = null;
        public Harmony harmonyPatcher;
        private static IWorldGenBlockAccessor worldGenBlockAccessor;
        private static MTRandom caveRand;
        private static NormalizedSimplexNoise basaltNoise;

        public static int CaveAmountDivisor => InitializeMod.ModConfig.CaveAmountDivisor;
        public static bool CreateShafts => InitializeMod.ModConfig.CreateShafts;
        public static float TunnelVerticalSizeMultiplier => InitializeMod.ModConfig.TunnelVerticalSizeMultiplier;
        public static float TunnelHorizontalSizeMultiplier => InitializeMod.ModConfig.TunnelHorizontalSizeMultiplier;
        public static float TunnelCurvinessMultiplier => InitializeMod.ModConfig.TunnelCurvinessMultiplier;

        public void Init(ICoreServerAPI api)
        {
            ServerAPI = api;
            Debug.Log($"Initialized [{InitializeMod.ModInfo.Name}] {nameof(CaveGeneration)}!");
        }

        public void Patch()
        {
            if(!Harmony.HasAnyPatches("cavetweaks_cavegeneration"))
            {
                harmonyPatcher = new Harmony("cavetweaks_cavegeneration");
                harmonyPatcher.PatchCategory("cavetweaks_cavegeneration");
            }
        }

        public void Unpatch()
        {
            if(Harmony.HasAnyPatches("cavetweaks_cavegeneration"))
            {
                harmonyPatcher.UnpatchAll();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "CarveShaft")]
        public static bool CarveShaft(GenCaves __instance, IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int caveCurrentIteration, int maxIterations, int branchLevel)
        {
            float vertAngleChange = 0f;
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            int currentIteration = 0;

            FieldInfo chunkRandField = AccessTools.Field(typeof(GenCaves), "chunkRand");
            LCGRandom chunkRand = (LCGRandom)chunkRandField.GetValue(__instance);

            while(currentIteration++ < maxIterations)
            {
                float relPos = (float)currentIteration / (float)maxIterations;
                float horRadius = horizontalSize * (1f - relPos * 0.33f);
                float vertRadius = horRadius * verticalSize;
                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);

                if(vertRadius < 1f)
                {
                    vertAngle *= 0.1f;
                }

                posX += (double)(GameMath.FastCos(horAngle) * advanceHor);
                posY += (double)GameMath.Clamp(advanceVer, -vertRadius, vertRadius);
                posZ += (double)(GameMath.FastSin(horAngle) * advanceHor);
                vertAngle += 0.1f * vertAngleChange;
                vertAngleChange = 0.9f * vertAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() / 3f;

                if(maxIterations - currentIteration < 10)
                {
                    int num = 3 + caveRand.NextInt(4);

                    for(int i = 0; i < num; i++)
                    {
                        CarveTunnel(__instance, chunks, chunkX, chunkZ, posX, posY, posZ, chunkRand.NextFloat() * 6.2831855f, (chunkRand.NextFloat() - 0.5f) * 0.25f, horizontalSize + 1f, verticalSize, caveCurrentIteration, maxIterations, 1, false, 0.1f, false);
                    }

                    return false;
                }

                if((caveRand.NextInt(5) != 0 || horRadius < 2f) && posX > (double)(-(double)horRadius * 2f) && posX < (double)(32f + horRadius * 2f) && posZ > (double)(-(double)horRadius * 2f) && posZ < (double)(32f + horRadius * 2f))
                {
                    SetBlocks(__instance, chunks, horRadius, vertRadius, posX, posY, posZ, terrainheightmap, rainheightmap, chunkX, chunkZ, false);
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "SetBlocks")]
        public static bool SetBlocks(GenCaves __instance, IServerChunk[] chunks, float horRadius, float vertRadius, double centerX, double centerY, double centerZ, ushort[] terrainheightmap, ushort[] rainheightmap, int chunkX, int chunkZ, bool genHotSpring)
        {
            FieldInfo globalConfigField = AccessTools.Field(typeof(GenCaves), "GlobalConfig");
            GlobalConfig globalConfig = (GlobalConfig)globalConfigField.GetValue(__instance);

            FieldInfo airBlockField = AccessTools.Field(typeof(GenCaves), "airBlockId");
            int airBlockId = (int)airBlockField.GetValue(__instance);

            int worldheight = ServerAPI.WorldManager.MapSizeY;
            IMapChunk mapchunk = chunks[0].MapChunk;
            horRadius += 1f;
            vertRadius += 2f;
            int num = (int)GameMath.Clamp(centerX - (double)horRadius, 0.0, 31.0);
            int maxdx = (int)GameMath.Clamp(centerX + (double)horRadius + 1.0, 0.0, 31.0);
            int mindy = (int)GameMath.Clamp(centerY - (double)(vertRadius * 0.7f), 1.0, (double)(worldheight - 1));
            int maxdy = (int)GameMath.Clamp(centerY + (double)vertRadius + 1.0, 1.0, (double)(worldheight - 1));
            int mindz = (int)GameMath.Clamp(centerZ - (double)horRadius, 0.0, 31.0);
            int maxdz = (int)GameMath.Clamp(centerZ + (double)horRadius + 1.0, 0.0, 31.0);
            double hRadiusSq = (double)(horRadius * horRadius);
            double vRadiusSq = (double)(vertRadius * vertRadius);
            double distortStrength = GameMath.Clamp((double)vertRadius / 4.0, 0.0, 0.1);

            for(int lx = num; lx <= maxdx; lx++)
            {
                double xdistRel = ((double)lx - centerX) * ((double)lx - centerX) / hRadiusSq;

                for(int lz = mindz; lz <= maxdz; lz++)
                {
                    double zdistRel = ((double)lz - centerZ) * ((double)lz - centerZ) / hRadiusSq;
                    double heightrnd = (double)(mapchunk.CaveHeightDistort[lz * 32 + lx] - 127) * distortStrength;

                    for(int y = mindy; y <= maxdy + 10; y++)
                    {
                        double num2 = (double)y - centerY;
                        double heightOffFac = (num2 > 0.0) ? (heightrnd * heightrnd) : 0.0;
                        double ydistRel = num2 * num2 / (vRadiusSq + heightOffFac);

                        if(xdistRel + ydistRel + zdistRel <= 1.0 && y <= worldheight - 1)
                        {
                            int ly = y % 32;

                            if(ServerAPI.World.Blocks[chunks[y / 32].Data.GetFluid((ly * 32 + lz) * 32 + lx)].LiquidCode != null)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            horRadius -= 1f;
            vertRadius -= 2f;
            int num3 = (int)GameMath.Clamp(centerX - (double)horRadius, 0.0, 31.0);
            maxdx = (int)GameMath.Clamp(centerX + (double)horRadius + 1.0, 0.0, 31.0);
            mindz = (int)GameMath.Clamp(centerZ - (double)horRadius, 0.0, 31.0);
            maxdz = (int)GameMath.Clamp(centerZ + (double)horRadius + 1.0, 0.0, 31.0);
            mindy = (int)GameMath.Clamp(centerY - (double)(vertRadius * 0.7f), 1.0, (double)(worldheight - 1));
            maxdy = (int)GameMath.Clamp(centerY + (double)vertRadius + 1.0, 1.0, (double)(worldheight - 1));
            hRadiusSq = (double)(horRadius * horRadius);
            vRadiusSq = (double)(vertRadius * vertRadius);
            int geoActivity = getGeologicActivity(chunkX * 32 + (int)centerX, chunkZ * 32 + (int)centerZ);
            genHotSpring &= (geoActivity > 128);

            if(genHotSpring && centerX >= 0.0 && centerX < 32.0 && centerZ >= 0.0 && centerZ < 32.0)
            {
                Dictionary<Vec3i, HotSpringGenData> data = mapchunk.GetModdata<Dictionary<Vec3i, HotSpringGenData>>("hotspringlocations", null);

                if(data == null)
                {
                    data = new Dictionary<Vec3i, HotSpringGenData>();
                }

                data[new Vec3i((int)centerX, (int)centerY, (int)centerZ)] = new HotSpringGenData
                {
                    horRadius = (double)horRadius
                };

                mapchunk.SetModdata<Dictionary<Vec3i, HotSpringGenData>>("hotspringlocations", data);
            }

            int yLavaStart = geoActivity * 16 / 128;

            for(int lx2 = num3; lx2 <= maxdx; lx2++)
            {
                double xdistRel = ((double)lx2 - centerX) * ((double)lx2 - centerX) / hRadiusSq;

                for(int lz2 = mindz; lz2 <= maxdz; lz2++)
                {
                    double zdistRel = ((double)lz2 - centerZ) * ((double)lz2 - centerZ) / hRadiusSq;
                    double heightrnd2 = (double)(mapchunk.CaveHeightDistort[lz2 * 32 + lx2] - 127) * distortStrength;
                    int surfaceY = (int)terrainheightmap[lz2 * 32 + lx2];

                    for(int y2 = maxdy + 10; y2 >= mindy; y2--)
                    {
                        double num4 = (double)y2 - centerY;
                        double heightOffFac2 = (num4 > 0.0) ? (heightrnd2 * heightrnd2 * Math.Min(1.0, (double)Math.Abs(y2 - surfaceY) / 10.0)) : 0.0;
                        double ydistRel = num4 * num4 / (vRadiusSq + heightOffFac2);

                        if(y2 <= worldheight - 1 && xdistRel + ydistRel + zdistRel <= 1.0)
                        {
                            if((int)terrainheightmap[lz2 * 32 + lx2] == y2)
                            {
                                terrainheightmap[lz2 * 32 + lx2] = (ushort)(y2 - 1);
                                int num5 = lz2 * 32 + lx2;
                                rainheightmap[num5] -= 1;
                            }

                            IChunkBlocks chunkBlockData = chunks[y2 / 32].Data;
                            int index3d = (y2 % 32 * 32 + lz2) * 32 + lx2;

                            if(y2 == 11)
                            {
                                if(basaltNoise.Noise((double)(chunkX * 32 + lx2), (double)(chunkZ * 32 + lz2)) > 0.65)
                                {
                                    chunkBlockData[index3d] = globalConfig.basaltBlockId;
                                    terrainheightmap[lz2 * 32 + lx2] = Math.Max((ushort)terrainheightmap[lz2 * 32 + lx2], (ushort)11);
                                    rainheightmap[lz2 * 32 + lx2] = Math.Max((ushort)rainheightmap[lz2 * 32 + lx2], (ushort)11);
                                }
                                else
                                {
                                    chunkBlockData[index3d] = 0;

                                    if(y2 > yLavaStart)
                                    {
                                        chunkBlockData[index3d] = globalConfig.basaltBlockId;
                                    }
                                    else
                                    {
                                        chunkBlockData.SetFluid(index3d, globalConfig.lavaBlockId);
                                    }

                                    if(y2 <= yLavaStart)
                                    {
                                        BlockPos blockPos = new BlockPos(chunkX * 32 + lx2, y2, chunkZ * 32 + lz2);
                                        worldGenBlockAccessor.ScheduleBlockLightUpdate(blockPos, airBlockId, globalConfig.lavaBlockId);
                                    }
                                }
                            }
                            else if(y2 < 12)
                            {
                                chunkBlockData[index3d] = 0;

                                if(y2 > yLavaStart)
                                {
                                    chunkBlockData[index3d] = globalConfig.basaltBlockId;
                                }
                                else
                                {
                                    chunkBlockData.SetFluid(index3d, globalConfig.lavaBlockId);
                                }
                            }
                            else
                            {
                                chunkBlockData.SetBlockAir(index3d);
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static int getGeologicActivity(int posx, int posz)
        {
            int regionSize = ServerAPI.World.BlockAccessor.RegionSize;
            IMapRegion mapRegion = worldGenBlockAccessor.GetMapRegion(posx / regionSize, posz / regionSize);
            IntDataMap2D climateMap = (mapRegion != null) ? mapRegion.ClimateMap : null;

            if(climateMap == null)
            {
                return 0;
            }

            int regionChunkSize = regionSize / 32;
            float fac = (float)climateMap.InnerSize / (float)regionChunkSize;
            int rlX = posx / 32 % regionChunkSize;
            int rlZ = posz / 32 % regionChunkSize;
            return climateMap.GetUnpaddedInt((int)((float)rlX * fac), (int)((float)rlZ * fac)) & 255;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "OnWorldGenBlockAccessor")]
        public static bool OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldGenBlockAccessor = chunkProvider.GetBlockAccessor(false);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "GeneratePartial")]
        public static bool GeneratePartial(GenCaves __instance, IServerChunk[] chunks, int chunkX, int chunkZ, int cdx, int cdz)
        {
            int worldheight = ServerAPI.WorldManager.MapSizeY;

            worldGenBlockAccessor.BeginColumn();

            // Calculate the number of caves based on the world height... - Tim
            int depthBasedCaveCount = (worldheight - 20) / CaveAmountDivisor;

            FieldInfo chunkRandField = AccessTools.Field(typeof(GenCaves), "chunkRand");
            LCGRandom chunkRand = (LCGRandom)chunkRandField.GetValue(__instance);

            int quantityCaves = ((double)chunkRand.NextInt(100) < TerraGenConfig.CavesPerChunkColumn * 100.0) ? depthBasedCaveCount : 0;
            int rndSize = 1024 * (worldheight - 20);

            while(quantityCaves-- > 0)
            {
                int rnd = chunkRand.NextInt(rndSize);
                int posX = cdx * 32 + rnd % 32;
                rnd /= 32;
                int posZ = cdz * 32 + rnd % 32;
                rnd /= 32;
                int posY = rnd + 8;
                float horAngle = chunkRand.NextFloat() * 6.2831855f;
                float vertAngle = (chunkRand.NextFloat() - 0.5f) * 0.25f;

                float horizontalSize = (chunkRand.NextFloat() * 2f + chunkRand.NextFloat()) * TunnelHorizontalSizeMultiplier;
                float verticalSize = (0.75f + chunkRand.NextFloat() * 0.4f) * TunnelVerticalSizeMultiplier;
                rnd = chunkRand.NextInt(500000000);

                if(rnd % 100 < 4)
                {
                    horizontalSize = (chunkRand.NextFloat() * 2f + chunkRand.NextFloat() + chunkRand.NextFloat()) * TunnelHorizontalSizeMultiplier;
                    verticalSize = (0.25f + chunkRand.NextFloat() * 0.2f) * TunnelVerticalSizeMultiplier;
                }
                else if(rnd % 100 == 4)
                {
                    horizontalSize = (0.75f + chunkRand.NextFloat()) * TunnelHorizontalSizeMultiplier;
                    verticalSize = (chunkRand.NextFloat() * 2f + chunkRand.NextFloat()) * TunnelVerticalSizeMultiplier;
                }

                rnd /= 100;
                bool extraBranchy = posY < TerraGenConfig.seaLevel / 2 && rnd % 50 == 0;
                bool depthIncreasedBranching = posY < TerraGenConfig.seaLevel && rnd % (int)(100 - (posY / worldheight * 50)) == 0;
                rnd /= 50;
                int rnd2 = rnd % 1000;
                rnd /= 1000;
                bool largeNearLavaLayer = rnd2 % 10 < 3;

                float curveMult = TunnelCurvinessMultiplier;
                float curviness = (rnd == 0) ? 0.035f * curveMult : ((rnd2 < 30) ? 0.5f * curveMult : 0.1f * curveMult);

                int maxIterations = ChunkRange * 32 - 16;
                maxIterations -= chunkRand.NextInt(maxIterations / 4);
                caveRand.SetWorldSeed((ulong)chunkRand.NextInt(10000000));
                caveRand.InitPositionSeed(chunkX + cdx, chunkZ + cdz);
                CarveTunnel(__instance, chunks, chunkX, chunkZ, (double)posX, (double)posY, (double)posZ, horAngle, vertAngle, horizontalSize, verticalSize, 0, maxIterations, 0, extraBranchy || depthIncreasedBranching, curviness, largeNearLavaLayer);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "initWorldGen")]
        public static bool initWorldGen(GenCaves __instance)
        {
            caveRand = new MTRandom((ulong)(ServerAPI.WorldManager.Seed + 123128));
            basaltNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 0.2857142984867096, 0.8999999761581421, (long)(ServerAPI.World.Seed + 12));
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCaves), "CarveTunnel")]
        public static bool CarveTunnel(GenCaves __instance, IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int currentIteration, int maxIterations, int branchLevel, bool extraBranchy = false, float curviness = 0.1f, bool largeNearLavaLayer = false)
        {
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            float horAngleChange = 0f;
            float vertAngleChange = 0f;
            float horRadiusGain = 0f;
            float horRadiusLoss = 0f;
            float horRadiusGainAccum = 0f;
            float horRadiusLossAccum = 0f;
            float verHeightGain = 0f;
            float verHeightLoss = 0f;
            float verHeightGainAccum = 0f;
            float verHeightLossAccum = 0f;
            float sizeChangeSpeedAccum = 0.15f;
            float sizeChangeSpeedGain = 0f;
            int branchRand = (branchLevel + 1) * (extraBranchy ? 12 : 25);

            while(currentIteration++ < maxIterations)
            {
                float relPos = (float)currentIteration / (float)maxIterations;
                float horRadius = 1.5f + GameMath.FastSin(relPos * 3.1415927f) * horizontalSize + horRadiusGainAccum;
                horRadius = Math.Min(horRadius, Math.Max(1f, horRadius - horRadiusLossAccum));
                float vertRadius = 1.5f + GameMath.FastSin(relPos * 3.1415927f) * (verticalSize + horRadiusLossAccum / 4f) + verHeightGainAccum;
                vertRadius = Math.Min(vertRadius, Math.Max(0.6f, vertRadius - verHeightLossAccum));

                // Use an extreme shrink factor to try and quickly close the cave at lower levels... - Tim
                // (So we don't have cases where the tunnels are cut off to reveal a completely flat basalt floor)
                if(posY <= 16)
                {
                    float factor = Math.Max((float)Math.Pow((posY - 12) / 4f, 5), 0.01f);
                    horRadius *= factor;
                    vertRadius *= factor;
                }

                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);

                if(largeNearLavaLayer)
                {
                    float factor = 1f + Math.Max(0f, 1f - (float)Math.Abs(posY - 12.0) / 10f);
                    horRadius *= factor;
                    vertRadius *= factor;
                }

                if(vertRadius < 1f)
                {
                    vertAngle *= 0.1f;
                }

                posX += (double)(GameMath.FastCos(horAngle) * advanceHor);
                posY += (double)GameMath.Clamp(advanceVer, -vertRadius, vertRadius);
                posZ += (double)(GameMath.FastSin(horAngle) * advanceHor);
                vertAngle *= 0.8f;

                int rrnd = caveRand.NextInt(800000);

                if(rrnd / 10000 == 0)
                {
                    sizeChangeSpeedGain = caveRand.NextFloat() * caveRand.NextFloat() / 2f;
                }

                bool genHotSpring = false;
                int rnd = rrnd % 10000;

                if((rnd -= 30) <= 0)
                {
                    horAngle = caveRand.NextFloat() * 6.2831855f;
                }
                else if((rnd -= 76) <= 0)
                {
                    horAngle += caveRand.NextFloat() * 3.1415927f - 1.5707964f;
                }
                else if((rnd -= 60) <= 0)
                {
                    horRadiusGain = caveRand.NextFloat() * caveRand.NextFloat() * 3.5f;
                }
                else if((rnd -= 60) <= 0)
                {
                    horRadiusLoss = caveRand.NextFloat() * caveRand.NextFloat() * 10f;
                }
                else if((rnd -= 50) <= 0)
                {
                    if(posY < (double)(TerraGenConfig.seaLevel - 10))
                    {
                        verHeightLoss = caveRand.NextFloat() * caveRand.NextFloat() * 12f;
                        horRadiusGain = Math.Max(horRadiusGain, caveRand.NextFloat() * caveRand.NextFloat() * 3f);
                    }
                }
                else if((rnd -= 9) <= 0)
                {
                    if(posY < (double)(TerraGenConfig.seaLevel - 20))
                    {
                        horRadiusGain = 1f + caveRand.NextFloat() * caveRand.NextFloat() * 5f;
                    }
                }
                else if((rnd -= 9) <= 0)
                {
                    verHeightGain = 2f + caveRand.NextFloat() * caveRand.NextFloat() * 7f;
                }
                else if(rnd - 100 <= 0 && posY < 19.0)
                {
                    verHeightGain = 2f + caveRand.NextFloat() * caveRand.NextFloat() * 5f;
                    horRadiusGain = 4f + caveRand.NextFloat() * caveRand.NextFloat() * 9f;
                }

                if(posY > -5.0 && posY < 16.0 && horRadius > 4f && vertRadius > 2f)
                {
                    genHotSpring = true;
                }

                sizeChangeSpeedAccum = Math.Max(0.1f, sizeChangeSpeedAccum + sizeChangeSpeedGain * 0.05f);
                sizeChangeSpeedGain -= 0.02f;
                horRadiusGainAccum = Math.Max(0f, horRadiusGainAccum + horRadiusGain * sizeChangeSpeedAccum);
                horRadiusGain -= 0.45f;
                horRadiusLossAccum = Math.Max(0f, horRadiusLossAccum + horRadiusLoss * sizeChangeSpeedAccum);
                horRadiusLoss -= 0.4f;
                verHeightGainAccum = Math.Max(0f, verHeightGainAccum + verHeightGain * sizeChangeSpeedAccum);
                verHeightGain -= 0.45f;
                verHeightLossAccum = Math.Max(0f, verHeightLossAccum + verHeightLoss * sizeChangeSpeedAccum);
                verHeightLoss -= 0.4f;
                horAngle += curviness * horAngleChange;
                vertAngle += curviness * vertAngleChange;
                vertAngleChange = 0.9f * vertAngleChange + caveRand.NextFloatMinusToPlusOne() * caveRand.NextFloat() * 3f;
                horAngleChange = 0.9f * horAngleChange + caveRand.NextFloatMinusToPlusOne() * caveRand.NextFloat();

                if(rrnd % 140 == 0)
                {
                    horAngleChange *= caveRand.NextFloat() * 6f;
                }

                int brand = branchRand + 1 * Math.Max(0, (int)posY - (TerraGenConfig.seaLevel - 20));

                if(branchLevel < 3 && (vertRadius > 1f || horRadius > 1f) && caveRand.NextInt(brand) == 0)
                {
                    CarveTunnel(__instance, chunks, chunkX, chunkZ, posX, posY + (double)(verHeightGainAccum / 2f), posZ, horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1f) + 3.1415927f, vertAngle + (caveRand.NextFloat() - 0.5f) * (caveRand.NextFloat() - 0.5f), horizontalSize, verticalSize + verHeightGainAccum, currentIteration, maxIterations - (int)((double)caveRand.NextFloat() * 0.5 * (double)maxIterations), branchLevel + 1, false, 0.1f, false);
                }

                if(CreateShafts)
                {
                    if(branchLevel < 1 && horRadius > 3f && posY > 60.0 && caveRand.NextInt(60) == 0)
                    {
                        CarveShaft(__instance, chunks, chunkX, chunkZ, posX, posY + (double)(verHeightGainAccum / 2f), posZ, horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1f) + 3.1415927f, -1.6707964f + 0.2f * caveRand.NextFloat(), Math.Min(3.5f, horRadius - 1f), verticalSize + verHeightGainAccum, currentIteration, maxIterations - (int)((double)caveRand.NextFloat() * 0.5 * (double)maxIterations) + (int)(posY / 5.0 * (double)(0.5f + 0.5f * caveRand.NextFloat())), branchLevel);
                        branchLevel++;
                    }
                }

                if((horRadius < 2f || rrnd % 5 != 0) && posX > (double)(-(double)horRadius * 2f) && posX < (double)(32f + horRadius * 2f) && posZ > (double)(-(double)horRadius * 2f) && posZ < (double)(32f + horRadius * 2f))
                {
                    SetBlocks(__instance, chunks, horRadius, vertRadius + verHeightGainAccum, posX, posY + (double)(verHeightGainAccum / 2f), posZ, terrainheightmap, rainheightmap, chunkX, chunkZ, genHotSpring);
                }
            }

            return false;
        }

        // (Overridden to 5 in GenCaves class)... - Tim
        public static int ChunkRange { get { return 5; } }
    }
}
