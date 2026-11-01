// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The Java back end: packages per namespace, the overload rule over erased signatures (no two forms of a class share
/// one, and the telescoped overloads take no call another form takes, §2.2), flags enums holding any combination, every
/// handle a call reads kept reachable until the call returns, a user's classes in packages of their own over their own
/// jextract class and registry (BIR9), the members C# hides that Java overrides, and the dispatch reaching what C's does
/// (BIR15).
/// </summary>
public class JavaTests
{
    [Theory]
    [InlineData("NanoQL", false, "nql", "org.nanoql")]
    [InlineData("NanoQL.TermStructures", false, "nql", "org.nanoql.termstructures")]
    [InlineData("NanoQL.Experimental", true, "nql", "org.nanoql.experimental")]
    [InlineData("NanoQL.Models", true, "nql", "org.nanoql.experimental")]
    [InlineData("Acme.Curves", false, "acme", "acme.curves")]
    [InlineData("Acme.Curves", true, "acme", "acme.curves.experimental")]
    public void PackagesFollowNamespaces(string ns, bool experimental, string prefix, string package) =>
        Assert.Equal(package, JavaWriter.Package(ns, experimental, prefix));

    [Fact]
    public void NoTwoFormsOfAClassShareAnErasedSignature()
    {
        var w = new JavaWriter(Surfaces.Nanoql.Surface);
        foreach (IGrouping<string, JavaMember> group in w.Members.Values.GroupBy(m => m.Holder + "|" + (m.Style == "new" ? "<init>" : m.Name)))
        {
            var signatures = group.Select(m => (m.Style == "self" ? "self," : string.Empty) + string.Join(",", m.Types.Select(JavaWriter.Erased))).ToList();
            Assert.True(signatures.Count == signatures.Distinct().Count(), $"{group.Key}: two forms share an erased signature");
        }

        Assert.True(w.Renamed > 0 && w.Renamed < 200, $"{w.Renamed} forms renamed");
    }

    [Fact]
    public void NoTelescopedOverloadTakesACallAnotherFormTakes()
    {
        var w = new JavaWriter(Surfaces.Nanoql.Surface);
        int offered = 0;
        foreach (IGrouping<string, JavaMember> group in w.Members.Values.GroupBy(m => m.Holder + "|" + (m.Style == "new" ? "<init>" : m.Name)))
        {
            var calls = new HashSet<string>(StringComparer.Ordinal);
            foreach (JavaMember m in group)
            {
                for (int n = m.DefaultsFrom; n <= m.Types.Count; n++)
                {
                    offered++;
                    Assert.True(calls.Add(n + "|" + string.Join(",", m.Types.Take(n).Select(JavaWriter.Erased))), $"{group.Key}: two forms take {n} arguments of one erasure each");
                }
            }
        }

        Assert.True(offered > 10000);
    }

    [Fact]
    public void AFlagsEnumHoldsAnyCombination()
    {
        string dir = Directory.CreateTempSubdirectory("nanoql-java-").FullName;
        try
        {
            Surfaces.Nanoql.WriteJava(dir);
            string flags = File.ReadAllText(Path.Combine(dir, "org", "nanoql", "models", "MarkovFunctionalModelSettingsAdjustments.java"));
            Assert.Contains("public record MarkovFunctionalModelSettingsAdjustments(int value) implements org.nanoql.NanoEnum {", flags, StringComparison.Ordinal);
            Assert.Contains("public MarkovFunctionalModelSettingsAdjustments or(MarkovFunctionalModelSettingsAdjustments other) {", flags, StringComparison.Ordinal);
            Assert.Contains("        return new MarkovFunctionalModelSettingsAdjustments(value);", flags, StringComparison.Ordinal);
            string unit = File.ReadAllText(Path.Combine(dir, "org", "nanoql", "time", "TimeUnit.java"));
            Assert.Contains("public enum TimeUnit implements org.nanoql.NanoEnum {", unit, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', flags);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EveryMemberFencesWhatItReadsAHandleFrom()
    {
        string dir = Directory.CreateTempSubdirectory("nanoql-java-").FullName;
        try
        {
            Surfaces.Nanoql.WriteJava(dir);
            int fenced = 0;
            foreach (string file in Directory.EnumerateFiles(dir, "*.java", SearchOption.AllDirectories))
            {
                // Each member of a class, from its declaration at four spaces to the next.
                foreach (string member in Regex.Split(File.ReadAllText(file), @"(?m)^(?=    (?:public|default|static|protected|private) )"))
                {
                    var read = Regex.Matches(member, @"Native\.h\((\w+)\)").Select(x => x.Groups[1].Value).Where(n => n != "v_")
                        .Concat(Regex.Matches(member, @"Native\.list\(a_, (\w+), [\w.]+\.ADDRESS, \(s_, i_, v_\) -> s_\.setAtIndex\([\w.]+\.ADDRESS, i_, [\w.]+\.Native\.h\(v_\)\)\)").Select(x => x.Groups[1].Value));
                    foreach (string name in read.Distinct())
                    {
                        // A map's objects travel in lists made from it, kept reachable through the map.
                        Match from = Regex.Match(member, $@"{name} = (\w+) == null \? null");
                        string held = from.Success ? from.Groups[1].Value : name;
                        Assert.True(member.Contains($"java.lang.ref.Reference.reachabilityFence({held});", StringComparison.Ordinal), $"{Path.GetFileName(file)}: a member reads a handle from {name} and doesn't fence {held}");
                        fenced++;
                    }
                }
            }

            Assert.True(fenced > 10000, $"{fenced} handles fenced");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AUsersClassesGetPackagesOfTheirOwnOverNanoqls()
    {
        string dir = Directory.CreateTempSubdirectory("nanoql-java-").FullName;
        try
        {
            // The user's prefix alone: its classes, over its own jextract class and registry, beside nanoql's jar.
            Surfaces.WithFixtures.WriteJava(dir, ["fx"]);
            string package = Path.Combine(dir, "org", "nanoql", "bind", "tests", "fixtures");
            string curve = File.ReadAllText(Path.Combine(package, "UserCalibratedCurve.java"));
            Assert.Contains("public class UserCalibratedCurve extends org.nanoql.termstructures.YieldTermStructure implements org.nanoql.models.ICalibratedModel {", curve, StringComparison.Ordinal);
            Assert.Contains("        fx.internal.FxTypes.register();", curve, StringComparison.Ordinal);
            Assert.Contains("fx.ffi.fx_h.fx_UserCalibratedCurve_", curve, StringComparison.Ordinal);
            Assert.DoesNotContain("org.nanoql.ffi.nanoql_h", curve, StringComparison.Ordinal);
            string registry = File.ReadAllText(Path.Combine(dir, "fx", "internal", "FxTypes.java"));
            Assert.Contains("org.nanoql.internal.Types.register(\"NanoQL.Bind.Tests.Fixtures.UserCalibratedCurve\"", registry, StringComparison.Ordinal);

            // A user's class named like one of nanoql's: each its own class, nanoql's named in full.
            string calendar = File.ReadAllText(Path.Combine(package, "Calendar.java"));
            Assert.Contains("public static boolean holds(org.nanoql.time.Calendar calendar, java.time.LocalDate d) {", calendar, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(package, "experimental", "Walk.java")));

            // Nothing of nanoql's: its jar holds that.
            Assert.False(Directory.Exists(Path.Combine(dir, "org", "nanoql", "time")));
            Assert.False(File.Exists(Path.Combine(dir, "org", "nanoql", "internal", "NanoqlTypes.java")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void OneMemberCSharpHidesJavaOverrides()
    {
        // A member C# declares new, with an ancestor class's member's Java name and erased parameters: Java overrides
        // what C# and C++ hide, so through the base's reference Java calls the derived member (docs/bindings.md, Java).
        Surface surface = Surfaces.Nanoql.Surface;
        var w = new JavaWriter(surface);
        var byName = surface.Types.ToDictionary(t => t.Type.FullName!);
        var byClass = w.Members.Values.Where(m => m.Style == "instance").GroupBy(m => m.Type.Type.FullName!).ToDictionary(g => g.Key, g => g.ToList());
        var hides = new List<string>();
        foreach ((string cls, List<JavaMember> ms) in byClass)
        {
            foreach (JavaMember m in ms)
            {
                MethodInfo? method = Method(m.Function.Member);
                if (method == null || (method.IsVirtual && !method.Attributes.HasFlag(MethodAttributes.NewSlot)) || method.DeclaringType!.IsInterface)
                {
                    continue;
                }

                // The same result too: a C# covariant-return override is a new slot as well, and an override in both. A
                // member a class surfaces from a closed generic base that crosses too is the base's own, not a hide.
                string signature = string.Join(",", m.Types.Select(JavaWriter.Erased)) + "->" + JavaWriter.Erased(w.Result(m.Function));
                for (string? b = byName[cls].Base; b != null && byName.TryGetValue(b, out TypeRow? row); b = row.Base)
                {
                    if (byClass.GetValueOrDefault(b, []).Any(a => a.Name == m.Name && string.Join(",", a.Types.Select(JavaWriter.Erased)) + "->" + JavaWriter.Erased(w.Result(a.Function)) == signature
                        && !(Method(a.Function.Member) is { } am && am.MetadataToken == method.MetadataToken && am.DeclaringType == method.DeclaringType)))
                    {
                        hides.Add(m.Type.CName + "." + m.Name);
                        break;
                    }
                }
            }
        }

        Assert.Equal(["InterestRateVolSurface.optionDateFromTenor"], hides.Order(StringComparer.Ordinal));

        static MethodInfo? Method(MemberInfo? member) => member switch
        {
            MethodInfo mi => mi,
            PropertyInfo pi => pi.GetMethod,
            _ => null,
        };
    }

    [Fact]
    public void TheDispatchReachesEveryFunctionCsReplays()
    {
        Generation g = Surfaces.Nanoql;
        Dictionary<string, string> files = g.JavaDispatch();
        Assert.Equal(JavaReplayWriter.Parts + 1, files.Count);
        var java = files.Where(f => !f.Key.EndsWith("DispatchTable.java", StringComparison.Ordinal))
            .SelectMany(f => f.Value.Split('\n').Where(l => l.StartsWith("        \"nql_", StringComparison.Ordinal)).Select(l => l.Trim().Trim(',', '"')))
            .ToHashSet(StringComparer.Ordinal);
        var c = g.CDispatch.Split('\n').Where(l => l.StartsWith("    {\"nql_", StringComparison.Ordinal))
            .Select(l => l[(l.IndexOf('"', StringComparison.Ordinal) + 1)..l.IndexOf('"', l.IndexOf('"', StringComparison.Ordinal) + 1)])
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(c.Count, java.Count);
        Assert.Empty(c.Except(java));
    }
}
