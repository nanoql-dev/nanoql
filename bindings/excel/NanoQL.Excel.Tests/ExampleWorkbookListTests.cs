// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NanoQL.Excel.Tests;

/// <summary>The example workbooks in bindings/excel/examples/ call only the stable add-in's functions: their formulas
/// read from the files, outside Excel, against functions.json. The in-Excel suite recalculates them.</summary>
public sealed partial class ExampleWorkbookListTests
{
    /// <summary>Each workbook's sheets and defined names call nanoql's functions, every one listed and stable.</summary>
    [Fact]
    public void EveryFunctionTheWorkbooksCallIsListedAndStable()
    {
        string root = ErrorTests.Repository();
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "bindings", "excel", "functions.json")));
        HashSet<string> stable = [.. doc.RootElement.GetProperty("functions").EnumerateArray()
            .Where(f => !f.GetProperty("experimental").GetBoolean())
            .Select(f => f.GetProperty("excel").GetString()!)];
        string[] workbooks = Directory.GetFiles(Path.Combine(root, "bindings", "excel", "examples"), "*.xlsx");
        Assert.Equal(["bond.xlsx", "curve.xlsx", "option.xlsx", "sofr.xlsx", "swap.xlsx"], workbooks.Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (string path in workbooks)
        {
            var called = new SortedSet<string>(StringComparer.Ordinal);
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                foreach (ZipArchiveEntry part in zip.Entries.Where(e => e.FullName == "xl/workbook.xml" || e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)))
                {
                    using var reader = new StreamReader(part.Open());
                    string xml = reader.ReadToEnd();
                    foreach (Match formula in Formula().Matches(xml))
                    {
                        called.UnionWith(Call().Matches(WebUtility.HtmlDecode(formula.Groups[1].Value)).Select(m => m.Groups[1].Value));
                    }
                }
            }

            Assert.NotEmpty(called);
            Assert.DoesNotContain(called, name => !stable.Contains(name));
        }
    }

    /// <summary>No part of any workbook holds a folder: a saved value or property naming an absolute path, a share or a
    /// user's folder would ship with the add-in. The SOFR workbook reads its conventions file from its own folder inside
    /// a formula, never through a cell's value.</summary>
    [Fact]
    public void NoWorkbookHoldsAPathOrAUserFolder()
    {
        string[] workbooks = Directory.GetFiles(Path.Combine(ErrorTests.Repository(), "bindings", "excel", "examples"), "*.xlsx");
        Assert.NotEmpty(workbooks);
        foreach (string path in workbooks)
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            foreach (ZipArchiveEntry part in zip.Entries)
            {
                using var reader = new StreamReader(part.Open());
                Match found = Folder().Match(reader.ReadToEnd());
                Assert.False(found.Success, $"{Path.GetFileName(path)}'s {part.FullName} holds \"{found.Value}\"");
            }
        }
    }

    // A drive's path (not a URL's scheme), a share, a user's folder, a profile's.
    [GeneratedRegex(@"(?<![A-Za-z])[A-Za-z]:[\\/]|\\\\[A-Za-z]|[\\/](?:Users|home)[\\/]|AppData", RegexOptions.IgnoreCase)]
    private static partial Regex Folder();

    // A cell's formula or a defined name's: Excel saves an add-in's call as _xll.<name>(.
    [GeneratedRegex("<(?:f|definedName)\\b[^>]*>([^<]*)</(?:f|definedName)>")]
    private static partial Regex Formula();

    [GeneratedRegex("(?<![A-Za-z0-9_])(?:_xll\\.)?(nql[A-Za-z0-9_]*)\\s*\\(")]
    private static partial Regex Call();
}
