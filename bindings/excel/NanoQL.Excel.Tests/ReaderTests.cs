// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The file readers (XLR3): a call reading files keys their content beside their paths, so an edited file is a new
/// object; the single-path read parses the bytes its key hashed, the multi-path read refuses a file changed between its
/// read and its second hash; one content from two paths two objects, each reporting its own path (B5-1).
/// </summary>
[Collection(TestSettings.Name)]
public sealed class ReaderTests : IDisposable
{
    private const string Desk = "<Conventions xmlns=\"https://nanoql.org/schemas/conventions\">\n  <Calendar name=\"TARGET\" base=\"TARGET\">\n    <AddedHoliday>2026-12-24</AddedHoliday>\n  </Calendar>\n</Conventions>\n";
    private const string Desk2 = "<Conventions xmlns=\"https://nanoql.org/schemas/conventions\">\n  <Calendar name=\"TARGET\" base=\"TARGET\">\n    <AddedHoliday>2026-12-23</AddedHoliday>\n  </Calendar>\n</Conventions>\n";

    private readonly string dir = Path.Combine(Path.GetTempPath(), "nanoql-excel-readers-" + Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a new instance of the <see cref="ReaderTests"/> class: a clean runtime and a folder of files.</summary>
    public ReaderTests()
    {
        Reset();
        Directory.CreateDirectory(dir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        X.BetweenReadAndCheck = null;
        Directory.Delete(dir, true);
    }

    /// <summary>An edited file, read again by the same cell's call, is a new object; the unedited one the same.</summary>
    [Fact]
    public void AnEditedFileIsANewHandle()
    {
        string path = Write("a.xml", Desk);
        string a = H(Call("nqlConventionsXmlRead", path));
        Assert.Equal(a, H(Call("nqlConventionsXmlRead", path)));
        Write("a.xml", Desk2);
        string b = H(Call("nqlConventionsXmlRead", path));
        Assert.NotEqual(a, b);
        string multi = H(Call("nqlConventionsXmlRead2", Col(path)));
        Write("a.xml", Desk);
        Assert.NotEqual(multi, H(Call("nqlConventionsXmlRead2", Col(path))));
    }

    /// <summary>The multi-path read refuses a file changed between its read and its second hash.</summary>
    [Fact]
    public void AFileChangedDuringTheReadIsRefused()
    {
        string path = Write("b.xml", Desk);
        X.BetweenReadAndCheck = () => File.WriteAllText(path, Desk2);
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionsXmlRead2", Col(path)));
        Assert.Contains("changed while it was read", Message(), StringComparison.Ordinal);
    }

    /// <summary>One content from two paths: two handles, each snapshot reporting its own path (B5-1).</summary>
    [Fact]
    public void OneContentFromTwoPathsIsTwoHandles()
    {
        string p1 = Write("c.xml", Desk), p2 = Write("d.xml", Desk);
        string a = H(Call("nqlConventionsXmlRead", p1)), b = H(Call("nqlConventionsXmlRead", p2));
        Assert.NotEqual(a, b);
        Assert.Contains(p1, Show(Call("nqlConventionSnapshotSources", a)), StringComparison.Ordinal);
        Assert.Contains(p2, Show(Call("nqlConventionSnapshotSources", b)), StringComparison.Ordinal);
    }

    /// <summary>A malformed file is #VALUE! (BIR4's 13), its message by the cell (XLP56).</summary>
    [Fact]
    public void AMalformedFileIsValue()
    {
        string path = Write("e.xml", "<Conventions xmlns=\"https://nanoql.org/schemas/conventions\"><Nonsense/></Conventions>");
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionsXmlRead", path));
        Assert.NotEqual(string.Empty, Message());
    }

    private string Write(string name, string text)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, text);
        return path;
    }
}
