/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Domain.Background
{
    /// <summary>
    /// Generates a 2D alpha field for background layer rendering.
    /// All implementations MUST be deterministic: identical parameters produce identical output.
    /// </summary>
    public interface IBackgroundFieldGenerator
    {
        /// <summary>
        /// Generates an alpha field of the specified dimensions.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="parameters">Generator-specific parameters.</param>
        /// <param name="seed">Deterministic seed for random values.</param>
        /// <returns>Float array of size width*height with values in [0,1].</returns>
        float[] Generate(int width, int height, FieldParameters parameters, ulong seed);

        /// <summary>
        /// Returns the archetype this generator implements.
        /// </summary>
        GeneratorArchetype Archetype { get; }
    }

    /// <summary>
    /// Canonical generator archetypes. Each archetype represents a family of related
    /// generation techniques that share similar visual characteristics.
    /// </summary>
    public enum GeneratorArchetype
    {
        /// <summary>
        /// Domain-warped clouds using fBm with curl distortion.
        /// Produces soft, billowy, organic cloud-like patterns.
        /// </summary>
        DomainWarpedClouds = 0,

        /// <summary>
        /// Curl noise flow fields with particle advection.
        /// Produces flowing, streamline-like organic patterns.
        /// </summary>
        CurlFlowAdvection = 1,

        /// <summary>
        /// Color-first atmospheric washes and gradients.
        /// Produces soft, painterly, fog-like backgrounds.
        /// </summary>
        AtmosphericWash = 2,

        /// <summary>
        /// Escape-time fractal density (Julia/Mandelbrot).
        /// Produces intricate, organic fractal patterns.
        /// </summary>
        FractalEscapeDensity = 3,

        /// <summary>
        /// IFS-based botanical/branching patterns.
        /// Produces organic fern-like and branching structures.
        /// </summary>
        BotanicalIFS = 4,

        /// <summary>
        /// Implicit surface / metaball haze volumes.
        /// Produces soft, blob-like organic shapes.
        /// </summary>
        ImplicitBlobHaze = 5,

        /// <summary>
        /// Marble-like flowing veined patterns.
        /// Produces elegant, stone-like textures.
        /// </summary>
        MarbledFlow = 6,

        /// <summary>
        /// Concentric ripple and wave interference patterns.
        /// Produces soft, rippling water-like effects.
        /// </summary>
        ConcentricRipples = 7,

        /// <summary>
        /// Nebula-like soft glowing regions.
        /// Produces cosmic, ethereal backgrounds.
        /// </summary>
        NebulaGlow = 8,

        /// <summary>
        /// Organic cellular patterns with soft edges.
        /// Produces natural, cell-like structures.
        /// </summary>
        OrganicCells = 9,

        /// <summary>
        /// Crystalline frost-like branching patterns.
        /// Produces delicate, ice-crystal structures.
        /// </summary>
        CrystallineFrost = 10,

        /// <summary>
        /// Recursive tree branching silhouettes.
        /// Produces organic tree-like structures growing upward.
        /// </summary>
        BranchingTree = 11,

        /// <summary>
        /// Curving vine and tendril patterns.
        /// Produces flowing, climbing plant structures.
        /// </summary>
        VineTendrils = 12,

        /// <summary>
        /// Underground root-like spreading networks.
        /// Produces organic, spreading root systems.
        /// </summary>
        RootNetwork = 13,

        /// <summary>
        /// Dappled light through canopy leaves.
        /// Produces soft, overlapping leaf shadow patterns.
        /// </summary>
        CanopyDapple = 14,

        /// <summary>
        /// Radial floral petal arrangements.
        /// Produces soft, flower-like mandala patterns.
        /// </summary>
        FloralMandala = 15,

        // Intentionally no legacy archetypes.
    }

    /// <summary>
    /// Parameters for field generation, shared across all generator types.
    /// </summary>
    public struct FieldParameters
    {
        /// <summary>Scale multiplier for base frequency.</summary>
        public float Scale;

        /// <summary>Density/coverage target [0,1].</summary>
        public float Density;

        /// <summary>Number of octaves/iterations for multi-pass algorithms.</summary>
        public int Octaves;

        /// <summary>Warp/distortion amplitude [0,1].</summary>
        public float WarpAmplitude;

        /// <summary>Edge softness factor [0,1] where 0=sharp, 1=very soft.</summary>
        public float Softness;

        /// <summary>Whether this is a macro-scale layer (larger, softer features).</summary>
        public bool IsMacroLayer;

        /// <summary>
        /// Creates default parameters suitable for most generators.
        /// </summary>
        public static FieldParameters Default => new FieldParameters
        {
            Scale = 1.0f,
            Density = 0.5f,
            Octaves = 4,
            WarpAmplitude = 0.3f,
            Softness = 0.5f,
            IsMacroLayer = false
        };

        /// <summary>
        /// Creates parameters for macro-scale layers.
        /// </summary>
        public static FieldParameters Macro => new FieldParameters
        {
            Scale = 0.3f,
            Density = 0.4f,
            Octaves = 3,
            WarpAmplitude = 0.4f,
            Softness = 0.8f,
            IsMacroLayer = true
        };

        /// <summary>
        /// Creates parameters for meso-scale layers.
        /// </summary>
        public static FieldParameters Meso => new FieldParameters
        {
            Scale = 1.0f,
            Density = 0.5f,
            Octaves = 4,
            WarpAmplitude = 0.25f,
            Softness = 0.5f,
            IsMacroLayer = false
        };

        /// <summary>
        /// Creates parameters for micro-scale detail layers.
        /// </summary>
        public static FieldParameters Micro => new FieldParameters
        {
            Scale = 2.5f,
            Density = 0.6f,
            Octaves = 2,
            WarpAmplitude = 0.1f,
            Softness = 0.3f,
            IsMacroLayer = false
        };
    }
}
