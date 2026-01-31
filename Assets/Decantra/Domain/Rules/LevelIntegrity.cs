using System;
using System.Collections.Generic;
using Decantra.Domain.Model;

namespace Decantra.Domain.Rules
{
    public static class LevelIntegrity
    {
        public static bool TryValidate(LevelState state, out string error)
        {
            error = null;
            if (state == null)
            {
                error = "State is null.";
                return false;
            }

            if (state.Bottles == null || state.Bottles.Count == 0)
            {
                error = "State has no bottles.";
                return false;
            }

            var volumes = new Dictionary<ColorId, int>();
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var bottle = state.Bottles[i];
                if (bottle == null)
                {
                    error = $"Bottle {i} is null.";
                    return false;
                }

                if (bottle.Capacity <= 0)
                {
                    error = $"Bottle {i} has invalid capacity.";
                    return false;
                }

                if (bottle.Count > bottle.Capacity)
                {
                    error = $"Bottle {i} is overfilled.";
                    return false;
                }

                for (int s = 0; s < bottle.Slots.Count; s++)
                {
                    var color = bottle.Slots[s];
                    if (!color.HasValue) continue;
                    if (!volumes.TryGetValue(color.Value, out int count))
                    {
                        count = 0;
                    }
                    volumes[color.Value] = count + 1;
                }
            }

            if (!HasCapacityForEachColor(state.Bottles, volumes, requireNonSink: true, out error))
            {
                return false;
            }

            if (!ValidateSinkMonochrome(state.Bottles, out error))
            {
                return false;
            }

            if (!ValidateSealedSinks(state.Bottles, volumes, out error))
            {
                return false;
            }

            return true;
        }

        public static void ValidateOrThrow(LevelState state)
        {
            if (!TryValidate(state, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static bool HasCapacityForEachColor(IReadOnlyList<Bottle> bottles, Dictionary<ColorId, int> volumes, bool requireNonSink, out string error)
        {
            error = null;
            foreach (var kvp in volumes)
            {
                int volume = kvp.Value;
                bool found = false;
                for (int i = 0; i < bottles.Count; i++)
                {
                    var bottle = bottles[i];
                    if (requireNonSink && bottle.IsSink) continue;
                    if (bottle.Capacity == volume)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    error = $"No non-sink bottle can contain color {kvp.Key} volume {volume}.";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateSealedSinks(IReadOnlyList<Bottle> bottles, Dictionary<ColorId, int> volumes, out string error)
        {
            error = null;
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (!bottle.IsSink || !bottle.IsFull) continue;

                if (!bottle.IsSolvedBottle())
                {
                    error = $"Sink bottle {i} is sealed with mixed colors.";
                    return false;
                }

                var color = bottle.TopColor;
                if (!color.HasValue)
                {
                    error = $"Sink bottle {i} is full but has no color.";
                    return false;
                }

                if (!volumes.TryGetValue(color.Value, out int volume) || volume != bottle.Capacity)
                {
                    error = $"Sink bottle {i} seals color {color.Value} volume {volume}, capacity {bottle.Capacity}.";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateSinkMonochrome(IReadOnlyList<Bottle> bottles, out string error)
        {
            error = null;
            if (bottles == null) return true;
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (bottle == null) continue;
                if (!bottle.IsSink) continue;
                if (!bottle.IsSingleColorOrEmpty())
                {
                    error = $"Sink bottle {i} has mixed colors.";
                    return false;
                }
            }
            return true;
        }
    }
}
