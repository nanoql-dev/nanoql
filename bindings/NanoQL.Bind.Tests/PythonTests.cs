// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The Python back end: PEP 8's names, the kinds' tokens, and the overload rule, that no two forms of a group can take
/// one call exactly (§2.2).
/// </summary>
public class PythonTests
{
    [Theory]
    [InlineData("FairRate", "fair_rate")]
    [InlineData("NPV", "npv")]
    [InlineData("ATMRate", "atm_rate")]
    [InlineData("YoYInflationIndex", "yoy_inflation_index")]
    [InlineData("IsBusinessDay", "is_business_day")]
    [InlineData("Black76", "black76")]
    [InlineData("lambda", "lambda_")]
    [InlineData("Close", "close_")]
    public void NamesArePep8sSnakeCase(string name, string snake) => Assert.Equal(snake, PythonWriter.Snake(name));

    [Fact]
    public void AKindsTokenNamesTheClassItsObjectOrEnumTakes()
    {
        var w = new PythonWriter(Surfaces.Nanoql.Surface);
        CFunction ctor = Surfaces.Nanoql.Surface.Functions.Single(f => f.CName == "nql_FlatForward_new");
        Assert.Equal(["date", "double", "handle@nql.DayCounter", "enum@nql.Compounding", "enum@nql.Frequency"], ctor.Params.Select(p => w.Token(p.Kind)));
        CFunction map = Surfaces.Nanoql.Surface.Functions.First(f => f.Params.Any(p => p.Kind.Text == "map:enum:double"));
        Assert.StartsWith("map:enum@nql.", w.Token(map.Params.First(p => p.Kind.Text == "map:enum:double").Kind));
    }

    [Fact]
    public void NoTwoFormsOfAGroupTakeOneCallExactly()
    {
        var w = new PythonWriter(Surfaces.Nanoql.Surface);
        int groups = 0, checkedForms = 0;
        foreach (IGrouping<Type, CFunction> type in Surfaces.Nanoql.Surface.Functions.GroupBy(f => f.Type))
        {
            foreach (IGrouping<string, CFunction> group in w.Groups(type).Where(g => g.Count() > 1))
            {
                groups++;
                List<CFunction> forms = [.. group.OrderBy(f => f.Params.Count).ThenBy(f => f.CName, StringComparer.Ordinal)];
                List<List<int>> offered = w.Offered(forms);
                var seen = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < forms.Count; i++)
                {
                    foreach (int n in offered[i])
                    {
                        string call = string.Join(",", forms[i].Params.Take(n).Select(p => w.Token(p.Kind)));
                        Assert.False(seen.TryGetValue(call, out string? other), $"{forms[i].CName} and {other} both take ({call}) exactly");
                        seen[call] = forms[i].CName;
                        checkedForms++;
                    }
                }
            }
        }

        Assert.True(groups > 500, $"{groups} overload groups");
        Assert.True(checkedForms > groups * 2);
    }

    [Fact]
    public void AFormIsntOfferedAtACountAnotherFormsWholeSignatureTakes()
    {
        var w = new PythonWriter(Surfaces.Nanoql.Surface);
        List<CFunction> forms = [.. Surfaces.Nanoql.Surface.Functions.Where(f => f.CName.StartsWith("nql_AnalyticHestonEngine_Calculate__VanillaOption_EvaluationContext_HestonModel", StringComparison.Ordinal))
            .OrderBy(f => f.Params.Count).ThenBy(f => f.CName, StringComparer.Ordinal)];
        List<List<int>> offered = w.Offered(forms);
        Assert.Equal([3, 4], offered[0]);
        Assert.DoesNotContain(3, offered.Skip(1).SelectMany(o => o));
    }

    [Fact]
    public void ThePackagesModulesAreWritten()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-python-" + Guid.NewGuid().ToString("N"));
        try
        {
            (int functions, int classes, int enums) = Surfaces.Nanoql.WritePython(dir);
            Assert.Equal(Surfaces.Nanoql.Surface.Functions.Count, functions);
            Assert.True(classes > 1500 && enums == Surfaces.Nanoql.Surface.Enums.Count);
            string stable = File.ReadAllText(Path.Combine(dir, "_generated.py"));
            Assert.Contains("class FlatForward(*_bases(YieldTermStructure)):", stable, StringComparison.Ordinal);
            Assert.Contains("\"nql_FlatForward_new\": ((\"date\", \"double\", \"handle@nql.DayCounter\", \"enum@nql.Compounding\", \"enum@nql.Frequency\", ), \"new\"),", stable, StringComparison.Ordinal);
            Assert.Contains("    _key = \"nql.FlatForward\"", stable, StringComparison.Ordinal);
            Assert.Contains("    @_overload", stable, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', stable);
            string experimental = File.ReadAllText(Path.Combine(dir, "experimental", "__init__.py"));
            Assert.Contains("_attach(_g.", experimental, StringComparison.Ordinal);

            // No module-level loop binds a name a module alias may take.
            Assert.DoesNotContain("for _n", experimental, StringComparison.Ordinal);
            Assert.DoesNotContain("for _n", stable, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AUsersAssemblyGetsAPackageOfItsOwnOverNanoqls()
    {
        string root = Path.Combine(Path.GetTempPath(), "nanoql-bind-python-" + Guid.NewGuid().ToString("N"));
        try
        {
            Surfaces.WithFixtures.WritePython(Path.Combine(root, "nanoql"));
            string nanoql = File.ReadAllText(Path.Combine(root, "nanoql", "_generated.py"));
            string fx = File.ReadAllText(Path.Combine(root, "fx", "_generated.py"));
            Assert.DoesNotContain("class UserCalibratedCurve", nanoql, StringComparison.Ordinal);
            Assert.DoesNotMatch("\"fx_[A-Z]", nanoql);
            Assert.Contains("class UserCalibratedCurve(*_bases(_n.YieldTermStructure, _n.ICalibratedModel)):", fx, StringComparison.Ordinal);

            // A user's class named like one of nanoql's: its own key, and nanoql's kinds still name nanoql's.
            Assert.Contains("class Calendar(*_bases(Object)):\n    __slots__ = ()\n    _type = \"NanoQL.Bind.Tests.Fixtures.Calendar\"\n    _key = \"fx.Calendar\"", fx, StringComparison.Ordinal);
            Assert.Contains("\"handle@fx.Calendar\"", fx, StringComparison.Ordinal);
            Assert.Contains("\"handle@nql.Calendar\"", fx, StringComparison.Ordinal);
            Assert.DoesNotContain("\"handle@Calendar\"", nanoql, StringComparison.Ordinal);
            Assert.Contains("def holds(calendar: _n.Calendar | None, d: datetime.date) -> bool:", fx, StringComparison.Ordinal);

            // A user's class over nanoql's experimental interface is experimental: in its package's experimental
            // module, which imports nanoql's, and not in its stable one, which doesn't.
            string fxExperimental = File.ReadAllText(Path.Combine(root, "fx", "experimental", "__init__.py"));
            Assert.DoesNotContain("class Walk(", fx, StringComparison.Ordinal);
            Assert.DoesNotContain("_nx", fx, StringComparison.Ordinal);
            Assert.Contains("class Walk(*_bases(_nx.IRandomWalkDistribution)):", fxExperimental, StringComparison.Ordinal);
            Assert.Contains("import nanoql.experimental as _nx", fxExperimental, StringComparison.Ordinal);

            // A stable class's member returning that class is experimental too: added to the class on the opt-in.
            Assert.DoesNotContain("def make_walk", fx, StringComparison.Ordinal);
            Assert.Contains("def make_walk() -> Walk | None:", fxExperimental, StringComparison.Ordinal);
            Assert.Contains("from nanoql._generated import *", fx, StringComparison.Ordinal);
            Assert.DoesNotContain("\"nql_", fx, StringComparison.Ordinal);
            Assert.DoesNotContain("class FlatForward(", fx, StringComparison.Ordinal);
            Assert.Contains("_nanoql.use_library(_LIBRARY)", File.ReadAllText(Path.Combine(root, "fx", "__init__.py")), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, "fx", "experimental", "__init__.py")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NoPythonMemberHoldsTwoCSharpMembers()
    {
        var w = new PythonWriter(Surfaces.Nanoql.Surface);
        foreach (IGrouping<Type, CFunction> type in Surfaces.Nanoql.Surface.Functions.GroupBy(f => f.Type))
        {
            foreach (IGrouping<string, CFunction> group in w.Groups(type))
            {
                Assert.Single(group.Select(PythonWriter.MemberOf).Distinct());
            }
        }

        // A record's init property and a method of one snake name: the method keeps it, the property takes _init.
        var settings = w.Groups(Surfaces.Nanoql.Surface.Functions.Where(f => f.TypeName == "LinearTsrPricerSettings")).ToDictionary(g => g.Key, g => g.ToList());
        Assert.All(settings["with_price_threshold"], f => Assert.Equal("method", f.Kind));

        // A struct's implicit constructor is a form of its constructor, not another member.
        var loss = w.Groups(Surfaces.Nanoql.Surface.Functions.Where(f => f.TypeName == "Loss")).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Contains(loss["__init__"], f => f.Member == null);
        Assert.Contains(loss["__init__"], f => f.Member != null);
        Assert.Equal("with", settings["with_price_threshold_init"].Single().Kind);
    }
}
