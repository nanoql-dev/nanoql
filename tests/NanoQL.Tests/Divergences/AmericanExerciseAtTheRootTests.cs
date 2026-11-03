// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// American options worth exercising on the reference date, after QuantLib's master's #2879 and #2914, which upstream's
/// cases check (<c>AmericanOptionTests</c>). QuantLib 1.44's binomial engine reports the Black-Scholes theta from the
/// tree's value and Greeks where the option is exercised at the root, where the intrinsic value is time-independent and
/// the equation doesn't hold: 5.7500000000149 for a put at 115 on 100 and 9.149999999935266 for a call at 85, 91 days,
/// on 201 Cox-Ross-Rubinstein steps; and its finite-difference engine's interpolant undershoots the exercise value
/// between nodes, 9.999997372915146 for a call at 100 on 110 over 90 days (q 2%, σ 10%) on its default grid, below its
/// intrinsic 10, which its implied volatility then can't reach ("root not bracketed: f[0.0001,4] ->
/// [2.627085e-06,6.603111e+01]") (QuantLib-Python 1.44). nanoql's binomial theta is 0 at the root; its
/// finite-difference value is floored at the exercise value where the holder can exercise on the clock's reference
/// date, and the implied volatility returns its lower bound where the price error there is within rounding; elsewhere
/// QuantLib's bits.
/// </summary>
public class AmericanExerciseAtTheRootTests
{
    private static readonly Date Today = new(21, Month.September, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Put, 115.0, 0.0, 0.05)]
    [InlineData(OptionType.Call, 85.0, 0.10, 0.01)]
    public void TheBinomialThetaAtTheRootIsZero(OptionType type, double strike, double q, double r)
    {
        // QuantLib 1.44's 5.7500000000149 and 9.149999999935266
        var context = new EvaluationContext { EvaluationDate = Today };
        var process = Process(Today, 100.0, q, r, 0.15);
        var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new AmericanExercise(Today, Today + 91));
        OneAssetOptionResults results = BinomialVanillaEngine.Calculate<CoxRossRubinstein>(option, context, process, 201);
        Assert.Equal(15.0, results.NPV!.Value, 1e-10);
        Assert.Equal(0.0, results.Theta!.Value);
    }

    [Fact]
    public void TheFiniteDifferenceValueIsTheExerciseValueAndItsVolatilityTheLowerBound()
    {
        // QuantLib 1.44's 9.999997372915146 below the intrinsic 10, and its implied volatility's throw
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var process = Process(today, 110.0, 0.02, 0.0, 0.10);
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(today, today + 90));
        double value = FdBlackScholesVanillaEngine.Calculate(option, context, process).NPV!.Value;
        Assert.Equal(10.0, value);
        Assert.Equal(1.0e-4, option.ImpliedVolatility(value, process, context, 1.0e-8, 200, 1.0e-4, 4.0));
    }

    private static BlackScholesMertonProcess Process(Date today, double spot, double q, double r, double vol) =>
        new(spot, new FlatForward(today, q, Dc), new FlatForward(today, r, Dc), new BlackConstantVol(today, new NullCalendar(), vol, Dc));
}
