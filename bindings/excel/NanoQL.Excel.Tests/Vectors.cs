// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The committed repro vectors (<c>bindings/excel/tests/repro/</c>, XLR14): a vector's calls made again on the test host
/// through the experimental tests' build, each call in a cell of its own as its workbook held it, with the vector's date
/// system, its conventions through the settings file and its read files in this checkout; and XLD30's two substitutions,
/// which leave the generator's version and the checkout's own path out of a comparison.
/// </summary>
internal static partial class Vectors
{
    private const string Files = "\\bindings\\excel\\tests\\repro\\workbooks\\files\\";

    // The add-in's own functions (nqlxl), which every build carries beside its generated ones.
    private static readonly Dictionary<string, MethodInfo> Own = typeof(OwnFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => m.GetCustomAttribute<ExcelFunctionAttribute>() != null)
        .ToDictionary(m => m.GetCustomAttribute<ExcelFunctionAttribute>()!.Name, StringComparer.Ordinal);

    /// <summary>Gets the committed vectors' folder.</summary>
    public static string Folder => Path.Combine(ErrorTests.Repository(), "bindings", "excel", "tests", "repro");

    /// <summary>The committed vectors, by name.</summary>
    /// <returns>Each vector's name and text.</returns>
    public static List<(string Name, string Text)> All()
        => [.. Directory.GetFiles(Folder, "*.json").Order(StringComparer.Ordinal).Select(p => (Path.GetFileNameWithoutExtension(p), File.ReadAllText(p)))];

    /// <summary>A vector's text with its generator's version and the checkout it was exported in replaced (XLD30).</summary>
    /// <param name="text">The vector.</param>
    /// <returns>The text compared.</returns>
    public static string Normal(string text)
    {
        string root = Root(text) is { } r ? r : ErrorTests.Repository();
        return Generator().Replace(text, "\"generator\": \"nanoql-excel <version>\"").Replace(Escaped(root) + "\\\\bindings", "<checkout>\\\\bindings", StringComparison.Ordinal);
    }

    /// <summary>Makes a vector's calls on a reset runtime, each in a cell of row 0, the n-th call's in column 8n.</summary>
    /// <param name="text">The vector.</param>
    /// <returns>The root call's cell.</returns>
    public static ExcelReference Make(string text)
    {
        using JsonDocument doc = JsonDocument.Parse(text);
        JsonElement inputs = doc.RootElement.GetProperty("cases")[0].GetProperty("inputs");
        Host.Shift = inputs.GetProperty("dateSystem").GetInt32() == 1904 ? 1462 : 0;
        if (inputs.TryGetProperty("conventions", out JsonElement conventions))
        {
            // The conventions as a desk's settings file names them, so the provider's identity is the file's.
            string dir = Path.Combine(Path.GetTempPath(), "nanoql-excel-vectors-" + Environment.ProcessId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "desk.xml"), conventions.GetString()!);
            File.WriteAllText(Path.Combine(dir, "nanoql64.settings.json"), "{\"conventions\": \"desk.xml\"}");
            XlSettings.Load(Path.Combine(dir, "nanoql64.xll"), null);
        }

        string? root = Root(text);
        var made = new Dictionary<string, object>(StringComparer.Ordinal);
        var cells = new Dictionary<string, ExcelReference>(StringComparer.Ordinal);
        object Arg(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Null => ExcelMissing.Value,
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when root != null && e.GetString()!.StartsWith(root + Files, StringComparison.OrdinalIgnoreCase)
                => ErrorTests.Repository() + e.GetString()![root.Length..],
            JsonValueKind.String => e.GetString()!,
            JsonValueKind.Object when e.TryGetProperty("item", out JsonElement it) => Item((object[,])made[e.GetProperty("ref").GetString()!], it.GetInt32()),
            JsonValueKind.Object when e.TryGetProperty("ref", out JsonElement rf) => made[rf.GetString()!],
            JsonValueKind.Object when e.TryGetProperty("empty", out _) => ExcelEmpty.Value,
            JsonValueKind.Object when e.TryGetProperty("rows", out JsonElement rows) => Range(e, rows.GetInt32(), e.GetProperty("cols").GetInt32()),
            _ => throw new InvalidDataException("an argument the test doesn't make: " + e),
        };
        object Range(JsonElement e, int rows, int cols)
        {
            var o = new object[rows, cols];
            var all = e.GetProperty("cells").EnumerateArray().ToList();
            for (int i = 0; i < rows * cols; i++)
            {
                o[i / cols, i % cols] = Arg(all[i]);
            }

            return o;
        }

        int n = 0;
        foreach (JsonElement call in inputs.GetProperty("calls").EnumerateArray())
        {
            int column = 8 * n++;
            Host.Cell = TestHost.At(0, column);
            string fn = call.GetProperty("fn").GetString()!;
            object[] args = [.. call.GetProperty("args").EnumerateArray().Select(Arg)];
            object result = Own.TryGetValue(fn, out MethodInfo? own)
                ? own.Invoke(null, [.. args, .. Enumerable.Repeat<object>(ExcelMissing.Value, own.GetParameters().Length - args.Length)])!
                : Call(fn, args);
            if (result is object[,] { Length: > 1 } m)
            {
                Host.Spill(0, column, m);
            }
            else
            {
                Host.Show(0, column, result is object[,] one ? one[0, 0] : result);
            }

            made[call.GetProperty("id").GetString()!] = result;
            cells[call.GetProperty("id").GetString()!] = TestHost.At(0, column);
        }

        return cells[inputs.GetProperty("result").GetString()!];
    }

    /// <summary>A cell's export as its lines' text, each line ended, or the refusal's message.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="format">The format.</param>
    /// <returns>The text, or "refused: " and the message.</returns>
    public static string Export(ExcelReference cell, string format = "vector")
    {
        Host.Cell = TestHost.At(200, 0);
        object r = OwnFunctions.Repro(cell, format);
        return r is object[,] lines ? string.Concat(lines.Cast<string>().Select(l => l + "\n")) : "refused: " + Message();
    }

    // An object inside a returned array, by its item: the spill read by rows.
    private static object Item(object[,] m, int item) => m[item / m.GetLength(1), item % m.GetLength(1)];

    // The checkout a vector reading files was exported in: the folder above its files' bindings path.
    private static string? Root(string text)
    {
        Match m = FilesPath().Match(text);
        return m.Success ? m.Groups[1].Value.Replace("\\\\", "\\", StringComparison.Ordinal) : null;
    }

    private static string Escaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    [GeneratedRegex("\"generator\": \"nanoql-excel [^\"]*\"")]
    private static partial Regex Generator();

    [GeneratedRegex("\"([A-Za-z]:(?:\\\\\\\\[^\"\\\\]+)*?)\\\\\\\\bindings\\\\\\\\excel\\\\\\\\tests\\\\\\\\repro\\\\\\\\workbooks\\\\\\\\files\\\\\\\\")]
    private static partial Regex FilesPath();
}
