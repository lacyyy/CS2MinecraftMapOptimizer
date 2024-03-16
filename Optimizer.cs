using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using KeyValues2Parser;
using KeyValues2Parser.Constants;
using KeyValues2Parser.Models;
using KeyValues2Parser.ParsingKV2;

namespace CS2MinecraftMapOptimizer
{
    public static class Optimizer
    {
        public const string NAME_PREFIX = "mc_opti_";
        public const string NAME_WITH_WILDCARD = "mc_opti_*"; // Targets all entities with this name prefix


        // ---- DATA FROM _ADDING_ OPTIMIZATIONS TO THE MAP ----

        public static float blockSize = 0.0f;
        public static bool  enableOceanOpti = false;
        public static float oceanZCoord = 0.0f;

        public static string logicAutoOutputs = "";

        // ---- DATA FROM _REMOVING_ OPTIMIZATIONS FROM THE MAP ----

        // ...

        // ----

        // Returns true if successful, false if something went wrong
        public static bool AddOptimizations(string gameDir, string vmapPath, float blockSize, bool enableOceanOpti, float oceanZCoord)
        {
            Optimizer.blockSize = blockSize;
            Optimizer.enableOceanOpti = enableOceanOpti;
            Optimizer.oceanZCoord = oceanZCoord;

            // -game is the path to your '...\game\csgo' folder.
            //       Eg: 'C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo'
            // -vmapFilepath is the path to the vmap file that you are parsing
            string[] args = { "-game", gameDir, "-vmapFilepath", vmapPath };
            ParsedVMapData? parsedVMapData = GetVmapRequiredData(args);
            if (parsedVMapData is null)
                return false; // Failure

            //List<VBlock> allWorldMeshes = parsedVMapData.VMapContents.AllWorldMeshes;
            List<VBlock> allEntities    = parsedVMapData.VMapContents.AllEntities;

            if (DoesMapAlreadyContainOptimizations(allEntities))
            {
                Console.WriteLine("ERROR: Unable to add optimizations, since the map has already had optimizations added! " +
                    "You need to remove optimizations first before re-adding optimizations again!");
                return false; // Failure
            }

            Console.WriteLine();
            Console.WriteLine("Getting all minecraft blocks...");

            List<MCBlock> blocks = ParseUnnamedMinecraftBlocks(allEntities);
            ComputeBlockNeighbors(blocks);
            ComputeHiddenOnRoundStart(blocks); // Must be called after ComputeBlockNeighbors()

            // Must be called after ComputeHiddenOnRoundStart()
            logicAutoOutputs = GenerateLogicAutoOutputs(blocks);

            // Must be called after ComputeBlockNeighbors() and ComputeHiddenOnRoundStart()
            ComputeOnBreakOutputs(blocks);

            // Collect statistics
            int cntHiddenBlocks = 0;
            foreach (MCBlock block in blocks)
                if (block.hiddenOnRoundStart)
                    cntHiddenBlocks++;
            Console.WriteLine("{0} of {1} blocks are hidden blocks", cntHiddenBlocks, blocks.Count);

            // Delete parsed VMAP data to minimize RAM usage
            parsedVMapData = null; // Note: This didn't have the effect I hoped for.


            // KeyValues2Parser v1.1.5 decodes the VMAP file to a '.txt' file at this path.
            // If KeyValues2Parser changes that path, this needs to be updated accordingly!
            string decodedVmapFile = GameConfigurationValues.vmapFilepathDirectory + "\\" + Path.GetFileName(vmapPath) + ".txt";

            // Must be called after ComputeOnBreakOutputs()
            WriteOptimizationAdditionsToVmap(blocks, decodedVmapFile, vmapPath); // CAUTION, this can fail, not checked

            // TODO Error indication?

            return true; // Success
        }

        public static void RemoveOptimizations(string gameDir, string vmapPath)
        {
            // Note: We only parse the VMAP here to let KeyValues2Parse decode the VMAP file for us.

            // -game is the path to your '...\game\csgo' folder.
            //       Eg: 'C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo'
            // -vmapFilepath is the path to the vmap file that you are parsing
            string[] args = { "-game", gameDir, "-vmapFilepath", vmapPath };
            GetVmapRequiredData(args);

            // KeyValues2Parser v1.1.5 decodes the VMAP file to a '.txt' file at this path.
            // If KeyValues2Parser changes that path, this needs to be updated accordingly!
            string decodedVmapFile = GameConfigurationValues.vmapFilepathDirectory + "\\" + Path.GetFileName(vmapPath) + ".txt";

            WriteOptimizationRemovalToVmap(decodedVmapFile, vmapPath);

            // TODO Error indication?

        }

        private static ParsedVMapData? GetVmapRequiredData(string[] args)
        {
            var parsedVMapData = ParsedVMapDataGatherer.GetParsedVMapData(args);

            // Some code from JimWood

            //List<VBlock> allWorldMeshes    = parsedVMapData.VMapContents.AllWorldMeshes;
            //List<VBlock> allEntities       = parsedVMapData.VMapContents.AllEntities;
            //List<VBlock> allInstanceGroups = parsedVMapData.VMapContents.AllInstanceGroups;
            //List<VBlock> allInstances      = parsedVMapData.VMapContents.AllInstances;
            //List<VBlock> allPrefabs        = parsedVMapData.VMapContents.AllPrefabs;

            //Console.WriteLine();
            //Console.WriteLine("Getting required data from the main vmap and prefabs...");

            //// prefabs contents
            //if (ConfigurationSorter.prefabEntityIdsByVmap != null && ConfigurationSorter.prefabEntityIdsByVmap.Any())
            //{
            //    foreach (var prefabEntityIdByVmap in ConfigurationSorter.prefabEntityIdsByVmap)
            //    {
            //        var prefabEntityId = prefabEntityIdByVmap.Value;
            //        var prefab = prefabEntityIdByVmap.Key;

            //        AllEntitiesAndHiddenEntityMeshIdsInSpecificVmap allEntitiesAndHiddenEntityMeshIdsInPrefab = ConfigurationSorter.GetAllEntitiesInSpecificVmap(prefab, ConfigurationSorter.hiddenElementIdsInMainVmap) ?? new(prefab.MapName);
            //        List<VBlock> allWorldMeshesInPrefab = ConfigurationSorter.GetAllWorldMeshesInSpecificVmap(prefab, ConfigurationSorter.hiddenElementIdsInMainVmap, allEntitiesAndHiddenEntityMeshIdsInPrefab.hiddenEntityMeshIds);
            //        //List<VBlock> allMeshEntitiesInPrefab = ConfigurationSorter.GetAllMeshEntitiesInListOfEntities(allEntitiesAndHiddenEntityMeshIdsInPrefab.allEntities, ConfigurationSorter.hiddenElementIdsInMainVmap);
            //        List<VBlock> allInstanceGroupsInPrefab = ConfigurationSorter.GetAllInstanceGroupsInSpecificVmap(prefab, ConfigurationSorter.hiddenElementIdsInMainVmap);
            //        List<VBlock> allInstancesInPrefab = ConfigurationSorter.GetAllInstancesInSpecificVmap(prefab, ConfigurationSorter.hiddenElementIdsInMainVmap);

            //        // change the ids
            //        foreach (var entity in allEntitiesAndHiddenEntityMeshIdsInPrefab.allEntities)
            //        {
            //            var oldEntityId = Guid.Parse(entity.Variables["id"]);
            //            var newEntityId = Guid.NewGuid();

            //            // replaces the Id in the entity itself
            //            entity.Variables.Remove("id");
            //            entity.Variables.Add("id", newEntityId.ToString());

            //            // replaces the Id that the selection set contains, to ensure that it points to the new Id instead
            //            foreach (var selectionSet in parsedVMapData.SelectionSetsInPrefabByPrefabEntityId[prefabEntityIdByVmap.Value].GetAllInList())
            //            {
            //                if (selectionSet == null)
            //                    continue;

            //                if (selectionSet.SelectedObjectIds.Any(x => x.Equals(oldEntityId)))
            //                {
            //                    selectionSet.SelectedObjectIds?.RemoveAll(x => x.Equals(oldEntityId));
            //                    selectionSet.SelectedObjectIds?.Add(newEntityId);
            //                }

            //                if (selectionSet.MeshIds.Any(x => x.Equals(oldEntityId))) // this might break face selection sets
            //                {
            //                    selectionSet.MeshIds?.RemoveAll(x => x.Equals(oldEntityId));
            //                    selectionSet.MeshIds?.Add(newEntityId);
            //                }
            //            }

            //            // the meshes inside the entity
            //            var entityMeshes = entity.Arrays.Where(x => x.Id == "children" && x.InnerBlocks != null)?
            //                .SelectMany(x => x.InnerBlocks.Where(y => y.Id == "CMapMesh" && y.InnerBlocks != null));

            //            foreach (var entityMesh in entityMeshes)
            //            {
            //                var oldEntityMeshId = Guid.Parse(entityMesh.Variables["id"]);
            //                var newEntityMeshId = Guid.NewGuid();

            //                // replaces the Id in the entity mesh itself
            //                entityMesh.Variables.Remove("id");
            //                entityMesh.Variables.Add("id", newEntityMeshId.ToString());

            //                // replaces the Id that the selection set contains, to ensure that it points to the new Id instead
            //                foreach (var selectionSet in parsedVMapData.SelectionSetsInPrefabByPrefabEntityId[prefabEntityIdByVmap.Value].GetAllInList())
            //                {
            //                    if (selectionSet == null)
            //                        continue;

            //                    if (selectionSet.SelectedObjectIds.Any(x => x.Equals(oldEntityMeshId)))
            //                    {
            //                        selectionSet.SelectedObjectIds?.RemoveAll(x => x.Equals(oldEntityMeshId));
            //                        selectionSet.SelectedObjectIds?.Add(newEntityMeshId);
            //                    }

            //                    if (selectionSet.MeshIds.Any(x => x.Equals(oldEntityMeshId))) // this might break face selection sets
            //                    {
            //                        selectionSet.MeshIds?.RemoveAll(x => x.Equals(oldEntityMeshId));
            //                        selectionSet.MeshIds?.Add(newEntityMeshId);
            //                    }
            //                }
            //            }
            //            //
            //        }
            //        foreach (var mesh in allWorldMeshesInPrefab)
            //        {
            //            var oldMeshId = Guid.Parse(mesh.Variables["id"]);
            //            var newMeshId = Guid.NewGuid();

            //            // replaces the Id in the mesh itself
            //            mesh.Variables.Remove("id");
            //            mesh.Variables.Add("id", newMeshId.ToString());

            //            // replaces the Id that the selection set contains, to ensure that it points to the new Id instead
            //            foreach (var selectionSet in parsedVMapData.SelectionSetsInPrefabByPrefabEntityId[prefabEntityIdByVmap.Value].GetAllInList())
            //            {
            //                if (selectionSet == null)
            //                    continue;

            //                if (selectionSet.SelectedObjectIds.Any(x => x.Equals(oldMeshId)))
            //                {
            //                    selectionSet.SelectedObjectIds?.RemoveAll(x => x.Equals(oldMeshId));
            //                    selectionSet.SelectedObjectIds?.Add(newMeshId);
            //                }

            //                if (selectionSet.MeshIds.Any(x => x.Equals(oldMeshId))) // this might break face selection sets
            //                {
            //                    selectionSet.MeshIds?.RemoveAll(x => x.Equals(oldMeshId));
            //                    selectionSet.MeshIds?.Add(newMeshId);
            //                }
            //            }
            //        }
            //        foreach (var instanceGroup in allInstanceGroupsInPrefab)
            //        {
            //            var oldInstanceGroupId = Guid.Parse(instanceGroup.Variables["id"]);
            //            var newInstanceGroupId = Guid.NewGuid();

            //            // replaces the Id in the instanceGroup itself
            //            instanceGroup.Variables.Remove("id");
            //            instanceGroup.Variables.Add("id", newInstanceGroupId.ToString());

            //            // replaces the Id that the selection set contains, to ensure that it points to the new Id instead
            //            foreach (var selectionSet in parsedVMapData.SelectionSetsInPrefabByPrefabEntityId[prefabEntityIdByVmap.Value].GetAllInList())
            //            {
            //                if (selectionSet == null)
            //                    continue;

            //                if (selectionSet.SelectedObjectIds.Any(x => x.Equals(oldInstanceGroupId)))
            //                {
            //                    selectionSet.SelectedObjectIds?.RemoveAll(x => x.Equals(oldInstanceGroupId));
            //                    selectionSet.SelectedObjectIds?.Add(newInstanceGroupId);
            //                }

            //                if (selectionSet.MeshIds.Any(x => x.Equals(oldInstanceGroupId))) // this might break face selection sets
            //                {
            //                    selectionSet.MeshIds?.RemoveAll(x => x.Equals(oldInstanceGroupId));
            //                    selectionSet.MeshIds?.Add(newInstanceGroupId);
            //                }
            //            }
            //        }
            //        foreach (var instance in allInstancesInPrefab)
            //        {
            //            var oldInstanceId = Guid.Parse(instance.Variables["id"]);
            //            var newInstanceId = Guid.NewGuid();

            //            // replaces the Id in the instance itself
            //            instance.Variables.Remove("id");
            //            instance.Variables.Add("id", newInstanceId.ToString());

            //            // replaces the Id that the selection set contains, to ensure that it points to the new Id instead
            //            foreach (var selectionSet in parsedVMapData.SelectionSetsInPrefabByPrefabEntityId[prefabEntityIdByVmap.Value].GetAllInList())
            //            {
            //                if (selectionSet == null)
            //                    continue;

            //                if (selectionSet.SelectedObjectIds.Any(x => x.Equals(oldInstanceId)))
            //                {
            //                    selectionSet.SelectedObjectIds?.RemoveAll(x => x.Equals(oldInstanceId));
            //                    selectionSet.SelectedObjectIds?.Add(newInstanceId);
            //                }

            //                if (selectionSet.MeshIds.Any(x => x.Equals(oldInstanceId))) // this might break face selection sets
            //                {
            //                    selectionSet.MeshIds?.RemoveAll(x => x.Equals(oldInstanceId));
            //                    selectionSet.MeshIds?.Add(newInstanceId);
            //                }
            //            }
            //        }

            //        allWorldMeshes.AddRange(allWorldMeshesInPrefab);
            //        allEntities.AddRange(allEntitiesAndHiddenEntityMeshIdsInPrefab.allEntities);
            //        allInstanceGroups.AddRange(allInstanceGroupsInPrefab);
            //        allInstances.AddRange(allInstancesInPrefab);
            //    }
            //}


            //var allWorldMeshesInExampleSelectionSet = GetAllVBlocksInCorrectSelectionSet(allWorldMeshes, allInstances, parsedVMapData.SelectionSetsInMainVmap.ExampleSelectionSet, parsedVMapData.SelectionSetsInPrefabByPrefabEntityId.Values.Select(x => x.ExampleSelectionSet));


            //// meshes
            //var allSelectionSetsInVmapAndAllPrefabs = parsedVMapData.SelectionSetsInMainVmap.GetAllInList().Concat(parsedVMapData.SelectionSetsInPrefabByPrefabEntityId.Values.SelectMany(x => x.GetAllInList())).ToList();
            //var allWorldMeshesInNoSpecificSelectionSet = GetAllMeshesInNoSpecificSelectionSet(allWorldMeshes, allSelectionSetsInVmapAndAllPrefabs);


            //// mesh entities
            //var buyzoneMeshEntities = ConfigurationSorter.GetEntitiesByClassname(allEntities, Classnames.Buyzone);


            //// point entities
            //var hostageEntities = ConfigurationSorter.GetEntitiesByClassnameInSelectionSetList(allEntities, Classnames.HostageList, parsedVMapData.SelectionSetsInMainVmap, parsedVMapData.SelectionSetsInPrefabByPrefabEntityId);


            //// props
            //var allEntitiesInExampleSelectionSet = ConfigurationSorter.GetEntitiesInSpecificSelectionSet(allEntities, SelectionSetNames.ExampleSelectionSetName, parsedVMapData.SelectionSetsInMainVmap, parsedVMapData.SelectionSetsInPrefabByPrefabEntityId);

            //Console.WriteLine("Finished getting required data from the main vmap and prefabs");

            return parsedVMapData;
        }

        private static List<VBlock> GetAllVBlocksInCorrectSelectionSet(List<VBlock> vBlocksSearchingThrough, List<VBlock> allInstances, VSelectionSet selectionSetInMainVmap, IEnumerable<VSelectionSet> selectionSetsInEachPrefab)
        {
            return
                (from x in vBlocksSearchingThrough
                 where (selectionSetInMainVmap != null &&
                     (selectionSetInMainVmap.SelectedObjectIds.Any(y => y.Equals(Guid.Parse(x.Variables.First(z => z.Key == "id").Value))) ||
                         selectionSetInMainVmap.MeshIds.Any(y => y.Equals(Guid.Parse(x.Variables.First(z => z.Key == "id").Value))) ||
                         selectionSetInMainVmap.SelectedObjectIds.Any(y => allInstances.Any(z => Guid.Parse(z.Variables.First(z => z.Key == "id").Value).Equals(y))) ||
                         selectionSetInMainVmap.MeshIds.Any(y => allInstances.Any(z => Guid.Parse(z.Variables.First(z => z.Key == "id").Value).Equals(y))))) ||
                 selectionSetsInEachPrefab.Any(y =>
                     y != null &&
                         (y.SelectedObjectIds.Any(z => z.Equals(Guid.Parse(x.Variables.First(z => z.Key == "id").Value))) ||
                         y.MeshIds.Any(z => z.Equals(Guid.Parse(x.Variables.First(z => z.Key == "id").Value))) ||
                         y.SelectedObjectIds.Any(y => allInstances.Any(z => Guid.Parse(z.Variables.First(z => z.Key == "id").Value).Equals(y))) ||
                         y.MeshIds.Any(y => allInstances.Any(z => Guid.Parse(z.Variables.First(z => z.Key == "id").Value).Equals(y)))))
                 select x).Distinct().ToList()
                ?? new List<VBlock>();
        }


        private static List<VBlock> GetAllMeshesInNoSpecificSelectionSet(
            List<VBlock> allWorldMeshesInVmap,
            List<VSelectionSet> selectionSetsToExclude)
        {
            selectionSetsToExclude.RemoveAll(x => x == null);

            if (selectionSetsToExclude != null && selectionSetsToExclude.Any())
            {
                foreach (var selectionSetToExclude in selectionSetsToExclude)
                {
                    allWorldMeshesInVmap.RemoveAll(x => selectionSetToExclude.SelectedObjectIds.Any(y => y.Equals(x.Id)));
                }
            }

            return allWorldMeshesInVmap ?? new();
        }


        private static IEnumerable<VBlock> GetMeshesByTextureName(
            IEnumerable<VBlock> allWorldMeshesInVmap,
            string textureName,
            IEnumerable<VBlock> allWorldMeshesInSpecificSelectionSet = null)
        {
            var allWorldMeshes = (from x in allWorldMeshesInVmap
                                  from y in x.InnerBlocks
                                  where y.Id == "meshData"
                                  from z in y.Arrays
                                  where z.Id == "materials"
                                  from a in z.AllLinesInArrayByLineSplit
                                  where a.ToLower().Replace("materials/", string.Empty).Replace(".vmat", string.Empty) == textureName.ToLower()
                                  select x).Distinct().ToList();

            if (allWorldMeshesInSpecificSelectionSet != null && allWorldMeshesInSpecificSelectionSet.Any())
                allWorldMeshes.AddRange(allWorldMeshesInSpecificSelectionSet);

            allWorldMeshes = allWorldMeshes.Distinct().ToList();

            return allWorldMeshes ?? new();
        }


        private static IEnumerable<VBlock> GetMeshEntityMeshesByTextureName(
            IEnumerable<VBlock> allMeshEntities,
            string textureName,
            IEnumerable<VBlock> allMeshEntityMeshesInSpecificSelectionSet = null)
        {
            var allMeshEntityMeshes = (from x in allMeshEntities
                                       from y in x.InnerBlocks
                                       where y.Id == "entity_properties"
                                       from y2 in x.Arrays
                                       where y2.Id == "children"
                                       from z2 in y2.InnerBlocks
                                       where z2.Id == "CMapMesh"
                                       from a2 in z2.InnerBlocks
                                       where a2.Id == "meshData"
                                       from b2 in a2.Arrays
                                       where b2.Id == "materials"
                                       from c2 in b2.AllLinesInArrayByLineSplitUnformatted
                                       where c2.ToLower().Replace("materials/", string.Empty).Replace(".vmat", string.Empty) == textureName.ToLower()
                                       select z2).Distinct().ToList();

            if (allMeshEntityMeshesInSpecificSelectionSet != null && allMeshEntityMeshesInSpecificSelectionSet.Any())
                allMeshEntityMeshes.AddRange(allMeshEntityMeshesInSpecificSelectionSet);

            allMeshEntityMeshes = allMeshEntityMeshes.Distinct().ToList();

            return allMeshEntityMeshes ?? new();
        }


        private static IEnumerable<VBlock> GetMeshEntityMeshesByClassname(IEnumerable<VBlock> allMeshEntities, string classname)
        {
            return (from x in allMeshEntities
                    from y in x.InnerBlocks
                    where y.Id == "entity_properties"
                    where y.Variables.Any(z => z.Key == "classname" && z.Value.ToLower() == classname.ToLower())
                    from y2 in x.Arrays
                    where y2.Id == "children"
                    from z2 in y2.InnerBlocks
                    where z2.Id == "CMapMesh"
                    select z2).Distinct() ?? new List<VBlock>();
        }

        // Parse 3 space-separated floats
        private static Vector3 ParseVector3(string s)
        {
            string[] subs = s.Split(' ');
            return new Vector3(
                float.Parse(subs[0], CultureInfo.InvariantCulture.NumberFormat),
                float.Parse(subs[1], CultureInfo.InvariantCulture.NumberFormat),
                float.Parse(subs[2], CultureInfo.InvariantCulture.NumberFormat)
            );
        }

        private static bool DoesMapAlreadyContainOptimizations(List<VBlock> allEntities)
        {
            var func_breakables = ConfigurationSorter.GetEntitiesByClassname(allEntities, "func_breakable");
            foreach (var fb in func_breakables)
            {
                VBlock ent_properties = (from y in fb.InnerBlocks where y.Id == "entity_properties" select y).First();
                string targetname = ent_properties.Variables["targetname"];
                if (targetname.StartsWith(NAME_PREFIX))
                    return true;

                VArray connectionsData = (from y in fb.Arrays where y.Id == "connectionsData" select y).First();
                foreach (VBlock connection in connectionsData.InnerBlocks)
                    if (connection.Variables["targetName"].StartsWith(NAME_PREFIX))
                        return true;
            }
            return false;
        }

        private static List<MCBlock> ParseUnnamedMinecraftBlocks(List<VBlock> allEntities)
        {
            List<MCBlock> blocks = new List<MCBlock>();

            int cnt_collectedBlocks = 0;

            var func_breakables = ConfigurationSorter.GetEntitiesByClassname(allEntities, "func_breakable");
            foreach (var fb in func_breakables)
            {
                string ent_id = fb.Variables["id"];
                VBlock ent_properties = (from y in fb.InnerBlocks where y.Id == "entity_properties" select y).First();
                string ent_properties_id = ent_properties.Variables["id"];
                string targetname = ent_properties.Variables["targetname"];
                Vector3 origin = ParseVector3(fb.Variables["origin"]);
                Vector3 angles = ParseVector3(fb.Variables["angles"]);
                Vector3 scales = ParseVector3(fb.Variables["scales"]);
                // TODO check that angles are (0 0 0) and scales are (1 1 1) ?

                // Filter out named entities
                if (targetname.Length != 0)
                    continue;

                // Generate unique name for this block
                string new_targetname = NAME_PREFIX + cnt_collectedBlocks;
                cnt_collectedBlocks++;

                blocks.Add(new MCBlock {
                    targetname = new_targetname,
                    origin = origin,
                    angles = angles,
                    scales = scales,
                    id = ent_id,
                    entity_properties_id = ent_properties_id
                });
            }
            return blocks;
        }

        public static Vector3 GetNeighborDir(int neighbor_id)
        {
            switch(neighbor_id)
            {
                case (int)MCBlock.Neighbor.POS_X: return new Vector3(+1.0f,  0.0f,  0.0f);
                case (int)MCBlock.Neighbor.POS_Y: return new Vector3( 0.0f, +1.0f,  0.0f);
                case (int)MCBlock.Neighbor.POS_Z: return new Vector3( 0.0f,  0.0f, +1.0f);
                case (int)MCBlock.Neighbor.NEG_X: return new Vector3(-1.0f,  0.0f,  0.0f);
                case (int)MCBlock.Neighbor.NEG_Y: return new Vector3( 0.0f, -1.0f,  0.0f);
                case (int)MCBlock.Neighbor.NEG_Z: return new Vector3( 0.0f,  0.0f, -1.0f);
            }
            return new Vector3(0, 0, 0); // Invalid neighbor
        }

        public static int GetOppositeNeighborId(int neighbor_id)
        {
            switch (neighbor_id)
            {
                case (int)MCBlock.Neighbor.POS_X: return (int)MCBlock.Neighbor.NEG_X;
                case (int)MCBlock.Neighbor.POS_Y: return (int)MCBlock.Neighbor.NEG_Y;
                case (int)MCBlock.Neighbor.POS_Z: return (int)MCBlock.Neighbor.NEG_Z;
                case (int)MCBlock.Neighbor.NEG_X: return (int)MCBlock.Neighbor.POS_X;
                case (int)MCBlock.Neighbor.NEG_Y: return (int)MCBlock.Neighbor.POS_Y;
                case (int)MCBlock.Neighbor.NEG_Z: return (int)MCBlock.Neighbor.POS_Z;
            }
            return -1; // Invalid neighbor
        }

        public static void ComputeBlockNeighbors(List<MCBlock> blocks)
        {
            const float EPSILON = 1e-3f;
            const float EPSILON_SQR = EPSILON * EPSILON;

            Console.WriteLine("Computing block neighbours...");
            Console.WriteLine(blocks.Count);


            foreach (MCBlock block in blocks)
            {
                for (int dir_id = 0; dir_id < 6; dir_id++)
                {
                    // Skip direction if it was already computed
                    if (block.neighbors[dir_id] is not null)
                        continue;

                    Vector3 neighborDir = GetNeighborDir(dir_id);
                    Vector3 neighborPos = block.origin + blockSize * neighborDir;

                    // Check if the neighbor in this direction exists
                    foreach (MCBlock other_block in blocks)
                    {
                        float deviation_sqr = Vector3.DistanceSquared(other_block.origin, neighborPos);
                        if (deviation_sqr > EPSILON_SQR)
                            continue;

                        // Neighbour found! Remember neighbor relation both ways.
                        block.neighbors[dir_id] = other_block;
                        int opposite_dir_id = GetOppositeNeighborId(dir_id);
                        other_block.neighbors[opposite_dir_id] = block;
                        break;
                    }
                }
            }
            Console.WriteLine("DONE computing block neighbours.");
        }

        public static void ComputeHiddenOnRoundStart(List<MCBlock> blocks)
        {
            foreach (MCBlock block in blocks)
            {
                bool hasAllNeighbors =
                    (block.neighbors[(int)MCBlock.Neighbor.POS_X] is not null) &&
                    (block.neighbors[(int)MCBlock.Neighbor.POS_Y] is not null) &&
                    (block.neighbors[(int)MCBlock.Neighbor.POS_Z] is not null) &&
                    (block.neighbors[(int)MCBlock.Neighbor.NEG_X] is not null) &&
                    (block.neighbors[(int)MCBlock.Neighbor.NEG_Y] is not null) &&
                    (block.neighbors[(int)MCBlock.Neighbor.NEG_Z] is not null);

                if (hasAllNeighbors)
                {
                    block.hiddenOnRoundStart = true;
                }
                else if (Optimizer.enableOceanOpti)
                {
                    bool hasAllNeighborsExceptBelow =
                        (block.neighbors[(int)MCBlock.Neighbor.POS_X] is not null) &&
                        (block.neighbors[(int)MCBlock.Neighbor.POS_Y] is not null) &&
                        (block.neighbors[(int)MCBlock.Neighbor.POS_Z] is not null) &&
                        (block.neighbors[(int)MCBlock.Neighbor.NEG_X] is not null) &&
                        (block.neighbors[(int)MCBlock.Neighbor.NEG_Y] is not null) &&
                        (block.neighbors[(int)MCBlock.Neighbor.NEG_Z] is null);

                    float tolerance = 10.0f;
                    bool isBlockInsideOcean =
                        (block.origin.Z - 0.5f * Optimizer.blockSize - tolerance) < Optimizer.oceanZCoord;

                    if (hasAllNeighborsExceptBelow && isBlockInsideOcean)
                        block.hiddenOnRoundStart = true;
                }
            }
        }

        public static void ComputeOnBreakOutputs(List<MCBlock> blocks)
        {
            foreach (MCBlock block in blocks)
            {
                List<List<string>> onBreakOutputs = new List<List<string>>();
                for (int dir_id = 0; dir_id < 6; dir_id++)
                {
                    if (block.neighbors[dir_id] is null)
                        continue;

                    MCBlock neighbor = block.neighbors[dir_id];
                    if (!neighbor.hiddenOnRoundStart)
                        continue; // Only generate outputs towards hidden neighbors

                    // Generate OnBreak output from this block to this neighbor.
                    string new_guid = Guid.NewGuid().ToString();
                    List<string> onBreakOutput = [ // Lines of KV2 element
                        "\"DmeConnectionData\"",
                        "{",
                        "\t\"id\" \"elementid\" \"" + new_guid + "\"",
                        "\t\"outputName\" \"string\" \"OnBreak\"",
                        "\t\"targetType\" \"int\" \"7\"",
                        "\t\"targetName\" \"string\" \"" + neighbor.targetname + "\"",
                        "\t\"inputName\" \"string\" \"Alpha\"",
                        "\t\"overrideParam\" \"string\" \"255\"",
                        "\t\"delay\" \"float\" \"0\"",
                        "\t\"timesToFire\" \"int\" \"1\"",
                        "}", // <-- Intentionally not including comma and line break here, VMAP inserter handles that
                    ];
                    onBreakOutputs.Add(onBreakOutput);
                }
                block.onBreakOutputs = onBreakOutputs;
            }
        }

        public static string GenerateLogicAutoOutputs(List<MCBlock> blocks)
        {
            string connections = "";

            connections +=
                "\t\t{\r\n" +
                "\t\t\tsourceEntity = \"\"\r\n" +
                "\t\t\toutput = \"OnMapSpawn\"\r\n" +
                "\t\t\ttargetEntity = \"" + NAME_WITH_WILDCARD + "\"\r\n" + // Target all hidden blocks (We generated a name for them)
                "\t\t\tinput = \"Alpha\"\r\n" +
                "\t\t\tparam = \"0\"\r\n" +
                "\t\t\tioTargetType = \"ENTITY_IO_TARGET_ENTITYNAME_OR_CLASSNAME\"\r\n" +
                "\t\t\tdelay = 0.0\r\n" +
                "\t\t\ttimesToFire = -1\r\n" +
                "\t\t\trelayConnection = false\r\n" +
                "\t\t\tfromGlobalRelay = false\r\n" +
                "\t\t},\r\n";

            string kv3_output =
                "<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} " +
                "format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->\r\n" +
                "{\r\n" +
                "\tconnections = \r\n" +
                "\t[\r\n" +
                connections +
                "\t]\r\n" +
                "}\r\n";
            return kv3_output;
        }

        // The srcVmapFile must be the decoded VMAP file that was parsed by KeyValues2Parser
        public static void WriteOptimizationAdditionsToVmap(List<MCBlock> blocks, string srcVmapFile, string destVmapFile)
        {
            try
            {
                Console.WriteLine("Writing optimization additions to the VMAP file...");

                const string ENTITY_BEGINNING = "\"CMapEntity\"";
                

                using (StreamReader sr = new StreamReader(srcVmapFile))
                using (StreamWriter sw = new StreamWriter(destVmapFile, false)) // Overwrite destination file
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.EndsWith(ENTITY_BEGINNING))
                        {
                            sw.WriteLine(line);

                            // Read all lines of this entity
                            int indentLevel = line.IndexOf(ENTITY_BEGINNING); // # of tab characters
                            string entityEnding = "";
                            for (int i = 0; i < indentLevel; i++)
                                entityEnding += "\t";
                            entityEnding += "}"; // Note: A comma could follow this

                            List<string> linesOfEntity = new List<string>();
                            while ((line = sr.ReadLine()) != null)
                            {
                                linesOfEntity.Add(line);
                                if (line.StartsWith(entityEnding))
                                {
                                    // We've collected all lines of this entity, now process them as a whole
                                    WriteOptimizationAdditionsToEntity(blocks, linesOfEntity);
                                    foreach (string entLine in linesOfEntity)
                                        sw.WriteLine(entLine);
                                    break;
                                }
                            }
                            continue; // Entity fully processed, go to next line
                        }

                        sw.WriteLine(line);
                    }
                }
                Console.WriteLine("DONE!");
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR: Failed to add optimizations to VMAP file:");
                Console.WriteLine(e.Message);
            }
            
        }

        // Process and potentially modify lines of a VMAP CMapEntity element
        public static void WriteOptimizationAdditionsToEntity(List<MCBlock> blocks, List<string> linesOfEntity)
        {
            // Note: linesOfEntity includes opening and closing curly brace lines.
            //       The closing curly brace line might have a trailing comma.

            const string ID_BEGINNING         = "\"id\" \"elementid\" \"";
            const string ENT_PROPS_BEGINNING  = "\"entity_properties\" \"EditGameClassProps\"";
            const string TARGETNAME_BEGINNING = "\"targetname\" \"string\" \"";
            const string CONNSDATA_BEGINNING  = "\"connectionsData\" \"element_array\"";

            // Determine indent level
            int indentLevel = linesOfEntity[0].IndexOf("{") + 1; // # of tab characters
            string indentation = "";
            for (int i = 0; i < indentLevel; i++)
                indentation += "\t";

            // Check if we encountered a minecraft block
            MCBlock? block = null;
            foreach (string line in linesOfEntity)
            {
                if (line.StartsWith(indentation + ID_BEGINNING))
                {
                    foreach (MCBlock b in blocks)
                        if (line.Equals(indentation + ID_BEGINNING + b.id + "\""))
                            block = b; // Match!
                    if (block is null)
                        return; // This entity is not a block, nothing to do here
                    break;
                }
            }

            // At this point we know this entity is a minecraft block.

            // Find location of "entity_properties" element
            int entPropsLineIdx = 0;
            for (int i = 0; i < linesOfEntity.Count; i++)
                if (linesOfEntity[i].Equals(indentation + ENT_PROPS_BEGINNING))
                    entPropsLineIdx = i;

            // Set targetname of this block IF it's hidden on round start
            // Note: Setting the targetname of 8000 blocks made the Hammer editor very laggy...
            if (block.hiddenOnRoundStart) { 
                for (int i = entPropsLineIdx; i < linesOfEntity.Count; i++)
                {
                    if (linesOfEntity[i].StartsWith(indentation + "\t" + TARGETNAME_BEGINNING))
                    {
                        linesOfEntity[i] = indentation + "\t" + TARGETNAME_BEGINNING + block.targetname + "\"";
                        break;
                    }
                }
            }

            // Find beginning and end of "connectionsData" element
            int connsDataStartLineIdx = 0;
            int connsDataEndLineIdx = 0; // past-the-end
            for (int i = 0; i < linesOfEntity.Count; i++)
                if (linesOfEntity[i].StartsWith(indentation + CONNSDATA_BEGINNING))
                    connsDataStartLineIdx = i + 2;
            for (int i = connsDataStartLineIdx; i < linesOfEntity.Count; i++)
            {
                if (linesOfEntity[i].StartsWith(indentation + "]"))
                {
                    connsDataEndLineIdx = i;
                    break;
                }
            }

            // Add OnBreak outputs of this block without any commas
            foreach (List<string> onBreakOutput in block.onBreakOutputs)
            {
                foreach (string line in onBreakOutput)
                {
                    linesOfEntity.Insert(connsDataEndLineIdx, indentation + "\t" + line);
                    connsDataEndLineIdx++;
                }
            }

            // Add missing commas, but not a trailing comma
            for (int i = connsDataStartLineIdx; i < connsDataEndLineIdx; i++)
            {
                if (i == connsDataEndLineIdx - 1)
                    break; // No trailing comma!
                if (linesOfEntity[i].Equals(indentation + "\t}"))
                    linesOfEntity[i] = indentation + "\t},";
            }
        }

        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------

        // The srcVmapFile must be the decoded VMAP file
        public static void WriteOptimizationRemovalToVmap(string srcVmapFile, string destVmapFile)
        {
            try
            {
                Console.WriteLine("Writing optimization removal to the VMAP file...");

                const string ENTITY_BEGINNING = "\"CMapEntity\"";


                using (StreamReader sr = new StreamReader(srcVmapFile))
                using (StreamWriter sw = new StreamWriter(destVmapFile, false)) // Overwrite destination file
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.EndsWith(ENTITY_BEGINNING))
                        {
                            sw.WriteLine(line);

                            // Read all lines of this entity
                            int indentLevel = line.IndexOf(ENTITY_BEGINNING); // # of tab characters
                            string entityEnding = "";
                            for (int i = 0; i < indentLevel; i++)
                                entityEnding += "\t";
                            entityEnding += "}"; // Note: A comma could follow this

                            List<string> linesOfEntity = new List<string>();
                            while ((line = sr.ReadLine()) != null)
                            {
                                linesOfEntity.Add(line);
                                if (line.StartsWith(entityEnding))
                                {
                                    // We've collected all lines of this entity, now process them as a whole
                                    WriteOptimizationRemovalToEntity(linesOfEntity);
                                    foreach (string entLine in linesOfEntity)
                                        sw.WriteLine(entLine);
                                    break;
                                }
                            }
                            continue; // Entity fully processed, go to next line
                        }

                        sw.WriteLine(line);
                    }
                }
                Console.WriteLine("DONE!");
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR: Failed to add optimizations to VMAP file:");
                Console.WriteLine(e.Message);
            }

        }

        // Process and potentially modify lines of a VMAP CMapEntity element
        public static void WriteOptimizationRemovalToEntity(List<string> linesOfEntity)
        {
            // Note: linesOfEntity includes opening and closing curly brace lines.
            //       The closing curly brace line might have a trailing comma.

            const string ENT_PROPS_BEGINNING = "\"entity_properties\" \"EditGameClassProps\"";
            const string TARGETNAME_BEGINNING = "\"targetname\" \"string\" \"";
            const string CONNSDATA_BEGINNING = "\"connectionsData\" \"element_array\"";

            // Determine indent level
            int indentLevel = linesOfEntity[0].IndexOf("{") + 1; // # of tab characters
            string indentation = "";
            for (int i = 0; i < indentLevel; i++)
                indentation += "\t";

            // Find location of "entity_properties" element
            int entPropsLineIdx = 0;
            for (int i = 0; i < linesOfEntity.Count; i++)
                if (linesOfEntity[i].Equals(indentation + ENT_PROPS_BEGINNING))
                    entPropsLineIdx = i;

            // Clear targetname if it was generated by this program
            for (int i = entPropsLineIdx; i < linesOfEntity.Count; i++)
            {
                string lineBegin = indentation + "\t" + TARGETNAME_BEGINNING;
                if (linesOfEntity[i].StartsWith(lineBegin))
                {
                    // If targetname has our prefix
                    if (linesOfEntity[i].Substring(lineBegin.Length).StartsWith(NAME_PREFIX))
                        linesOfEntity[i] = lineBegin + "" + "\""; // Set empty targetname
                    break;
                }
            }

            // Find beginning and end of "connectionsData" element
            int connsDataStartLineIdx = 0;
            int connsDataEndLineIdx = 0; // past-the-end
            for (int i = 0; i < linesOfEntity.Count; i++)
                if (linesOfEntity[i].StartsWith(indentation + CONNSDATA_BEGINNING))
                    connsDataStartLineIdx = i + 2;
            for (int i = connsDataStartLineIdx; i < linesOfEntity.Count; i++)
            {
                if (linesOfEntity[i].StartsWith(indentation + "]"))
                {
                    connsDataEndLineIdx = i;
                    break;
                }
            }

            // Collect all entries of "connectionsData" element
            List<List<string>> connsDataEntries = new List<List<string>>();
            for (int i = connsDataStartLineIdx; i < connsDataEndLineIdx; i++)
            {
                List<string> nextEntry = new List<string>();
                for (; i < connsDataEndLineIdx; i++)
                {
                    nextEntry.Add(linesOfEntity[i]);
                    if (linesOfEntity[i].StartsWith(indentation + "\t}"))
                        break;
                }
                connsDataEntries.Add(nextEntry);
            }

            // Remove all entries of "connectionsData" element
            linesOfEntity.RemoveRange(connsDataStartLineIdx, connsDataEndLineIdx - connsDataStartLineIdx);
            connsDataEndLineIdx = connsDataStartLineIdx;

            // Add back entries of "connectionsData" element that were not generated by us
            foreach (List<string> connsDataEntry in connsDataEntries)
            {
                bool wasGeneratedByUs = true;
                foreach (string line in connsDataEntry)
                {
                    if (line.StartsWith(indentation + "\t\t" + "\"outputName\" \"string\" \""))
                        if (!line.StartsWith(indentation + "\t\t" + "\"outputName\" \"string\" \"OnBreak\""))
                            wasGeneratedByUs = false;
                    if (line.StartsWith(indentation + "\t\t" + "\"targetName\" \"string\" \""))
                        if (!line.StartsWith(indentation + "\t\t" + "\"targetName\" \"string\" \"" + NAME_PREFIX))
                            wasGeneratedByUs = false;
                    if (line.StartsWith(indentation + "\t\t" + "\"inputName\" \"string\" \""))
                        if (!line.StartsWith(indentation + "\t\t" + "\"inputName\" \"string\" \"Alpha\""))
                            wasGeneratedByUs = false;
                    if (line.StartsWith(indentation + "\t\t" + "\"overrideParam\" \"string\" \""))
                        if (!line.StartsWith(indentation + "\t\t" + "\"overrideParam\" \"string\" \"255\""))
                            wasGeneratedByUs = false;
                }

                if (!wasGeneratedByUs)
                    foreach (string line in connsDataEntry)
                    {
                        linesOfEntity.Insert(connsDataEndLineIdx, line);
                        connsDataEndLineIdx++;
                    }
            }

            // Add missing commas and potentially remove a trailing comma
            for (int i = connsDataStartLineIdx; i < connsDataEndLineIdx; i++)
            {
                if (i == connsDataEndLineIdx - 1) // Remove trailing comma
                {
                    if (linesOfEntity[i].Equals(indentation + "\t},"))
                        linesOfEntity[i] = indentation + "\t}";
                }
                else // Add missing commas
                {
                    if (linesOfEntity[i].Equals(indentation + "\t}"))
                        linesOfEntity[i] = indentation + "\t},";
                }
            }
        }
    }
}
