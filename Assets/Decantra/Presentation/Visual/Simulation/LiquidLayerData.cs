/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Presentation.Visual.Simulation
{
    /// <summary>
    /// Immutable snapshot of a single visual liquid layer used by Bottle3DView and the shader.
    ///
    /// Coordinates are in "interior space" where 0 = interior bottom and 1 = interior top
    /// (matching the [0..1] fill-fraction used inside the Liquid3D shader's _FillMin/_FillMax
    /// properties for each layer slot).
    ///
    /// All values are exact rational fractions derived from integer slot counts —
    /// no floating-point drift is introduced.
    /// </summary>
    public readonly struct LiquidLayerData
    {
        /// <summary>
        /// Index of the bottom-most slot occupied by this layer (0-based).
        /// </summary>
        public readonly int SlotIndexBottom;

        /// <summary>
        /// Number of contiguous same-color slots in this layer.
        /// </summary>
        public readonly int SlotCount;

        /// <summary>
        /// Fill fraction of the layer's bottom edge relative to total interior height.
        /// Range: [0..1]. Exact rational value: SlotIndexBottom / BottleCapacity.
        /// </summary>
        public readonly float FillMin;

        /// <summary>
        /// Fill fraction of the layer's top edge relative to total interior height.
        /// Range: [0..1]. Exact rational value: (SlotIndexBottom + SlotCount) / BottleCapacity.
        /// </summary>
        public readonly float FillMax;

        /// <summary>
        /// Normalised color R component (from ColorPalette lookup).
        /// </summary>
        public readonly float R;

        /// <summary>
        /// Normalised color G component.
        /// </summary>
        public readonly float G;

        /// <summary>
        /// Normalised color B component.
        /// </summary>
        public readonly float B;

        /// <summary>
        /// Color ID for palette lookup (integer enum value).
        /// </summary>
        public readonly int ColorId;

        public LiquidLayerData(int slotIndexBottom, int slotCount, int bottleCapacity,
                               float r, float g, float b, int colorId)
        {
            SlotIndexBottom = slotIndexBottom;
            SlotCount = slotCount;
            FillMin = bottleCapacity > 0 ? (float)slotIndexBottom / bottleCapacity : 0f;
            FillMax = bottleCapacity > 0 ? (float)(slotIndexBottom + slotCount) / bottleCapacity : 0f;
            R = r;
            G = g;
            B = b;
            ColorId = colorId;
        }

        /// <summary>Height fraction of this layer (FillMax - FillMin).</summary>
        public float Height => FillMax - FillMin;

        public override string ToString() =>
            $"Layer(slots={SlotIndexBottom}..{SlotIndexBottom + SlotCount - 1}, " +
            $"fill={FillMin:F4}..{FillMax:F4}, colorId={ColorId})";
    }
}
