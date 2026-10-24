// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo digital engine at seed 76 (the S-van plan's VR4 (xxv), under R8). QuantLib's pricer draws the
/// Brownian bridge's uniform numbers from <c>PseudoRandom::urng_type(76)</c> with the path generator's dimension
/// (<c>mcdigitalengine.hpp:177-179</c>), so at the pricing seed 76 without antithetic variates each step's bridge draws
/// the uniform number of the step's own increment: a down-and-in put at 80 (cash 10, S 100, r 5%, vol 25%, one year,
/// 12 steps, 100,000 samples) prices 3.4762897047890697 ± 0.01477, 5.7 errors above QuantLib's analytic engine,
/// 3.3915878484599937, where seed 77 gives 0.8 errors above [executed on the wheel: <c>oracle_c1b.py</c>]. nanoql's bridge
/// takes the seed 77 at the pricing seed 76; every other seed keeps QuantLib's 76 and its bits. QuantLib's check: its
/// analytic engine.
/// </summary>
public class McDigitalSeed76Tests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtSeed76TheBridgeDrawsItsOwnNumbers()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var option = new VanillaOption(new CashOrNothingPayoff(OptionType.Put, 80.0, 10.0), new AmericanExercise(Today, Today + 365));

        OneAssetOptionResults at76 = MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 12, null, false, false, 100000, null, null, 76);
        double analytic = AnalyticDigitalAmericanEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(3.3915878484599937, analytic);
        Assert.True(
            Math.Abs(at76.NPV!.Value - analytic) <= 3.0 * at76.ErrorEstimate!.Value, $"{at76.NPV} ± {at76.ErrorEstimate}");

        // another seed keeps QuantLib's bridge seed and bits
        Assert.Equal(
            3.37419669336565,
            MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, process, 12, null, false, false, 100000, null, null, 42).NPV);
    }
}
