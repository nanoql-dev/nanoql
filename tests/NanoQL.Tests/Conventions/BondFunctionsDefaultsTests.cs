// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's defaults are kept (S3R8): the bond functions' yield and Z-spread solve to 1e-10 in 100 iterations, the
/// yield from a 0.05 guess and the Z-spread from 0.0, where the bond's own yield solves to 1e-8 from 0.05, on the clean
/// price for an engine's results (<c>bondfunctions.hpp</c>, <c>bond.hpp:207-221</c>). The vectors pass every argument,
/// so the defaults are read here from the members' declarations.
/// </summary>
public class BondFunctionsDefaultsTests
{
    [Fact]
    public void TheYieldsAndTheZSpreadKeepQuantLibsDefaults()
    {
        MethodInfo[] yields = Methods(typeof(BondFunctions), nameof(BondFunctions.Yield));
        Assert.Equal(3, yields.Length);
        foreach (MethodInfo yield in yields)
        {
            Assert.Equal(1.0e-10, Default(yield, "accuracy"));
            Assert.Equal(0.05, Default(yield, "guess"));
            if (yield.GetParameters().Any(p => p.Name == "maxIterations"))
            {
                Assert.Equal(100, Default(yield, "maxIterations"));
            }
        }

        MethodInfo zSpread = Assert.Single(Methods(typeof(BondFunctions), nameof(BondFunctions.ZSpread)));
        Assert.Equal(1.0e-10, Default(zSpread, "accuracy"));
        Assert.Equal(100, Default(zSpread, "maxIterations"));
        Assert.Equal(0.0, Default(zSpread, "guess"));

        MethodInfo[] bondYields = Methods(typeof(Bond), nameof(Bond.Yield));
        Assert.Equal(2, bondYields.Length);
        foreach (MethodInfo yield in bondYields)
        {
            Assert.Equal(1.0e-8, Default(yield, "accuracy"));
            Assert.Equal(100, Default(yield, "maxEvaluations"));
            Assert.Equal(0.05, Default(yield, "guess"));
        }

        MethodInfo fromResults = Assert.Single(bondYields, m => m.GetParameters()[0].ParameterType == typeof(BondResults));
        Assert.Equal(BondPriceType.Clean, Default(fromResults, "priceType"));
    }

    private static MethodInfo[] Methods(Type type, string name) =>
        [.. type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(m => m.Name == name)];

    private static object? Default(MethodInfo method, string parameter)
    {
        ParameterInfo info = Assert.Single(method.GetParameters(), p => p.Name == parameter);
        Assert.True(info.HasDefaultValue, $"{method}: {parameter} has no default");
        return info.DefaultValue;
    }
}
