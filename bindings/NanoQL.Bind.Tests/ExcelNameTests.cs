// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;
using static NanoQL.Bind.Tests.ExcelSurfaces;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The Excel names (XLR1): words run together after the prefix, overloads and collisions numbered in the stated order
/// over the whole filtered surface, one numbering for both builds and the list on or off, released names frozen.
/// </summary>
public class ExcelNameTests
{
    /// <summary>Metadata order, not the C names': each class pinned (XLP36, XLP52).</summary>
    [Fact]
    public void TheStatedOrderNumbersEachGroup()
    {
        ExcelOutput x = WholeExperimental;
        Assert.Equal("nqlCalendarAdvance", NameOf(x, "nql_Calendar_Advance__Date_int"));
        Assert.Equal("nqlCalendarAdvance2", NameOf(x, "nql_Calendar_Advance__Date_Period"));
        Assert.Equal("nqlDiscountingSwapEngineCalculate", NameOf(x, "nql_DiscountingSwapEngine_Calculate__Swap"));
        Assert.Equal("nqlDiscountingSwapEngineCalculate2", NameOf(x, "nql_DiscountingSwapEngine_Calculate__FixedVsFloatingSwap"));
        Assert.Equal("nqlDiscountingSwapEngineCalculate4", NameOf(x, "nql_DiscountingSwapEngine_Calculate__BMASwap"));

        // A bindon group by its input sets' index; C-name order would put the complex-log formula's first.
        Assert.Equal("nqlAnalyticHestonEngineBindOn", NameOf(x, "nql_AnalyticHestonEngine_BindOn__Instrument_HestonModel_int"));
        Assert.Equal("nqlAnalyticHestonEngineBindOn2", NameOf(x, "nql_AnalyticHestonEngine_BindOn__Instrument_HestonModel_double"));
        Assert.Equal("nqlAnalyticHestonEngineBindOn3", NameOf(x, "nql_AnalyticHestonEngine_BindOn__Instrument_HestonModel_AnalyticHestonEngineComplexLogFormula"));

        // A group across declaring types: the type's own Create before its generic base's.
        Assert.Equal("nqlLinearInterpolatedSmileSectionCreate", NameOf(x, "nql_LinearInterpolatedSmileSection_Create__Date_doubleArray_doubleArray_Optdouble_EvaluationContext_DayCounter_VolatilityType"));
        Assert.Equal("nqlLinearInterpolatedSmileSectionCreate2", NameOf(x, "nql_LinearInterpolatedSmileSection_Create__Date_doubleArray_doubleArray_Optdouble_EvaluationContext_DayCounter_Linear"));
    }

    /// <summary>A draws function numbers after its type's members, a bind after its members (XLR1's order), synthesized.</summary>
    [Fact]
    public void DrawsNumberLast()
    {
        CFunction draws = Fn("Draws", "draws", typeof(Fixtures.UserRng), null);
        CFunction method = Fn("Draws", "method", typeof(Fixtures.UserRng), typeof(Fixtures.UserRng).GetMethod("Clone"));
        var names = ExcelNames.Number([draws, method], new Dictionary<CFunction, List<CFunction>>(), ExcelShipped.Read(null));
        Assert.Equal("fxUserRngDraws", names[method]);
        Assert.Equal("fxUserRngDraws2", names[draws]);
    }

    /// <summary>XLD31: a struct's implicit default constructor withheld beside a declared maker, kept as its type's only one.</summary>
    [Fact]
    public void AStructsDefaultConstructorBesideADeclaredMaker()
    {
        ExcelOutput x = WholeExperimental;
        Assert.Null(NameOf(x, "nql_Cubic_new__void"));
        Assert.Equal("nqlCubic", NameOf(x, "nql_Cubic_new__CubicInterpolationDerivativeApprox"));
        Assert.Equal("nqlLinear", NameOf(x, "nql_Linear_new"));
    }

    /// <summary>A constructor with no member or token in a numbered group is refused (B4-1), synthesized.</summary>
    [Fact]
    public void ATokenlessConstructorInANumberedGroupIsRefused()
    {
        CFunction tokenless = Fn("new", "new", typeof(DateTime), null);
        tokenless.TypeName = "Toke";
        CFunction declared = Fn("new", "new", typeof(Fixtures.UserRng), typeof(Fixtures.UserRng).GetConstructors()[0]);
        declared.TypeName = "TOKE";
        var e = Assert.Throws<BindException>(() => ExcelNames.Number([tokenless, declared], new Dictionary<CFunction, List<CFunction>>(), ExcelShipped.Read(null)));
        Assert.Contains("no member or metadata token in the numbered group fxToke", e.Message, StringComparison.Ordinal);
    }

    /// <summary>A generated name meeting the add-in's own regardless of case is refused (XLR13), synthesized.</summary>
    [Fact]
    public void ANameMeetingTheAddInsOwnIsRefused()
    {
        CFunction f = Fn("new", "new", typeof(Fixtures.UserRng), typeof(Fixtures.UserRng).GetConstructors()[0]);
        f.Prefix = "nql";
        f.TypeName = "XlError";
        var e = Assert.Throws<BindException>(() => ExcelNames.Number([f], new Dictionary<CFunction, List<CFunction>>(), ExcelShipped.Read(null)));
        Assert.Contains("meets the add-in's own nqlxlError", e.Message, StringComparison.Ordinal);
    }

    /// <summary>One numbering: the stable build's names are the experimental build's, the list on or off (XLR1, XLP84).</summary>
    [Fact]
    public void OneNumberingForEveryBuild()
    {
        var whole = WholeExperimental.Selection.Units.SelectMany(u => u.Functions.Select(f => (f.CName, u.Name))).ToDictionary(p => p.CName, p => p.Name);
        foreach (ExcelOutput x in new[] { WholeStable, ListedExperimental, ListedStable })
        {
            Assert.All(x.Selection.Units, u => Assert.Equal(whole[u.Function.CName], u.Name));
        }
    }

    /// <summary>A user prefix's names: <c>fx</c> and the type's and member's words.</summary>
    [Fact]
    public void AUserPrefixNamesItsFunctions()
    {
        Assert.Equal("fxSquare", NameOf(Users, "fx_Square_new"));
        Assert.Equal("fxSquareLabel", NameOf(Users, "fx_Square_get_Label"));
    }

    /// <summary>Released names stay: an overload added to a released group takes its lowest unused number; a retired name is never reused (XLR1).</summary>
    [Fact]
    public void ReleasedNamesStayAndRetiredOnesAreNeverReused()
    {
        string shipped = Path.Combine(Path.GetTempPath(), "nanoql-excel-shipped-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(shipped, """
            {"functions": [
              {"excel": "nqlCalendarAdvance", "c": "nql_Calendar_Advance__Date_Period"}
            ],
             "retired": ["nqlCalendarAdvance2"]}
            """);
        try
        {
            ExcelOutput x = Run(true, Nanoql(), shipped: shipped);
            Assert.Equal("nqlCalendarAdvance", NameOf(x, "nql_Calendar_Advance__Date_Period"));
            Assert.Equal("nqlCalendarAdvance3", NameOf(x, "nql_Calendar_Advance__Date_int"));
        }
        finally
        {
            File.Delete(shipped);
        }
    }

    private static CFunction Fn(string baseName, string kind, Type type, System.Reflection.MemberInfo? member) => new()
    {
        Prefix = "fx",
        TypeName = type.Name,
        BaseName = baseName,
        Kind = kind,
        Type = type,
        Holder = type,
        Member = member,
        CName = $"fx_{type.Name}_{baseName}_{kind}",
    };
}
