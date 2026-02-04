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
    /// Provides lookup by archetype and mapping from legacy GeneratorFamily enum.
    /// </summary>
    public static class BackgroundGeneratorRegistry
    {
        private static readonly Dictionary<GeneratorArchetype, IBackgroundFieldGenerator> Generators;
        private static readonly Dictionary<GeneratorFamily, GeneratorArchetype> LegacyMapping;

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

            // Map legacy GeneratorFamily to new archetypes
            LegacyMapping = new Dictionary<GeneratorFamily, GeneratorArchetype>
            {
                // Line/band patterns → AtmosphericWash (soft gradients)
                { GeneratorFamily.DirectionalLineFields, GeneratorArchetype.AtmosphericWash },
                { GeneratorFamily.BandGradients, GeneratorArchetype.AtmosphericWash },

                // Cell-based patterns → DomainWarpedClouds (organic alternative)
                { GeneratorFamily.VoronoiRegions, GeneratorArchetype.DomainWarpedClouds },
                { GeneratorFamily.PolygonShards, GeneratorArchetype.DomainWarpedClouds },

                // Wave/noise patterns → CurlFlowAdvection (flowing alternative)
                { GeneratorFamily.WaveInterference, GeneratorArchetype.CurlFlowAdvection },

                // Fractal patterns → DomainWarpedClouds (similar organic quality)
                { GeneratorFamily.FractalLite, GeneratorArchetype.DomainWarpedClouds },
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
        /// Gets the generator for the specified legacy GeneratorFamily.
        /// Maps to appropriate new archetype.
        /// </summary>
        public static IBackgroundFieldGenerator GetGenerator(GeneratorFamily legacyFamily)
        {
            var archetype = MapLegacyToArchetype(legacyFamily);
            return GetGenerator(archetype);
        }

        /// <summary>
        /// Maps a legacy GeneratorFamily to the new GeneratorArchetype.
        /// </summary>
        public static GeneratorArchetype MapLegacyToArchetype(GeneratorFamily legacyFamily)
        {
            if (LegacyMapping.TryGetValue(legacyFamily, out var archetype))
            {
                return archetype;
            }

            // Default fallback
            return GeneratorArchetype.AtmosphericWash;
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
            return Generators.Keys;
        }

        /// <summary>
        /// Selects an appropriate archetype for a zone based on zone index.
        /// Ensures visual variety across zones while maintaining determinism.
        /// </summary>
        /// <param name="zoneIndex">The zone index (0 = levels 1-9, 1 = levels 10-19, etc.)</param>
        /// <param name="seed">Deterministic seed for variation within archetype selection.</param>
        public static GeneratorArchetype SelectArchetypeForZone(int zoneIndex, ulong seed)
        {
            // All available archetypes for zone selection
            GeneratorArchetype[] allArchetypes =
            {
                GeneratorArchetype.AtmosphericWash,
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection,
                GeneratorArchetype.FractalEscapeDensity,
                GeneratorArchetype.BotanicalIFS,
                GeneratorArchetype.ImplicitBlobHaze,
                GeneratorArchetype.MarbledFlow,
                GeneratorArchetype.ConcentricRipples,
                GeneratorArchetype.NebulaGlow,
                GeneratorArchetype.OrganicCells,
                GeneratorArchetype.CrystallineFrost,
                GeneratorArchetype.BranchingTree,
                GeneratorArchetype.VineTendrils,
                GeneratorArchetype.RootNetwork,
                GeneratorArchetype.CanopyDapple,
                GeneratorArchetype.FloralMandala,
            };

            // Zone 0 (tutorial/early game): Always use AtmosphericWash for gentleness
            if (zoneIndex == 0)
            {
                return GeneratorArchetype.AtmosphericWash;
            }

            // Zone 1-5: Introduce specific archetypes progressively
            if (zoneIndex == 1) return GeneratorArchetype.DomainWarpedClouds;
            if (zoneIndex == 2) return GeneratorArchetype.OrganicCells; // User's favorite!
            if (zoneIndex == 3) return GeneratorArchetype.BranchingTree;
            if (zoneIndex == 4) return GeneratorArchetype.FloralMandala;
            if (zoneIndex == 5) return GeneratorArchetype.NebulaGlow;

            // Zone 6+: Cycle through all archetypes with deterministic variety
            var rng = new DeterministicRng(seed ^ (ulong)zoneIndex);

            // Ensure adjacent zones are different
            var previousArchetype = SelectArchetypeForZone(zoneIndex - 1, seed);

            // Pick randomly but exclude previous
            GeneratorArchetype selected;
            int attempts = 0;
            do
            {
                int index = rng.NextInt(0, allArchetypes.Length);
                selected = allArchetypes[index];
                attempts++;
            } while (selected == previousArchetype && attempts < 15);

            return selected;
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
