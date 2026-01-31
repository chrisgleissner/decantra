using System;
using System.Collections.Generic;
using System.Text;
using Decantra.Domain.Model;

namespace Decantra.Domain.Export
{
    public static class LevelLanguage
    {
        public const string Language = "decantra-level";
        public const int Version = 1;
        public const int DefaultVolumePerSlot = 100;

        public static LevelLanguageDocument FromLevelState(LevelState initial, int levelIndex, int gridRows, int gridCols, IReadOnlyList<LevelLanguageMove> moves, int volumePerSlot = DefaultVolumePerSlot)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (gridRows <= 0) throw new ArgumentOutOfRangeException(nameof(gridRows));
            if (gridCols <= 0) throw new ArgumentOutOfRangeException(nameof(gridCols));
            if (volumePerSlot <= 0) throw new ArgumentOutOfRangeException(nameof(volumePerSlot));

            int totalCells = gridRows * gridCols;
            if (initial.Bottles.Count > totalCells)
            {
                throw new InvalidOperationException("Grid is too small for level bottles.");
            }

            var cells = new List<List<LevelLanguageBottle>>(gridRows);
            for (int row = 0; row < gridRows; row++)
            {
                var rowCells = new List<LevelLanguageBottle>(gridCols);
                for (int col = 0; col < gridCols; col++)
                {
                    int index = row * gridCols + col;
                    if (index >= initial.Bottles.Count)
                    {
                        rowCells.Add(null);
                        continue;
                    }

                    var bottle = initial.Bottles[index];
                    if (bottle == null)
                    {
                        rowCells.Add(null);
                        continue;
                    }

                    var layers = BuildLayers(bottle, volumePerSlot);
                    var flags = bottle.IsSink ? new List<string> { "sink" } : null;
                    rowCells.Add(new LevelLanguageBottle(bottle.Capacity * volumePerSlot, layers, flags));
                }
                cells.Add(rowCells);
            }

            var document = new LevelLanguageDocument(
                Language,
                Version,
                levelIndex,
                new LevelLanguageGrid(gridRows, gridCols),
                new LevelLanguageInitial(cells),
                moves == null ? new List<LevelLanguageMove>() : new List<LevelLanguageMove>(moves));

            return document;
        }

        public static string Serialize(LevelLanguageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!LevelLanguageValidator.TryValidate(document, out string error))
            {
                throw new InvalidOperationException(error);
            }

            var sb = new StringBuilder(1024);
            sb.Append('{');
            AppendStringProperty(sb, "lang", document.Lang);
            sb.Append(',');
            AppendNumberProperty(sb, "version", document.Version);
            sb.Append(',');
            AppendNumberProperty(sb, "level", document.Level);
            sb.Append(',');
            sb.Append("\"grid\":{");
            AppendNumberProperty(sb, "rows", document.Grid.Rows);
            sb.Append(',');
            AppendNumberProperty(sb, "cols", document.Grid.Cols);
            sb.Append("},");
            sb.Append("\"initial\":{\"cells\":[");
            for (int r = 0; r < document.Initial.Cells.Count; r++)
            {
                if (r > 0) sb.Append(',');
                sb.Append('[');
                var row = document.Initial.Cells[r];
                for (int c = 0; c < row.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    var bottle = row[c];
                    if (bottle == null)
                    {
                        sb.Append("null");
                        continue;
                    }

                    sb.Append('{');
                    AppendNumberProperty(sb, "capacity", bottle.Capacity);
                    sb.Append(',');
                    sb.Append("\"layers\":[");
                    for (int i = 0; i < bottle.Layers.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append('[');
                        AppendString(sb, bottle.Layers[i].Color);
                        sb.Append(',');
                        sb.Append(bottle.Layers[i].Volume);
                        sb.Append(']');
                    }
                    sb.Append(']');

                    if (bottle.Flags != null && bottle.Flags.Count > 0)
                    {
                        sb.Append(',');
                        sb.Append("\"flags\":[");
                        for (int i = 0; i < bottle.Flags.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            AppendString(sb, bottle.Flags[i]);
                        }
                        sb.Append(']');
                    }

                    sb.Append('}');
                }
                sb.Append(']');
            }
            sb.Append(']');
            sb.Append('}');
            sb.Append(',');
            sb.Append("\"moves\":[");
            for (int i = 0; i < document.Moves.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var move = document.Moves[i];
                sb.Append('{');
                sb.Append("\"from\":[");
                sb.Append(move.FromRow);
                sb.Append(',');
                sb.Append(move.FromCol);
                sb.Append("],");
                sb.Append("\"to\":[");
                sb.Append(move.ToRow);
                sb.Append(',');
                sb.Append(move.ToCol);
                sb.Append("]}");
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryParse(string json, out LevelLanguageDocument document, out string error)
        {
            document = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON is empty.";
                return false;
            }

            if (!JsonLite.TryParse(json, out var root, out error))
            {
                return false;
            }

            if (root.Type != JsonLite.JsonType.Object)
            {
                error = "Root must be an object.";
                return false;
            }

            var obj = root.ObjectValue;
            if (!TryGetString(obj, "lang", out var lang, out error)) return false;
            if (!TryGetInt(obj, "version", out var version, out error)) return false;
            if (!TryGetInt(obj, "level", out var level, out error)) return false;

            if (!obj.TryGetValue("grid", out var gridValue) || gridValue.Type != JsonLite.JsonType.Object)
            {
                error = "grid must be an object.";
                return false;
            }
            if (!TryGetInt(gridValue.ObjectValue, "rows", out var rows, out error)) return false;
            if (!TryGetInt(gridValue.ObjectValue, "cols", out var cols, out error)) return false;

            if (!obj.TryGetValue("initial", out var initialValue) || initialValue.Type != JsonLite.JsonType.Object)
            {
                error = "initial must be an object.";
                return false;
            }
            if (!initialValue.ObjectValue.TryGetValue("cells", out var cellsValue) || cellsValue.Type != JsonLite.JsonType.Array)
            {
                error = "initial.cells must be an array.";
                return false;
            }

            var rowsList = new List<List<LevelLanguageBottle>>(rows);
            if (cellsValue.ArrayValue.Count != rows)
            {
                error = "cells row count does not match grid.rows.";
                return false;
            }

            for (int r = 0; r < cellsValue.ArrayValue.Count; r++)
            {
                var rowValue = cellsValue.ArrayValue[r];
                if (rowValue.Type != JsonLite.JsonType.Array)
                {
                    error = "cells row must be an array.";
                    return false;
                }
                if (rowValue.ArrayValue.Count != cols)
                {
                    error = "cells column count does not match grid.cols.";
                    return false;
                }

                var row = new List<LevelLanguageBottle>(cols);
                for (int c = 0; c < rowValue.ArrayValue.Count; c++)
                {
                    var cellValue = rowValue.ArrayValue[c];
                    if (cellValue.Type == JsonLite.JsonType.Null)
                    {
                        row.Add(null);
                        continue;
                    }
                    if (cellValue.Type != JsonLite.JsonType.Object)
                    {
                        error = "cell must be null or object.";
                        return false;
                    }

                    if (!TryGetInt(cellValue.ObjectValue, "capacity", out var capacity, out error)) return false;
                    if (!cellValue.ObjectValue.TryGetValue("layers", out var layersValue) || layersValue.Type != JsonLite.JsonType.Array)
                    {
                        error = "layers must be an array.";
                        return false;
                    }

                    var layers = new List<LevelLanguageLayer>(layersValue.ArrayValue.Count);
                    for (int i = 0; i < layersValue.ArrayValue.Count; i++)
                    {
                        var layerValue = layersValue.ArrayValue[i];
                        if (layerValue.Type != JsonLite.JsonType.Array || layerValue.ArrayValue.Count != 2)
                        {
                            error = "layer must be [color, volume].";
                            return false;
                        }
                        var colorValue = layerValue.ArrayValue[0];
                        var volumeValue = layerValue.ArrayValue[1];
                        if (colorValue.Type != JsonLite.JsonType.String)
                        {
                            error = "layer color must be string.";
                            return false;
                        }
                        if (volumeValue.Type != JsonLite.JsonType.Number)
                        {
                            error = "layer volume must be number.";
                            return false;
                        }
                        layers.Add(new LevelLanguageLayer(colorValue.StringValue, volumeValue.NumberValue));
                    }

                    List<string> flags = null;
                    if (cellValue.ObjectValue.TryGetValue("flags", out var flagsValue))
                    {
                        if (flagsValue.Type != JsonLite.JsonType.Array)
                        {
                            error = "flags must be an array.";
                            return false;
                        }
                        flags = new List<string>(flagsValue.ArrayValue.Count);
                        for (int i = 0; i < flagsValue.ArrayValue.Count; i++)
                        {
                            var flagValue = flagsValue.ArrayValue[i];
                            if (flagValue.Type != JsonLite.JsonType.String)
                            {
                                error = "flag must be string.";
                                return false;
                            }
                            flags.Add(flagValue.StringValue);
                        }
                    }

                    row.Add(new LevelLanguageBottle(capacity, layers, flags));
                }
                rowsList.Add(row);
            }

            if (!obj.TryGetValue("moves", out var movesValue) || movesValue.Type != JsonLite.JsonType.Array)
            {
                error = "moves must be an array.";
                return false;
            }

            var moves = new List<LevelLanguageMove>(movesValue.ArrayValue.Count);
            for (int i = 0; i < movesValue.ArrayValue.Count; i++)
            {
                var moveValue = movesValue.ArrayValue[i];
                if (moveValue.Type != JsonLite.JsonType.Object)
                {
                    error = "move must be object.";
                    return false;
                }

                if (!moveValue.ObjectValue.TryGetValue("from", out var fromValue) || fromValue.Type != JsonLite.JsonType.Array || fromValue.ArrayValue.Count != 2)
                {
                    error = "from must be [row,col].";
                    return false;
                }
                if (!moveValue.ObjectValue.TryGetValue("to", out var toValue) || toValue.Type != JsonLite.JsonType.Array || toValue.ArrayValue.Count != 2)
                {
                    error = "to must be [row,col].";
                    return false;
                }

                if (!TryGetInt(fromValue.ArrayValue, 0, out var fromRow, out error)) return false;
                if (!TryGetInt(fromValue.ArrayValue, 1, out var fromCol, out error)) return false;
                if (!TryGetInt(toValue.ArrayValue, 0, out var toRow, out error)) return false;
                if (!TryGetInt(toValue.ArrayValue, 1, out var toCol, out error)) return false;

                moves.Add(new LevelLanguageMove(fromRow, fromCol, toRow, toCol));
            }

            document = new LevelLanguageDocument(lang, version, level, new LevelLanguageGrid(rows, cols), new LevelLanguageInitial(rowsList), moves);
            if (!LevelLanguageValidator.TryValidate(document, out error))
            {
                document = null;
                return false;
            }

            return true;
        }

        private static List<LevelLanguageLayer> BuildLayers(Bottle bottle, int volumePerSlot)
        {
            var layers = new List<LevelLanguageLayer>();
            if (bottle == null) return layers;
            ColorId? current = null;
            int count = 0;
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var slot = bottle.Slots[i];
                if (!slot.HasValue)
                {
                    continue;
                }

                if (!current.HasValue || current.Value != slot.Value)
                {
                    if (current.HasValue)
                    {
                        layers.Add(new LevelLanguageLayer(ColorName(current.Value), count * volumePerSlot));
                    }
                    current = slot.Value;
                    count = 1;
                }
                else
                {
                    count++;
                }
            }

            if (current.HasValue)
            {
                layers.Add(new LevelLanguageLayer(ColorName(current.Value), count * volumePerSlot));
            }

            return layers;
        }

        private static string ColorName(ColorId color)
        {
            return color switch
            {
                ColorId.Red => "red",
                ColorId.Blue => "blue",
                ColorId.Green => "green",
                ColorId.Yellow => "yellow",
                ColorId.Purple => "purple",
                ColorId.Orange => "orange",
                ColorId.Cyan => "cyan",
                ColorId.Magenta => "magenta",
                _ => "unknown"
            };
        }

        private static void AppendStringProperty(StringBuilder sb, string name, string value)
        {
            AppendString(sb, name);
            sb.Append(':');
            AppendString(sb, value);
        }

        private static void AppendNumberProperty(StringBuilder sb, string name, int value)
        {
            AppendString(sb, name);
            sb.Append(':');
            sb.Append(value);
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static bool TryGetString(Dictionary<string, JsonLite.JsonValue> obj, string key, out string value, out string error)
        {
            value = null;
            error = null;
            if (!obj.TryGetValue(key, out var val) || val.Type != JsonLite.JsonType.String)
            {
                error = $"{key} must be string.";
                return false;
            }
            value = val.StringValue;
            return true;
        }

        private static bool TryGetInt(Dictionary<string, JsonLite.JsonValue> obj, string key, out int value, out string error)
        {
            value = 0;
            error = null;
            if (!obj.TryGetValue(key, out var val) || val.Type != JsonLite.JsonType.Number)
            {
                error = $"{key} must be number.";
                return false;
            }
            value = val.NumberValue;
            return true;
        }

        private static bool TryGetInt(List<JsonLite.JsonValue> list, int index, out int value, out string error)
        {
            value = 0;
            error = null;
            if (index < 0 || index >= list.Count)
            {
                error = "Index out of range.";
                return false;
            }
            if (list[index].Type != JsonLite.JsonType.Number)
            {
                error = "Expected number.";
                return false;
            }
            value = list[index].NumberValue;
            return true;
        }
    }

    public sealed class LevelLanguageDocument
    {
        public LevelLanguageDocument(string lang, int version, int level, LevelLanguageGrid grid, LevelLanguageInitial initial, List<LevelLanguageMove> moves)
        {
            Lang = lang;
            Version = version;
            Level = level;
            Grid = grid;
            Initial = initial;
            Moves = moves ?? new List<LevelLanguageMove>();
        }

        public string Lang { get; }
        public int Version { get; }
        public int Level { get; }
        public LevelLanguageGrid Grid { get; }
        public LevelLanguageInitial Initial { get; }
        public List<LevelLanguageMove> Moves { get; }
    }

    public sealed class LevelLanguageGrid
    {
        public LevelLanguageGrid(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
        }

        public int Rows { get; }
        public int Cols { get; }
    }

    public sealed class LevelLanguageInitial
    {
        public LevelLanguageInitial(List<List<LevelLanguageBottle>> cells)
        {
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public List<List<LevelLanguageBottle>> Cells { get; }
    }

    public sealed class LevelLanguageBottle
    {
        public LevelLanguageBottle(int capacity, List<LevelLanguageLayer> layers, List<string> flags)
        {
            Capacity = capacity;
            Layers = layers ?? new List<LevelLanguageLayer>();
            Flags = flags;
        }

        public int Capacity { get; }
        public List<LevelLanguageLayer> Layers { get; }
        public List<string> Flags { get; }
    }

    public readonly struct LevelLanguageLayer
    {
        public LevelLanguageLayer(string color, int volume)
        {
            Color = color;
            Volume = volume;
        }

        public string Color { get; }
        public int Volume { get; }
    }

    public readonly struct LevelLanguageMove
    {
        public LevelLanguageMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            FromRow = fromRow;
            FromCol = fromCol;
            ToRow = toRow;
            ToCol = toCol;
        }

        public int FromRow { get; }
        public int FromCol { get; }
        public int ToRow { get; }
        public int ToCol { get; }
    }

    public static class LevelLanguageValidator
    {
        public static bool TryValidate(LevelLanguageDocument document, out string error)
        {
            error = null;
            if (document == null)
            {
                error = "Document is null.";
                return false;
            }

            if (document.Lang != LevelLanguage.Language)
            {
                error = "Unsupported lang.";
                return false;
            }

            if (document.Version != LevelLanguage.Version)
            {
                error = "Unsupported version.";
                return false;
            }

            if (document.Grid == null || document.Initial == null)
            {
                error = "Grid and initial are required.";
                return false;
            }

            if (document.Grid.Rows <= 0 || document.Grid.Cols <= 0)
            {
                error = "Grid rows and cols must be positive.";
                return false;
            }

            if (document.Initial.Cells.Count != document.Grid.Rows)
            {
                error = "Initial cells rows mismatch.";
                return false;
            }

            for (int r = 0; r < document.Initial.Cells.Count; r++)
            {
                var row = document.Initial.Cells[r];
                if (row == null || row.Count != document.Grid.Cols)
                {
                    error = "Initial cells cols mismatch.";
                    return false;
                }

                for (int c = 0; c < row.Count; c++)
                {
                    var bottle = row[c];
                    if (bottle == null) continue;
                    if (bottle.Capacity <= 0)
                    {
                        error = "Bottle capacity must be positive.";
                        return false;
                    }

                    int total = 0;
                    var distinctColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < bottle.Layers.Count; i++)
                    {
                        var layer = bottle.Layers[i];
                        if (string.IsNullOrWhiteSpace(layer.Color))
                        {
                            error = "Layer color must be string.";
                            return false;
                        }
                        if (layer.Volume <= 0)
                        {
                            error = "Layer volume must be positive.";
                            return false;
                        }
                        total += layer.Volume;
                        distinctColors.Add(layer.Color);
                    }

                    if (total > bottle.Capacity)
                    {
                        error = "Layer volumes exceed capacity.";
                        return false;
                    }

                    if (bottle.Flags != null && bottle.Flags.Contains("sink"))
                    {
                        if (distinctColors.Count > 1)
                        {
                            error = "Sink bottle cannot start with multiple colors.";
                            return false;
                        }
                    }
                }
            }

            if (document.Moves == null)
            {
                error = "Moves must be present.";
                return false;
            }

            for (int i = 0; i < document.Moves.Count; i++)
            {
                var move = document.Moves[i];
                if (move.FromRow < 0 || move.FromRow >= document.Grid.Rows) { error = "Move from row out of range."; return false; }
                if (move.FromCol < 0 || move.FromCol >= document.Grid.Cols) { error = "Move from col out of range."; return false; }
                if (move.ToRow < 0 || move.ToRow >= document.Grid.Rows) { error = "Move to row out of range."; return false; }
                if (move.ToCol < 0 || move.ToCol >= document.Grid.Cols) { error = "Move to col out of range."; return false; }
            }

            return true;
        }
    }

    internal static class JsonLite
    {
        internal enum JsonType
        {
            Object,
            Array,
            String,
            Number,
            Bool,
            Null
        }

        internal sealed class JsonValue
        {
            public JsonType Type { get; private set; }
            public Dictionary<string, JsonValue> ObjectValue { get; private set; }
            public List<JsonValue> ArrayValue { get; private set; }
            public string StringValue { get; private set; }
            public int NumberValue { get; private set; }
            public bool BoolValue { get; private set; }

            public static JsonValue Object(Dictionary<string, JsonValue> value) => new JsonValue { Type = JsonType.Object, ObjectValue = value };
            public static JsonValue Array(List<JsonValue> value) => new JsonValue { Type = JsonType.Array, ArrayValue = value };
            public static JsonValue String(string value) => new JsonValue { Type = JsonType.String, StringValue = value };
            public static JsonValue Number(int value) => new JsonValue { Type = JsonType.Number, NumberValue = value };
            public static JsonValue Bool(bool value) => new JsonValue { Type = JsonType.Bool, BoolValue = value };
            public static JsonValue Null() => new JsonValue { Type = JsonType.Null };
        }

        public static bool TryParse(string json, out JsonValue value, out string error)
        {
            value = null;
            error = null;
            int index = 0;
            try
            {
                SkipWhitespace(json, ref index);
                value = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index != json.Length)
                {
                    error = "Trailing characters.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static JsonValue ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) throw new InvalidOperationException("Unexpected end of JSON.");
            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return JsonValue.String(ParseString(json, ref index));
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            if (c == '-' || char.IsDigit(c)) return JsonValue.Number(ParseNumber(json, ref index));
            throw new InvalidOperationException($"Unexpected character '{c}'.");
        }

        private static JsonValue ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (Peek(json, index) == '}')
            {
                index++;
                return JsonValue.Object(dict);
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                var val = ParseValue(json, ref index);
                dict[key] = val;
                SkipWhitespace(json, ref index);
                char next = Peek(json, index);
                if (next == ',')
                {
                    index++;
                    continue;
                }
                if (next == '}')
                {
                    index++;
                    break;
                }
                throw new InvalidOperationException("Expected ',' or '}'.");
            }

            return JsonValue.Object(dict);
        }

        private static JsonValue ParseArray(string json, ref int index)
        {
            var list = new List<JsonValue>();
            Expect(json, ref index, '[');
            SkipWhitespace(json, ref index);
            if (Peek(json, index) == ']')
            {
                index++;
                return JsonValue.Array(list);
            }

            while (true)
            {
                var val = ParseValue(json, ref index);
                list.Add(val);
                SkipWhitespace(json, ref index);
                char next = Peek(json, index);
                if (next == ',')
                {
                    index++;
                    continue;
                }
                if (next == ']')
                {
                    index++;
                    break;
                }
                throw new InvalidOperationException("Expected ',' or ']'.");
            }

            return JsonValue.Array(list);
        }

        private static string ParseString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var sb = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    if (index >= json.Length) throw new InvalidOperationException("Invalid escape.");
                    char esc = json[index++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 > json.Length) throw new InvalidOperationException("Invalid unicode escape.");
                            string hex = json.Substring(index, 4);
                            if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                throw new InvalidOperationException("Invalid unicode escape.");
                            }
                            sb.Append((char)code);
                            index += 4;
                            break;
                        default:
                            throw new InvalidOperationException("Invalid escape character.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new InvalidOperationException("Unterminated string.");
        }

        private static int ParseNumber(string json, ref int index)
        {
            int start = index;
            if (json[index] == '-') index++;
            while (index < json.Length && char.IsDigit(json[index])) index++;
            if (start == index) throw new InvalidOperationException("Invalid number.");
            string segment = json.Substring(start, index - start);
            if (!int.TryParse(segment, out int value))
            {
                throw new InvalidOperationException("Invalid number.");
            }
            return value;
        }

        private static JsonValue ParseBool(string json, ref int index)
        {
            if (json.Substring(index).StartsWith("true", StringComparison.Ordinal))
            {
                index += 4;
                return JsonValue.Bool(true);
            }
            if (json.Substring(index).StartsWith("false", StringComparison.Ordinal))
            {
                index += 5;
                return JsonValue.Bool(false);
            }
            throw new InvalidOperationException("Invalid boolean.");
        }

        private static JsonValue ParseNull(string json, ref int index)
        {
            if (json.Substring(index).StartsWith("null", StringComparison.Ordinal))
            {
                index += 4;
                return JsonValue.Null();
            }
            throw new InvalidOperationException("Invalid null.");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index];
                if (!char.IsWhiteSpace(c)) break;
                index++;
            }
        }

        private static void Expect(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
            {
                throw new InvalidOperationException($"Expected '{expected}'.");
            }
            index++;
        }

        private static char Peek(string json, int index)
        {
            if (index >= json.Length) throw new InvalidOperationException("Unexpected end of JSON.");
            return json[index];
        }
    }
}
