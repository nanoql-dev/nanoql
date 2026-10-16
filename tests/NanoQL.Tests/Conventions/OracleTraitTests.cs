// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit.v3;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The <c>Oracle</c> trait's placement (docs/porting-playbook.md §5): <c>[Trait("Oracle", "QuantLib")]</c> sits on the
/// class of every test class in <c>TestSuite/</c> and <c>Vectors/</c>, and on nothing else, so the coverage gate counts
/// only tests that check against QuantLib.
/// </summary>
public class OracleTraitTests
{
    private static readonly string[] OracleNamespaces = ["NanoQL.Tests.TestSuite", "NanoQL.Tests.Vectors"];

    [Fact]
    public void EveryTestClassInTestSuiteAndVectorsCarriesTheTraitOnTheClass()
    {
        List<Type> classes = TestClasses().Where(t => OracleNamespaces.Contains(t.Namespace)).ToList();

        Assert.NotEmpty(classes);
        Assert.Empty(classes.Where(t => !Traits(t).Contains(new KeyValuePair<string, string>("Oracle", "QuantLib")))
            .Select(t => t.FullName));
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
            bool oracleClass = OracleNamespaces.Contains(t.Namespace) && IsTestClass(t);
            if (!oracleClass && HasOracle(t))
            {
                misplaced.Add(t.FullName!);
            }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            misplaced.AddRange(t.GetMethods(all).Where(HasOracle).Select(m => $"{t.FullName}.{m.Name}"));
        }

        Assert.Empty(misplaced);
    }

    private static IEnumerable<Type> TestClasses() =>
        typeof(OracleTraitTests).Assembly.GetTypes().Where(IsTestClass);

    private static bool IsTestClass(Type t) =>
        t.IsClass && t.GetMethods().Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any());

    private static IEnumerable<KeyValuePair<string, string>> Traits(ICustomAttributeProvider member) =>
        member.GetCustomAttributes(inherit: false).OfType<ITraitAttribute>().SelectMany(a => a.GetTraits());

    private static bool HasOracle(ICustomAttributeProvider member) => Traits(member).Any(t => t.Key == "Oracle");
}
