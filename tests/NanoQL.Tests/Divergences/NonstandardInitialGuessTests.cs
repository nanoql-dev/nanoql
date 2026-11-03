// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The nonstandard engine's initial guess for a calibration basket (#2845's class): QuantLib averages the fixed
/// nominals by dividing their sum by the count of those above 1e-8
/// (<c>gaussian1dnonstandardswaptionengine.cpp:123-133</c>), a count that can be 0 while the sum is positive, where
/// QuantLib's master refuses the float-float engine's like cases ("no leg 1 reset dates remain at calibration expiry").
/// On the wheel, a Bermudan payer exercised in one, two and three years into a 5-year swap on fixed and floating
/// nominals of 5e-9 refuses its basket with "forward + displacement (-nan(ind) + 0) must be positive", the NaN guess's.
/// nanoql refuses at the guess, in the master's words for its class.
/// </summary>
public class NonstandardInitialGuessTests
{
    [Fact]
    public void FixedNominalsNoneAbove1e8AreRefused()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var calendar = new TARGET();
        Date start = calendar.Advance(calendar.Advance(today, 1, TimeUnit.Years), 2, TimeUnit.Days);
        Schedule Schedule(int months) => new(
            start,
            calendar.Advance(start, 5, TimeUnit.Years),
            new Period(months, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        var swap = new NonstandardSwap(
            SwapType.Payer,
            Enumerable.Repeat(5e-9, 5),
            Enumerable.Repeat(5e-9, 10),
            Schedule(12),
            Enumerable.Repeat(0.03, 5),
            new Thirty360(Thirty360Convention.BondBasis),
            Schedule(6),
            new Euribor6M(curve),
            1.0,
            0.0,
            new Actual360(),
            context,
            paymentConvention: BusinessDayConvention.ModifiedFollowing);
        var swaption = new NonstandardSwaption(swap, new BermudanExercise([.. new[] { 1, 2, 3 }.Select(i => calendar.Advance(today, i, TimeUnit.Years))]));
        var engine = new Gaussian1dNonstandardSwaptionEngine(Gaussian1dInputs.Constant(curve, 0.01, 0.01), discountCurve: curve);
        SwaptionVolatilityStructure vol = ConstantSwaptionVolatility.Create(
            0, calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed(), context);

        var e = Assert.Throws<NanoQLException>(() => swaption.CalibrationBasket(engine, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve, curve), vol, context));
        Assert.Equal($"no fixed leg nominals above 1e-8 remain at calibration expiry {new Date(15, Month.January, 2026)}", e.Message);
    }
}
