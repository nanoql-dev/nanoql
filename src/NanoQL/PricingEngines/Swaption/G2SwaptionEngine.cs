// SPDX-FileCopyrightText: Copyright (C) 2004 Mike Parker
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/g2swaptionengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;

namespace NanoQL.PricingEngines;

/// <summary>The G2 swaption engine: a European swaption priced by G2's analytic formula.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it. The fixed rate is corrected for the floating leg's spread, which the model doesn't take
/// into account, by the spread times the ratio of the legs' basis-point values on the model's curve, and the swaption
/// is priced by <see cref="G2"/>'s integral over the given number of standard deviations on the given number of
/// segments, its coupons the swap's own fixed coupons.
/// </para>
/// <para>As QuantLib's header warns, the engine assumes that the exercise date equals the start date of the swap.</para>
/// </remarks>
public static class G2SwaptionEngine
{
    /// <summary>Prices a European swaption on a G2 model.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="range">The number of standard deviations used in the exponential term of the integral.</param>
    /// <param name="intervals">The number of intervals used in the integration.</param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled, its swap's nominals aren't constant, a leg's
    /// basis-point value isn't available, or the integrand's break-even level can't be solved.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="intervals"/> is 0.</exception>
    public static InstrumentResults Calculate(
        Swaption swaption, EvaluationContext context, G2 model, double range, int intervals)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        if (!(swaption.SettlementType == SettlementType.Physical))
        {
            throw new NanoQLException("cash-settled swaptions not priced with G2 engine");
        }

        // adjust the fixed rate of the swap for the spread on the
        // floating leg (which is not taken into account by the
        // model)
        FixedVsFloatingSwap swap = swaption.Underlying;
        FixedVsFloatingSwapResults swapResults = DiscountingSwapEngine.Calculate(swap, context, model.TermStructure, false);
        double correction = swap.Spread * Math.Abs(Available(swapResults.FloatingLegBPS) / Available(swapResults.FixedLegBPS));
        double fixedRate = swap.FixedRate - correction;

        FixedVsFloatingSwapArguments arguments = FixedVsFloatingSwapArguments.Of(swap, context);
        double value = model.Swaption(arguments, fixedRate, range, intervals);
        return swaption.FetchResults(new InstrumentResults { NPV = value });
    }

    // A leg's basis-point value, as the swap's legBPS reads it.
    private static double Available(double? bps) => bps ?? throw new NanoQLException("result not available");
}
