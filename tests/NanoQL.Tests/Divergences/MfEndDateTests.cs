// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Markov-functional model's caplet end dates (the S-g1d plan's G1R5 (i), #2848's class): a caplet calibration
/// point, and the outputs' forward rates and caplets, end at the value date advanced by the index's tenor on its fixing
/// calendar (<c>markovfunctional.cpp:278</c>, <c>:949</c>, <c>:1109</c>), where the index's own end is its maturity
/// date, which an index of its own may override. On the wheel, USD Libor 6M fixing on 9 April 2025 values on 11 April
/// and matures on 14 October 2025, where the advance gives 13 October. nanoql ends each at the index's maturity date,
/// as QuantLib's master computes the Gaussian1d forward rate after the pin. QuantLib's check: the index's own maturity
/// date, and for an index whose maturity is the advance (Euribor), QuantLib's bits. The outputs' caplets read the end
/// twice, the forward rate's and the caplet's (<c>:949</c>, <c>:1109</c>): a call struck at 0 pays the floating leg, so
/// its price is P(0, t) − P(0, end) for any end the two share with the calibration point, QuantLib's own martingale
/// identity, which nanoql's holds to 7e-8; QuantLib's end a day early at the forward's site moves it by 4e-5, at the
/// caplet's by 5e-3.
/// </summary>
public class MfEndDateTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly USDLibor Index = new(new Period(6, TimeUnit.Months));
    private static readonly Date[] Expiries = [new(9, Month.April, 2025), new(9, Month.October, 2025)];

    [Fact]
    public void ACapletPointEndsAtItsIndexsMaturityDate()
    {
        MarkovFunctional model = Model();
        Date fixing = Expiries[0];
        Date end = Index.MaturityDate(Index.ValueDate(fixing));
        Assert.Equal(new Date(14, Month.October, 2025), end);
        double annuity = Index.DayCounter.YearFraction(fixing, end) * Curve.Discount(end, true);
        Assert.Equal(annuity, model.ModelOutputs(Context).Annuity[0]);

        // QuantLib's end, the advance: 13 October 2025.
        Date advanced = Index.FixingCalendar.Advance(Index.ValueDate(fixing), Index.Tenor, Index.BusinessDayConvention, Index.EndOfMonth);
        Assert.Equal(new Date(13, Month.October, 2025), advanced);
    }

    [Fact]
    public void TheOutputsCapletsEndWhereTheCalibrationPointDoes()
    {
        MarkovFunctionalModelOutputs outputs = Model().ModelOutputs(Context);
        for (int p = 0; p < Expiries.Length; p++)
        {
            Date end = Index.MaturityDate(Index.ValueDate(Expiries[p]));
            double floatingLeg = Curve.Discount(Expiries[p], true) - Curve.Discount(end, true);
            Assert.Equal(0.0, outputs.SmileStrikes[p][0]);
            Assert.True(Math.Abs((outputs.ModelCallPremium[p][0] / floatingLeg) - 1.0) <= 1e-6, $"{p}: {outputs.ModelCallPremium[p][0]} against {floatingLeg}");
        }
    }

    // A model calibrated to USD Libor 6M caplets at 20%, fixing on 9 April and 9 October 2025.
    private static MarkovFunctional Model()
    {
        OptionletVolatilityStructure vol = new ConstantOptionletVolatility(
            Today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());
        return new MarkovFunctional(
            Curve, 0.01, [], [1.0], vol, Expiries, Index, Context,
            new MarkovFunctionalModelSettings().WithYGridPoints(32).WithGaussHermitePoints(16).WithAdjustments(MarkovFunctionalModelSettingsAdjustments.AdjustNone));
    }
}
