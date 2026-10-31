// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit.v3;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The <c>Oracle</c> trait's placement (docs/porting-playbook.md §5): each oracle's test classes carry its trait on the
/// class, and nothing else carries one, so the coverage gate's runs count only the tests that check against that
/// oracle. <c>[Trait("Oracle", "QuantLib")]</c> sits on every test class in <c>TestSuite/</c> and <c>Vectors/</c>,
/// <c>[Trait("Oracle", "QLNet")]</c> on every one in <c>QLNet/</c>, its vector classes included, and
/// <c>[Trait("Oracle", "Reference")]</c>, a published standard's values, on every one in <c>Reference/</c>.
/// </summary>
public class OracleTraitTests
{
    private static readonly Dictionary<string, string> Oracles = new()
    {
        ["NanoQL.Tests.TestSuite"] = "QuantLib",
        ["NanoQL.Tests.Vectors"] = "QuantLib",
        ["NanoQL.Tests.QLNet"] = "QLNet",
        ["NanoQL.Tests.Reference"] = "Reference",
    };

    [Fact]
    public void EveryTestClassInTestSuiteAndVectorsCarriesTheTraitOnTheClass()
    {
        Placement("QuantLib");
    }

    [Fact]
    public void EveryTestClassInQLNetCarriesItsTraitOnTheClass()
    {
        Placement("QLNet");
    }

    [Fact]
    public void EveryTestClassInReferenceCarriesItsTraitOnTheClass()
    {
        Placement("Reference");
    }

    [Fact]
    public void NoOtherClassOrMethodCarriesAnOracleTrait()
    {
        var misplaced = new List<string>();
        Assembly assembly = typeof(OracleTraitTests).Assembly;
        if (HasOracle(assembly))
        {
            misplaced.Add(assembly.GetName().Name!);
        }

        foreach (Type t in assembly.GetTypes())
        {
            // A test class of an oracle's namespace carries that oracle's trait alone; every other type none.
            Oracles.TryGetValue(t.Namespace ?? string.Empty, out string? oracle);
            List<KeyValuePair<string, string>> traits = Traits(t).Where(p => p.Key == "Oracle").ToList();
            bool allowed = oracle is not null && IsTestClass(t) && traits.Count <= 1
                && traits.All(p => p.Value == oracle);
            if (traits.Count > 0 && !allowed)
            {
                misplaced.Add(t.FullName!);
            }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            misplaced.AddRange(t.GetMethods(all).Where(HasOracle).Select(m => $"{t.FullName}.{m.Name}"));
        }

        Assert.Empty(misplaced);
    }

    private static void Placement(string oracle)
    {
        List<Type> classes = TestClasses()
            .Where(t => Oracles.TryGetValue(t.Namespace ?? string.Empty, out string? o) && o == oracle).ToList();

        Assert.NotEmpty(classes);
        Assert.Empty(classes.Where(t => !Traits(t).Contains(new KeyValuePair<string, string>("Oracle", oracle)))
            .Select(t => t.FullName));
    }

    private static IEnumerable<Type> TestClasses() =>
        typeof(OracleTraitTests).Assembly.GetTypes().Where(IsTestClass);

    private static bool IsTestClass(Type t) =>
        t.IsClass && t.GetMethods().Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any());

    private static IEnumerable<KeyValuePair<string, string>> Traits(ICustomAttributeProvider member) =>
        member.GetCustomAttributes(inherit: false).OfType<ITraitAttribute>().SelectMany(a => a.GetTraits());

    private static bool HasOracle(ICustomAttributeProvider member) => Traits(member).Any(t => t.Key == "Oracle");
}
