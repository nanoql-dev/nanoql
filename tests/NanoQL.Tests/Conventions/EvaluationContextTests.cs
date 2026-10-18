// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using NanoQL.Indexes;
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
        Assert.Same(IndexFixings.Empty, context.Fixings);
        Assert.True(context.UseAtParCoupons);
    }

    [Fact]
    public void EqualityAndTheHashFollowTheFixingsContent()
    {
        var euribor = new IborIndex(
            "Euribor", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            true, new Actual360());
        var shouting = new IborIndex(
            "EURIBOR", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            true, new Actual360());
        var sofr = new OvernightIndex("SOFR", 0, null, new UnitedStates(UnitedStatesMarket.SOFR), new Actual360());
        Date d1 = new(13, Month.October, 2026), d2 = new(14, Month.October, 2026);

        // The same fixings, stored in different orders and under names differing in ASCII case.
        IndexFixings a = IndexFixings.Empty.AddFixing(euribor, d1, 0.01).AddFixing(euribor, d2, 0.02).AddFixing(sofr, d1, 0.03);
        IndexFixings b = IndexFixings.Empty.AddFixing(sofr, d1, 0.03).AddFixings(shouting, [d2, d1], [0.02, 0.01]);
        var first = new EvaluationContext { EvaluationDate = Today, Fixings = a };
        var second = new EvaluationContext { EvaluationDate = Today, Fixings = b };

        Assert.NotSame(a, b);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());

        // A changed fixing, an extra history, the switch: each makes the contexts unequal.
        Assert.NotEqual(first, second with { Fixings = b.AddFixing(euribor, d1, 0.015, forceOverwrite: true) });
        Assert.NotEqual(first, second with { Fixings = b.AddFixings(new OvernightIndex("ESTR", 0, null, new TARGET(), new Actual360()), [], []) });
        Assert.NotEqual(first, second with { UseAtParCoupons = false });
        Assert.Throws<ArgumentNullException>(() => first with { Fixings = null! });
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
