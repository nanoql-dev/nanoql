// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/dynprogvppintrinsicvalueengine.hpp ql/experimental/finitedifferences/dynprogvppintrinsicvalueengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The intrinsic value of a <see cref="VanillaVPPOption"/> by dynamic programming over known fuel and power prices, one
/// price of each per exercise step: the plant's step condition rolled back from the last step, undiscounted.
/// </summary>
/// <remarks>
/// QuantLib's engine holds a discount curve it never reads; it isn't taken here. A step past either price list is
/// refused, where QuantLib reads past a fuel list shorter than the power list.
/// </remarks>
[Experimental("NQL0001")]
public static class DynProgVPPIntrinsicValueEngine
{
    /// <summary>Prices a plant.</summary>
    /// <param name="option">The plant.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="fuelPrices">The fuel price at each step.</param>
    /// <param name="powerPrices">The power price at each step.</param>
    /// <param name="fuelCostAddon">The cost added to each unit of fuel.</param>
    /// <returns>The results: the value alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The plant is refused, or a step has no price ("invalid time").</exception>
    public static MultiAssetOptionResults Calculate(
        VanillaVPPOption option,
        EvaluationContext context,
        IReadOnlyList<double> fuelPrices,
        IReadOnlyList<double> powerPrices,
        double fuelCostAddon)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fuelPrices);
        ArgumentNullException.ThrowIfNull(powerPrices);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        ImmutableArray<double> fuel = [.. fuelPrices];
        ImmutableArray<double> power = [.. powerPrices];
        var fuelPrice = new FuelPrice(fuel);
        var sparkSpreadPrice = new SparkSpreadPrice(option.HeatRate, fuel, power);
        var stepConditionFactory = new FdmVPPStepConditionFactory(option);
        var mesher = new FdmMesherComposite(stepConditionFactory.StateMesher());
        var mesh = new FdmVPPStepConditionMesher(0, mesher);
        FdmVPPStepCondition stepCondition = stepConditionFactory.Build(mesh, fuelCostAddon, fuelPrice, sparkSpreadPrice);
        var state = new double[mesher.Layout.Dim[0]];
        for (int j = power.Length; j > 0; --j)
        {
            stepCondition.ApplyTo(state, j - 1);
        }

        return option.FetchResults(new MultiAssetOptionResults { NPV = stepCondition.MaxValue(state) });
    }

    // the spark spread at the step t names: power less fuel at the heat rate
    private sealed class SparkSpreadPrice(double heatRate, ImmutableArray<double> fuelPrices, ImmutableArray<double> powerPrices)
        : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t)
        {
            // QuantLib checks the power prices alone and reads the fuel price past a shorter list: both checked here
            int i = (int)t;
            if (!(i < powerPrices.Length && i < fuelPrices.Length))
            {
                throw new NanoQLException("invalid time");
            }

            return powerPrices[i] - (heatRate * fuelPrices[i]);
        }

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }

    // the fuel price at the step t names
    private sealed class FuelPrice(ImmutableArray<double> fuelPrices) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t)
        {
            int i = (int)t;
            if (!(i < fuelPrices.Length))
            {
                throw new NanoQLException("invalid time");
            }

            return fuelPrices[i];
        }

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }
}
