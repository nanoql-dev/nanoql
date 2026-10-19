// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An implied volatility whose bracket starts at −1. QuantLib's helper starts its volatility quote at −1.0, "so that
/// calculation is forced at first ... call", and runs the engine only when the volatility asked differs from the quote
/// (<c>swaption.cpp:55-92</c>, lines 61-64 and 87-91); the solver's first call is at the bracket's lower end
/// (<c>math/solver1d.hpp:187</c>), so a lower end of −1.0 skips the engine and reads its NPV result, which nothing has
/// set (<c>instrument.hpp:121</c>, <c>pricingengine.hpp:63-73</c>): an indeterminate value, and a run-dependent result.
/// nanoql's helper prices at every call: at −1.0 the price is the price at volatility 1, the variance being the
/// volatility squared, so the result depends on the inputs alone.
/// </summary>
public class SwaptionImpliedVolatilityTests
{
    [Fact]
    public void ABracketStartingAtMinusOnePricesAtVolatilityOne()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .WithFixedRate(0.03)
            .ToVanillaSwap(context);
        var swaption = new Swaption(swap, new EuropeanExercise(new Date(15, Month.January, 2026)));
        double Price(double vol) => BlackSwaptionEngine.Calculate(swaption, context, curve, vol).NPV!.Value;

        Assert.Equal(Price(1.0), Price(-1.0));

        // At the swaption's own price, both ends of [-1, 4] price above it: no root.
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => swaption.ImpliedVolatility(Price(0.2), curve, 0.1, context, 1e-10, 100, -1.0, 4.0));
        Assert.StartsWith("root not bracketed: f[-1,4] -> [", e.Message, StringComparison.Ordinal);

        // A price above volatility 1's is bracketed, and found.
        double implied = swaption.ImpliedVolatility(Price(1.5), curve, 1.2, context, 1e-12, 100, -1.0, 4.0);
        Assert.Equal(1.5, implied, 1e-10);
    }
}
