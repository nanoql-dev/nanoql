// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using ExcelDna.Integration;
using NanoQL.Extensions.Conventions;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The settings file: <c>&lt;xll name&gt;.settings.json</c> beside the add-in, its keys checked, a failed load never
/// falling back to QuantLib's conventions; the refusing provider; the set's empty argument.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class SettingsTests : IDisposable
{
    // A desk's TARGET, with an extra holiday (docs/conventions.md's example).
    private const string Desk = "<Conventions xmlns=\"https://nanoql.org/schemas/conventions\">\n  <Calendar name=\"TARGET\" base=\"TARGET\">\n    <AddedHoliday>2026-12-24</AddedHoliday>\n  </Calendar>\n</Conventions>\n";

    private readonly string dir = Path.Combine(Path.GetTempPath(), "nanoql-excel-settings-" + Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a new instance of the <see cref="SettingsTests"/> class: a clean runtime and an add-in's folder.</summary>
    public SettingsTests()
    {
        Reset();
        Directory.CreateDirectory(dir);
    }

    private string Xll => Path.Combine(dir, "nanoql64.xll");

    /// <inheritdoc/>
    public void Dispose()
    {
        Reset();
        Directory.Delete(dir, true);
    }

    /// <summary>No settings file: QuantLib's conventions, calls kept by their cells.</summary>
    [Fact]
    public void NoFileIsQuantLibs()
    {
        XlSettings.Load(Xll, null);
        Assert.Null(XlSettings.LoadError);
        Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Default);
        Assert.Equal("QuantLib", X.ProviderId);
        Assert.StartsWith("CreditDefaultSwap#", Cds() as string, StringComparison.Ordinal);
    }

    /// <summary>A conventions file, relative to the settings file's folder, loads as the default, its content's hash the provider.</summary>
    [Fact]
    public void ADesksConventionsLoad()
    {
        Directory.CreateDirectory(Path.Combine(dir, "conv"));
        File.WriteAllText(Path.Combine(dir, "conv", "desk.xml"), Desk);
        File.WriteAllText(Path.Combine(dir, "nanoql64.settings.json"), "{\"conventions\": \"conv/desk.xml\", \"recordCalls\": false}");
        XlSettings.Load(Xll, null);
        Assert.Null(XlSettings.LoadError);
        Assert.StartsWith("file:", X.ProviderId, StringComparison.Ordinal);
        Assert.False(X.RecordCalls);

        // The desk's TARGET by name: 2026-12-24 a holiday.
        double eve = new NanoQL.Time.Date(24, NanoQL.Time.Month.December, 2026).SerialNumber;
        Assert.Equal(false, Call("nqlCalendarIsBusinessDay", "TARGET", eve));
    }

    /// <summary>The environment variable names the file, a relative path against the add-in's folder, over the file's key.</summary>
    [Fact]
    public void TheEnvironmentNamesTheFile()
    {
        File.WriteAllText(Path.Combine(dir, "desk.xml"), Desk);
        XlSettings.Load(Xll, "desk.xml");
        Assert.Null(XlSettings.LoadError);
        Assert.StartsWith("file:", X.ProviderId, StringComparison.Ordinal);
    }

    /// <summary>A missing file, a malformed one, an unknown key, a key in another case, a wrong type: each a failed load naming why.</summary>
    [Theory]
    [InlineData("{\"conventions\": \"missing.xml\"}", "missing.xml")]
    [InlineData("{\"conventions\": \"bad.xml\"}", "bad.xml")]
    [InlineData("{\"convention\": \"desk.xml\"}", "\"convention\"")]
    [InlineData("{\"Conventions\": \"desk.xml\"}", "\"Conventions\"")]
    [InlineData("{\"recordCalls\": \"yes\"}", "\"recordCalls\"")]
    [InlineData("[1]", "isn't a JSON object")]
    public void AFailedLoadRefuses(string settings, string named)
    {
        File.WriteAllText(Path.Combine(dir, "bad.xml"), "<Conventions xmlns=\"https://nanoql.org/schemas/conventions\"><Nonsense/></Conventions>");
        File.WriteAllText(Path.Combine(dir, "nanoql64.settings.json"), settings);
        XlSettings.Load(Xll, null);
        Assert.NotNull(XlSettings.LoadError);
        Assert.Contains(named, XlSettings.LoadError, StringComparison.Ordinal);
        Assert.Equal("failed", X.ProviderId);

        // Every function reading the default conventions refuses with the load's error; an explicit set still works.
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionSetCalendar", null, "TARGET"));
        Assert.Contains("didn't load", Message(), StringComparison.Ordinal);
        string set = H(Call("nqlConventionSetQuantLib"));
        H(Call("nqlConventionSetCalendar", set, "TARGET"));

        // A member reading the default conventions inside nanoql refuses too, never priced on QuantLib's.
        Assert.Equal(ExcelError.ExcelErrorValue, Cds());
        Assert.Contains("didn't load", Message(), StringComparison.Ordinal);
    }

    // A credit default swap's builder, which reads its defaults from the default conventions.
    private static object Cds()
    {
        object builder = Call("nqlMakeCreditDefaultSwap", "5Y", 0.01);
        return builder is string b ? Call("nqlMakeCreditDefaultSwapToCreditDefaultSwap", b, H(Call("nqlEvaluationContext", 42262.0))) : builder;
    }

    /// <summary>The refusing provider overrides every overridable member of the convention provider.</summary>
    [Fact]
    public void TheRefusingProviderOverridesEveryMember()
    {
        var overridable = typeof(ConventionProvider).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.IsVirtual && !m.IsFinal && m.DeclaringType == typeof(ConventionProvider)).Select(m => m.Name).ToHashSet();
        var overridden = typeof(RefusingProvider).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetBaseDefinition().DeclaringType == typeof(ConventionProvider)).Select(m => m.Name).ToHashSet();
        Assert.True(overridable.Count >= 19);
        Assert.Empty(overridable.Except(overridden));
    }

    /// <summary>A relative path in a cell is refused.</summary>
    [Fact]
    public void ARelativePathInACellIsRefused()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionsXmlRead", "desk.xml"));
        Assert.Contains("relative path", Message(), StringComparison.Ordinal);
    }

    /// <summary>The set's empty argument reads the current provider as the native host reads a NULL set.</summary>
    [Fact]
    public void TheEmptySetArgument()
    {
        Assert.Same(ConventionSet.QuantLib, X.SelfSet(ExcelMissing.Value));
        ConventionSet desk = ConventionSet.QuantLib.With(ConventionsXml.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Desk))));
        using (ConventionProvider.Use(desk))
        {
            Assert.Same(desk, X.SelfSet(ExcelMissing.Value));
        }
    }
}
