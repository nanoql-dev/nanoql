// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="EvaluationContext"/>'s shape: the evaluation date is required and never <c>default(Date)</c>; every other
/// field is an init-only property with QuantLib's default, never a constructor parameter, so fields can be added
/// without breaking callers.
/// </summary>
public class EvaluationContextTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void TheDefaultsAreQuantLibs()
    {
        var context = new EvaluationContext { EvaluationDate = Today };

        Assert.Equal(Today, context.EvaluationDate);
        Assert.False(context.IncludeReferenceDateEvents);
        Assert.Null(context.IncludeTodaysCashFlows);
        Assert.False(context.EnforcesTodaysHistoricFixings);
    }

    [Fact]
    public void TheDefaultDateIsRefusedAtConstructionAndInWith()
    {
        Assert.Throws<ArgumentException>(() => new EvaluationContext { EvaluationDate = default });

        var context = new EvaluationContext { EvaluationDate = Today };
        Assert.Throws<ArgumentException>(() => context with { EvaluationDate = default });
    }

    [Fact]
    public void WithMakesVariationsAndLeavesTheOriginal()
    {
        var context = new EvaluationContext { EvaluationDate = Today };

        EvaluationContext other = context with { IncludeReferenceDateEvents = true, IncludeTodaysCashFlows = false };

        Assert.True(other.IncludeReferenceDateEvents);
        Assert.False(other.IncludeTodaysCashFlows);
        Assert.Equal(Today, other.EvaluationDate);
        Assert.False(context.IncludeReferenceDateEvents);
        Assert.NotEqual(context, other);
        Assert.Equal(context, new EvaluationContext { EvaluationDate = Today });
    }

    [Fact]
    public void NoPublicConstructorTakesAParameter()
    {
        ConstructorInfo[] constructors = typeof(EvaluationContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(constructors);
        Assert.All(constructors, c => Assert.Empty(c.GetParameters()));
    }

    [Fact]
    public void TheEvaluationDateIsTheOnlyRequiredMember()
    {
        string[] required = typeof(EvaluationContext)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .Select(m => m.Name)
            .ToArray();

        Assert.Equal([nameof(EvaluationContext.EvaluationDate)], required);
    }

    [Fact]
    public void EveryOtherPublicPropertyIsInitOnly()
    {
        PropertyInfo[] properties = typeof(EvaluationContext).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);
        foreach (PropertyInfo p in properties)
        {
            MethodInfo? setter = p.SetMethod;
            Assert.True(setter is not null, $"{p.Name} has no init accessor");
            Assert.Contains(
                typeof(IsExternalInit),
                setter!.ReturnParameter.GetRequiredCustomModifiers());
        }
    }
}
