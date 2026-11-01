// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using NanoQL.Bind.Emitting;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The C++ back end: QuantLib's camelCase, the overload rule (no two forms of a class share a C++ signature, and none
/// takes a call another takes exactly, §2.2), no implicit conversion through a constructor, a user's header of its own,
/// and the dispatch reaching what C's does (BIR14).
/// </summary>
public class CppTests
{
    [Theory]
    [InlineData("FairRate", "fairRate")]
    [InlineData("NPV", "NPV")]
    [InlineData("IsBusinessDay", "isBusinessDay")]
    [InlineData("YoYInflationIndex", "yoyInflationIndex")]
    [InlineData("Advance", "advance")]
    public void NamesAreQuantLibsCamelCase(string name, string camel) => Assert.Equal(camel, CppWriter.Camel(name));

    [Fact]
    public void NoTwoFormsOfAClassShareACppSignature()
    {
        var w = new CppWriter(Surfaces.Nanoql.Surface);
        foreach (IGrouping<string, CppMember> group in w.Members.Values.GroupBy(m => m.Class + "|" + (m.Style == "new" ? "new" : m.Name)))
        {
            var signatures = group.Select(m => string.Join(",", m.Types.Select(Unqualified))).ToList();
            Assert.True(signatures.Count == signatures.Distinct().Count(), $"{group.Key}: two forms share a C++ signature");
        }

        Assert.True(w.Renamed > 0 && w.Renamed < 100, $"{w.Renamed} forms renamed");
    }

    [Fact]
    public void NoTwoFormsOfAGroupTakeOneCallExactly()
    {
        var w = new CppWriter(Surfaces.Nanoql.Surface);
        int offered = 0;
        foreach (IGrouping<string, CppMember> group in w.Members.Values.GroupBy(m => m.Class + "|" + (m.Style == "new" ? "new" : m.Name)))
        {
            var calls = new HashSet<string>(StringComparer.Ordinal);
            foreach (CppMember m in group)
            {
                for (int n = m.DefaultsFrom; n <= m.Types.Count; n++)
                {
                    offered++;
                    Assert.True(calls.Add(n + "|" + string.Join(",", m.Types.Take(n).Select(Unqualified))), $"{group.Key}: two forms take {n} arguments of one type each");
                }
            }
        }

        Assert.True(offered > 10000);
    }

    [Fact]
    public void EveryConstructorOneArgumentCanCallIsExplicit()
    {
        string h = Surfaces.Nanoql.CppHeaders()["nanoql.hpp"];
        string? cls = null;
        int explicitOnes = 0;
        foreach (string line in h.Split('\n'))
        {
            Match c = Regex.Match(line, @"^class (\w+) [:{]");
            if (c.Success)
            {
                cls = c.Groups[1].Value;
                continue;
            }

            Match k = Regex.Match(line, @"^    (explicit )?(\w+)\((.*)\);$");
            if (!k.Success || k.Groups[2].Value != cls || k.Groups[3].Value.Length == 0)
            {
                continue;
            }

            if (TopLevel(k.Groups[3].Value).Count(p => !p.Contains(" = ", StringComparison.Ordinal)) <= 1)
            {
                Assert.True(k.Groups[1].Success, $"{cls}: '{line.Trim()}' converts implicitly");
                explicitOnes++;
            }
        }

        Assert.True(explicitOnes > 150, $"{explicitOnes} explicit constructors");
    }

    [Fact]
    public void AUsersAssemblyGetsAHeaderOfItsOwnOverNanoqls()
    {
        Dictionary<string, string> headers = Surfaces.WithFixtures.CppHeaders();
        Assert.Equal(["fx.hpp", "nanoql.hpp"], headers.Keys.Order(StringComparer.Ordinal));
        string fx = headers["fx.hpp"];
        Assert.DoesNotContain("UserCalibratedCurve", headers["nanoql.hpp"], StringComparison.Ordinal);
        Assert.Contains("#include \"nanoql.hpp\"", fx, StringComparison.Ordinal);
        Assert.Contains("namespace fx {", fx, StringComparison.Ordinal);
        Assert.Contains("class UserCalibratedCurve : public virtual ::nanoql::YieldTermStructure, public virtual ::nanoql::ICalibratedModel {", fx, StringComparison.Ordinal);

        // A user's class named like one of nanoql's (B2-1): nanoql's named with its namespace in the user's header.
        Assert.Contains("class Calendar : public virtual Object {", fx, StringComparison.Ordinal);
        Assert.Contains("    static bool holds(const ::nanoql::Calendar& calendar, Date d);", fx, StringComparison.Ordinal);
        Assert.Contains("    std::string name() const;", fx, StringComparison.Ordinal);
        Assert.DoesNotContain("name_()", fx, StringComparison.Ordinal);
        Assert.Contains("#ifdef NANOQL_EXPERIMENTAL\nclass Walk : public virtual ::nanoql::IRandomWalkDistribution {", fx, StringComparison.Ordinal);
        Assert.Contains("    [[deprecated(\"use Kinds.Dates\")]]", fx, StringComparison.Ordinal);
        Assert.Contains("NANOQL_ALLOW_DEPRECATED_BEGIN_", fx, StringComparison.Ordinal);
        Assert.DoesNotContain("class FlatForward ", fx, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', fx);
    }

    [Fact]
    public void NanoqlsHeaderHoldsItsRuntimeValuesAndGuards()
    {
        string h = Surfaces.Nanoql.CppHeaders()["nanoql.hpp"];
        Assert.Contains("class Object {", h, StringComparison.Ordinal);
        Assert.Contains("class Date {", h, StringComparison.Ordinal);
        Assert.Contains("class Period {", h, StringComparison.Ordinal);
        Assert.Contains("class FlatForward : public virtual YieldTermStructure {", h, StringComparison.Ordinal);
        Assert.Contains("    BackwardFlat();", h, StringComparison.Ordinal);
        Assert.Contains("#ifdef NANOQL_EXPERIMENTAL", h, StringComparison.Ordinal);
        Assert.Contains("const C& detail::null_of()", h, StringComparison.Ordinal);
        Assert.DoesNotContain("// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0\n\n// The hand-written part", h, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDispatchReachesEveryFunctionCsReplays()
    {
        Generation g = Surfaces.Nanoql;
        Dictionary<string, string> files = g.CppDispatch();
        Assert.Equal(CppReplayWriter.Parts + 1, files.Count);
        int handlers = files.Where(f => f.Key != "dispatch_table.cpp").Sum(f => f.Value.Split('\n').Count(l => l.StartsWith("    {\"nql_", StringComparison.Ordinal)));
        int c = g.CDispatch.Split('\n').Count(l => l.StartsWith("    {\"nql_", StringComparison.Ordinal));
        Assert.Equal(c, handlers);
    }

    // A parameter list split at its top-level commas.
    private static List<string> TopLevel(string ps)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < ps.Length; i++)
        {
            depth += ps[i] is '<' or '(' or '{' ? 1 : ps[i] is '>' or ')' or '}' ? -1 : 0;
            if (ps[i] == ',' && depth == 0)
            {
                parts.Add(ps[start..i]);
                start = i + 1;
            }
        }

        parts.Add(ps[start..]);
        return parts;
    }

    private static string Unqualified(string type) => type.Replace("const ", string.Empty, StringComparison.Ordinal).TrimEnd('&');
}
