// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The working-type mark (playbook §2.5): a type carries <see cref="WorkingTypeAttribute"/> exactly when its own
/// summary or remarks call it a working type, so the mark and the documentation stay in step both ways. The bindings
/// read the mark to keep such types off their handles, so a type the documentation calls one and the mark misses would
/// cross as a shared handle. The check reads every type of the assembly, internal ones too, against the XML
/// documentation the build writes beside it; the plural, "working types", is a passing mention and doesn't count.
/// </summary>
public partial class WorkingTypeMarkTests
{
    private static Assembly Library { get; } = typeof(Bond).Assembly;

    private static Dictionary<string, string> OwnText { get; } = ReadOwnText();

    [Fact]
    public void EveryTypeItsDocumentationCallsAWorkingTypeCarriesTheMark()
    {
        string[] unmarked =
        [
            .. Library.GetTypes()
                .Where(t => SaysWorkingType(t) && !IsMarked(t))
                .Select(t => t.FullName!)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Empty(unmarked);
    }

    [Fact]
    public void EveryMarkedTypeIsCalledAWorkingTypeByItsDocumentation()
    {
        Type[] marked = [.. Library.GetTypes().Where(IsMarked)];
        Assert.NotEmpty(marked);

        string[] undocumented = [.. marked.Where(t => !SaysWorkingType(t)).Select(t => t.FullName!).Order(StringComparer.Ordinal)];
        Assert.Empty(undocumented);
    }

    [Fact]
    public void TheMutableGeneratorsTheFirstWordingMissedCarryTheMark()
    {
        // "A mutable working type": a count of "A working type" alone missed these.
        foreach (Type t in new[] { typeof(MersenneTwisterUniformRng), typeof(HaltonRsg), typeof(SobolRsg) })
        {
            Assert.True(IsMarked(t), t.FullName);
            Assert.True(SaysWorkingType(t), t.FullName);
        }
    }

    [Fact]
    public void ADerivedTypeInheritsTheMarkThroughItsBase()
    {
        WorkingTypeAttribute? mark = typeof(MersenneTwisterUniformRng).GetCustomAttribute<WorkingTypeAttribute>(inherit: true);
        Assert.NotNull(mark);
        AttributeUsageAttribute usage = typeof(WorkingTypeAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.True(usage.Inherited);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, usage.ValidOn);
    }

    private static bool IsMarked(Type t) => t.GetCustomAttribute<WorkingTypeAttribute>(inherit: false) is not null;

    private static bool SaysWorkingType(Type t) =>
        OwnText.TryGetValue("T:" + t.FullName!.Replace('+', '.'), out string? text) && Singular().IsMatch(text);

    // Each documented type's own summary and remarks, by its documentation id.
    private static Dictionary<string, string> ReadOwnText()
    {
        string path = Path.ChangeExtension(Library.Location, ".xml");
        Assert.True(File.Exists(path), $"the library's XML documentation isn't beside it: {path}");
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement member in XDocument.Load(path).Descendants("member"))
        {
            string name = (string)member.Attribute("name")!;
            if (!name.StartsWith("T:", StringComparison.Ordinal))
            {
                continue;
            }

            text[name] = string.Join(' ', member.Elements().Where(e => e.Name == "summary" || e.Name == "remarks").Select(e => e.Value));
        }

        return text;
    }

    [GeneratedRegex(@"\bworking[\s-]+type\b(?!s)", RegexOptions.IgnoreCase)]
    private static partial Regex Singular();
}
