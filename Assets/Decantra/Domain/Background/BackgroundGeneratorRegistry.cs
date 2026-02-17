/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Rules;

namespace Decantra.Domain.Background
{
    /// <summary>
    /// Registry for background field generators.
    /// Provides lookup by archetype.
    /// </summary>
    public static class BackgroundGeneratorRegistry
    {
        private static readonly Dictionary<GeneratorArchetype, IBackgroundFieldGenerator> Generators;
        private static readonly GeneratorArchetype[] AllowedArchetypesOrdered =
        {
            GeneratorArchetype.DomainWarpedClouds,
            GeneratorArchetype.CurlFlowAdvection,
            GeneratorArchetype.AtmosphericWash,
            GeneratorArchetype.NebulaGlow,
            GeneratorArchetype.MarbledFlow,
            GeneratorArchetype.ConcentricRipples,
            GeneratorArchetype.ImplicitBlobHaze,
            GeneratorArchetype.OrganicCells,
            GeneratorArchetype.BotanicalIFS,
            GeneratorArchetype.BranchingTree,
            GeneratorArchetype.RootNetwork,
            GeneratorArchetype.VineTendrils,
            GeneratorArchetype.CanopyDapple,
            GeneratorArchetype.FloralMandala,
            GeneratorArchetype.CrystallineFrost,
            GeneratorArchetype.FractalEscapeDensity
        };

        static BackgroundGeneratorRegistry()
        {
            // Register all implemented generators
            Generators = new Dictionary<GeneratorArchetype, IBackgroundFieldGenerator>
            {
                { GeneratorArchetype.DomainWarpedClouds, new DomainWarpedCloudsGenerator() },
                { GeneratorArchetype.CurlFlowAdvection, new CurlFlowAdvectionGenerator() },
                { GeneratorArchetype.AtmosphericWash, new AtmosphericWashGenerator() },
                { GeneratorArchetype.FractalEscapeDensity, new FractalEscapeDensityGenerator() },
                { GeneratorArchetype.BotanicalIFS, new BotanicalIFSGenerator() },
                { GeneratorArchetype.ImplicitBlobHaze, new ImplicitBlobHazeGenerator() },
                { GeneratorArchetype.MarbledFlow, new MarbledFlowGenerator() },
                { GeneratorArchetype.ConcentricRipples, new ConcentricRipplesGenerator() },
                { GeneratorArchetype.NebulaGlow, new NebulaGlowGenerator() },
                { GeneratorArchetype.OrganicCells, new OrganicCellsGenerator() },
                { GeneratorArchetype.CrystallineFrost, new CrystallineFrostGenerator() },
                { GeneratorArchetype.BranchingTree, new BranchingTreeGenerator() },
                { GeneratorArchetype.VineTendrils, new VineTendrilsGenerator() },
                { GeneratorArchetype.RootNetwork, new RootNetworkGenerator() },
                { GeneratorArchetype.CanopyDapple, new CanopyDappleGenerator() },
                { GeneratorArchetype.FloralMandala, new FloralMandalaGenerator() },
            };

        }

        /// <summary>
        /// Gets the generator for the specified archetype.
        /// </summary>
        /// <exception cref="ArgumentException">If archetype is not implemented.</exception>
        public static IBackgroundFieldGenerator GetGenerator(GeneratorArchetype archetype)
        {
            if (Generators.TryGetValue(archetype, out var generator))
            {
                return generator;
            }

            throw new ArgumentException($"Generator archetype {archetype} is not yet implemented. Deferred to Phase 3.");
        }

        /// <summary>
        /// Checks if a generator archetype is currently implemented.
        /// </summary>
        public static bool IsImplemented(GeneratorArchetype archetype)
        {
            return Generators.ContainsKey(archetype);
        }

        /// <summary>
        /// Gets all implemented archetypes.
        /// </summary>
        public static IEnumerable<GeneratorArchetype> GetImplementedArchetypes()
        {
            for (int i = 0; i < AllowedArchetypesOrdered.Length; i++)
            {
                var archetype = AllowedArchetypesOrdered[i];
                if (Generators.ContainsKey(archetype))
                {
                    yield return archetype;
                }
            }
        }

        /// <summary>
        /// Gets the allowed archetypes in deterministic progression order.
        /// </summary>
        public static IReadOnlyList<GeneratorArchetype> GetAllowedArchetypes()
        {
            return AllowedArchetypesOrdered;
        }

        /// <summary>
        /// Selects an archetype for a specific level index.
        /// All levels within a 10-level zone share the same archetype.
        /// The globalSeed produces a deterministic shuffled ordering of all 16
        /// archetypes per cycle. No two consecutive zones ever share the same
        /// archetype, including at cycle boundaries.
        /// </summary>
        public static GeneratorArchetype SelectArchetypeForLevel(int levelIndex, int globalSeed)
        {
            int zoneIndex = BackgroundRules.GetZoneIndex(levelIndex);

            // Zone 0 (levels 1-9) is always the intro theme.
            if (zoneIndex == 0)
                return GeneratorArchetype.DomainWarpedClouds;

            // Remaining zones use a shuffled cycle over all archetypes.
            // Subtract 1 so the shuffle starts fresh at zone 1.
            int shuffleIndex = zoneIndex - 1;
            int count = AllowedArchetypesOrdered.Length;
            int cycleIndex = shuffleIndex / count;
            int posInCycle = shuffleIndex % count;

            var perm = ShuffleForCycle(globalSeed, cycleIndex, count);

            // Determine what the previous zone's archetype index was.
            int prevArchetypeIndex;
            if (cycleIndex == 0)
            {
                // Zone 0 is always DomainWarpedClouds (index 0 in AllowedArchetypesOrdered).
                prevArchetypeIndex = 0;
            }
            else
            {
                var prevPerm = ShuffleForCycle(globalSeed, cycleIndex - 1, count);
                prevArchetypeIndex = prevPerm[count - 1];
            }

            // Ensure no repeat at the boundary (zone 0→1 or cycle N→N+1).
            if (perm[0] == prevArchetypeIndex)
            {
                for (int i = 1; i < count; i++)
                {
                    if (perm[i] != prevArchetypeIndex)
                    {
                        (perm[0], perm[i]) = (perm[i], perm[0]);
                        break;
                    }
                }
            }

            return AllowedArchetypesOrdered[perm[posInCycle]];
        }

        /// <summary>
        /// Deterministic Fisher-Yates shuffle of indices [0..count-1] for a
        /// given cycle, seeded from globalSeed and cycleIndex.
        /// </summary>
        private static int[] ShuffleForCycle(int globalSeed, int cycleIndex, int count)
        {
            // Mix seed and cycle via Knuth multiplicative hash
            uint state = (uint)globalSeed ^ ((uint)cycleIndex * 2654435761u);

            var perm = new int[count];
            for (int i = 0; i < count; i++) perm[i] = i;

            // Fisher-Yates
            for (int i = count - 1; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int j = (int)(state % (uint)(i + 1));
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }

            return perm;
        }

        /// <summary>
        /// Gets the default FieldParameters for a given archetype and layer type.
        /// </summary>
        public static FieldParameters GetDefaultParameters(GeneratorArchetype archetype, ScaleBand scaleBand)
        {
            var baseParams = scaleBand switch
            {
                ScaleBand.Macro => FieldParameters.Macro,
                ScaleBand.Meso => FieldParameters.Meso,
                ScaleBand.Micro => FieldParameters.Micro,
                _ => FieldParameters.Default
            };

            // Archetype-specific adjustments
            switch (archetype)
            {
                case GeneratorArchetype.DomainWarpedClouds:
                    baseParams.WarpAmplitude = scaleBand == ScaleBand.Macro ? 0.5f : 0.3f;
                    baseParams.Octaves = scaleBand == ScaleBand.Macro ? 4 : 3;
                    break;

                case GeneratorArchetype.CurlFlowAdvection:
                    baseParams.Scale = scaleBand == ScaleBand.Macro ? 0.4f : 1.2f;
                    baseParams.Density = scaleBand == ScaleBand.Macro ? 0.35f : 0.5f;
                    break;

                case GeneratorArchetype.AtmosphericWash:
                    baseParams.Softness = scaleBand == ScaleBand.Macro ? 0.9f : 0.6f;
                    baseParams.WarpAmplitude = 0.2f;
                    break;
            }

            return baseParams;
        }
    }
}
