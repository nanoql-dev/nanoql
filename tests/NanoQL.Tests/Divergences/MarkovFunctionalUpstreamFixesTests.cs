// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fixes QuantLib's master made to the Markov-functional model after the pin, which nanoql takes: #2912
/// (<c>b762b0b02</c>) strikes the upper rate bound's digital at the bound, where QuantLib 1.44 subtracts the smile's
/// shift (<c>markovfunctional.cpp:432-436</c>), so on a shifted smile the model's rates are capped below the bound the
/// rest of the model reads unshifted; it asks for at least two grid points, where 1.44 asks for one
/// (<c>markovfunctional.hpp:168-172</c>), and on one the grid's spacing divides by zero; and its refusal of two smile
/// pretreatments is restated, "At most one of …". Its count of the pretreatments nanoql has (<c>MfSmileFlagsTests</c>).
/// The master's cases below are its tests of the settings, the grid points, the shifted smile's bound, the SABR and
/// payoff-extrapolation fits and the trace. Not ported: <c>testNullSwapIndex</c> and <c>testNullIborIndex</c> (nanoql's
/// constructors refuse a null index before the model reads it), <c>testTimeZeroNumeraireAfterDateChange</c>,
/// <c>testModelOutputsAfterRecalculation</c> and <c>testModelOutputsAfterMarketUpdate</c> (no observers: nanoql's
/// outputs are computed once from the model's own state, <c>MarkovFunctionalOutputsTests</c>).
/// </summary>
public class MarkovFunctionalUpstreamFixesTests
{
    private static readonly Date ReferenceDate = new(14, Month.November, 2012);
    private static readonly EvaluationContext Context = new() { EvaluationDate = ReferenceDate };
    private static readonly double[] Checkpoints = [0.1, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 5.0];

    [Fact]
    public void TestConflictingSmileSettings()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testConflictingSmileSettings)
        var factory = new ValidationOnlySmileFactory();
        MarkovFunctionalModelSettingsAdjustments[] conflicts =
        [
            KahaleSmile | SabrSmile,
            KahaleSmile | CustomSmile,
            SabrSmile | CustomSmile,
            KahaleSmile | SabrSmile | CustomSmile,
            KahaleInterpolation | SabrSmile,
            KahaleInterpolation | CustomSmile,
        ];
        foreach (MarkovFunctionalModelSettingsAdjustments adjustments in conflicts)
        {
            MarkovFunctionalModelSettings settings =
                new MarkovFunctionalModelSettings().WithAdjustments(adjustments).WithCustomSmileFactory(factory);
            Assert.Contains(
                "At most one of KahaleSmile, SabrSmile and CustomSmile can be specified at the same time",
                Assert.Throws<NanoQLException>(() => settings.Validate()).Message);
        }
    }

    [Fact]
    public void TestSmileSettingsValidation()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testSmileSettingsValidation)
        _ = new MarkovFunctionalModelSettings().Validate();

        var factory = new ValidationOnlySmileFactory();
        MarkovFunctionalModelSettingsAdjustments[] valid =
        [
            AdjustNone,
            KahaleSmile,
            SabrSmile,
            CustomSmile,
            KahaleInterpolation,
            KahaleSmile | SmileDeleteArbitragePoints,
            SabrSmile | SmileExponentialExtrapolation,
            AdjustDigitals | AdjustYts | ExtrapolatePayoffFlat | NoPayoffExtrapolation,
        ];
        foreach (MarkovFunctionalModelSettingsAdjustments adjustments in valid)
        {
            // QuantLib's validate completes the flags in place; nanoql's returns the completed copy
            MarkovFunctionalModelSettings settings = new MarkovFunctionalModelSettings()
                .WithAdjustments(adjustments).WithCustomSmileFactory(factory).Validate();
            if ((adjustments & KahaleInterpolation) != 0)
            {
                Assert.True((settings.Adjustments & KahaleSmile) != 0);
            }

            if ((adjustments & KahaleSmile) != 0 && (adjustments & SmileDeleteArbitragePoints) != 0)
            {
                Assert.True((settings.Adjustments & KahaleInterpolation) != 0);
            }
        }

        MarkovFunctionalModelSettings withoutFactory = new MarkovFunctionalModelSettings().WithAdjustments(CustomSmile);
        Assert.Contains("missing CustomSmile", Assert.Throws<NanoQLException>(() => withoutFactory.Validate()).Message);
    }

    [Fact]
    public void TestMinimumYGridPoints()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testMinimumYGridPoints)
        foreach (int points in new[] { 0, 1 })
        {
            MarkovFunctionalModelSettings settings = new MarkovFunctionalModelSettings().WithYGridPoints(points);
            Assert.Contains("At least two grid points", Assert.Throws<NanoQLException>(() => settings.Validate()).Message);
        }

        _ = new MarkovFunctionalModelSettings().WithYGridPoints(2).Validate();
    }

    [Fact]
    public void TestModelOutputsTrace()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testModelOutputsTrace)
        Date expiry = new TARGET().Advance(ReferenceDate, 1, TimeUnit.Years);
        var model = new MarkovFunctional(
            MarkovFunctionalTests.FlatYts(Context),
            0.01,
            [],
            [1.0],
            MarkovFunctionalTests.FlatOptionletVts(Context),
            [expiry],
            new Euribor(new Period(6, TimeUnit.Months)),
            Context,
            new MarkovFunctionalModelSettings().WithYGridPoints(32).WithGaussHermitePoints(16));
        MarkovFunctionalModelOutputs outputs = model.ModelOutputs(Context);
        Assert.Single(outputs.Expiries);
        Assert.Equal(expiry, outputs.Expiries[0]);
        Assert.Single(outputs.Atm);
        Assert.Single(outputs.Annuity);
        Assert.Single(outputs.MarketZerorate);
        Assert.True(Math.Abs(outputs.MarketZerorate[0] - 0.03) < 1.0e-12);
        Assert.True(outputs.Annuity[0] > 0.0);

        string trace = outputs.ToString();
        Assert.Contains("Markov functional model trace output", trace);
        Assert.Contains("Model settings", trace);
        Assert.Contains("Yield termstructure fit:", trace);
        Assert.Contains("Volatility smile fit:", trace);
        Assert.Contains("marketCallRaw", trace);
        Assert.Contains("marketVega", trace);
    }

    [Fact]
    public void TestSabrSmileCalibration()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testSabrSmileCalibration)
        Date expiry = new TARGET().Advance(ReferenceDate, 1, TimeUnit.Years);
        var model = new MarkovFunctional(
            MarkovFunctionalTests.FlatYts(Context),
            0.01,
            [],
            [1.0],
            MarkovFunctionalTests.FlatOptionletVts(Context),
            [expiry],
            new Euribor(new Period(6, TimeUnit.Months)),
            Context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64)
                .WithGaussHermitePoints(32)
                .WithAdjustments(SabrSmile | SmileExponentialExtrapolation)
                .WithSmileMoneynessCheckpoints(Checkpoints));
        MarkovFunctionalModelOutputs outputs = model.ModelOutputs(Context);
        Assert.Single(outputs.Expiries);
        Assert.NotEmpty(outputs.SmileStrikes[0]);
        Assert.True(Math.Abs(outputs.ModelZerorate[0] - outputs.MarketZerorate[0]) < 1.0e-4);
        AssertPremiumsFit(outputs);
        Assert.Single(model.ArbitrageIndices());
    }

    [Fact]
    public void TestShiftedSmileUpperRateBound()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testShiftedSmileUpperRateBound)
        Date expiry = new TARGET().Advance(ReferenceDate, 1, TimeUnit.Years);
        var curve = new FlatForward(ReferenceDate, 0.03, new Actual365Fixed());
        var index = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years));
        const double volatility = 0.5;
        foreach (double shift in new[] { 0.0, 0.05 })
        {
            foreach (double upperBound in new[] { 0.10, 2.0 })
            {
                string label = $"shift={shift}, upperBound={upperBound}";
                var surface = new ConstantSwaptionVolatility(
                    ReferenceDate, new TARGET(), BusinessDayConvention.Following, volatility, new Actual365Fixed(),
                    VolatilityType.ShiftedLognormal, shift);
                Assert.Equal(shift, surface.SmileSection(expiry, new Period(1, TimeUnit.Years), true).Shift);
                var model = new MarkovFunctional(
                    curve,
                    0.01,
                    [],
                    [1.0],
                    surface,
                    [expiry],
                    [new Period(1, TimeUnit.Years)],
                    index,
                    Context,
                    new MarkovFunctionalModelSettings()
                        .WithYGridPoints(64)
                        .WithYStdDevs(7.0)
                        .WithGaussHermitePoints(32)
                        .WithUpperRateBound(upperBound)
                        .WithDigitalGap(1.0e-7)
                        .WithMarketRateAccuracy(1.0e-10)
                        .WithAdjustments(AdjustNone));
                Date terminal = model.NumeraireDate;
                double accrual = index.DayCounter.YearFraction(expiry, terminal);
                double forward = ((curve.Discount(expiry) / curve.Discount(terminal)) - 1.0) / accrual;
                double stdDev = volatility * Math.Sqrt(curve.TimeFromReference(expiry));
                foreach (double state in new[] { 0.0, 1.09375, 3.0625 })
                {
                    double uncappedRate = ((forward + shift) * Math.Exp((-0.5 * stdDev * stdDev) + (stdDev * state))) - shift;
                    double expectedRate = Math.Min(uncappedRate, upperBound);
                    double modelRate = ((1.0 / model.Zerobond(terminal, expiry, state)) - 1.0) / accrual;
                    Assert.True(double.IsFinite(modelRate), $"{label}, state={state}");
                    Assert.True(
                        Math.Abs(modelRate - expectedRate) < 1.0e-8,
                        $"{label}, state={state}: model rate {modelRate}, expected {expectedRate}");
                }

                MarkovFunctionalModelOutputs outputs = model.ModelOutputs(Context);
                Assert.Single(outputs.Expiries);
                IReadOnlyList<double> strikes = outputs.SmileStrikes[0];
                Assert.NotEmpty(strikes);
                int atmIndex = 0;
                for (int strike = 1; strike < strikes.Count; ++strike)
                {
                    if (Math.Abs(strikes[strike] - forward) < Math.Abs(strikes[atmIndex] - forward))
                    {
                        atmIndex = strike;
                    }
                }

                Assert.True(Math.Abs(strikes[atmIndex] - forward) < 1.0e-12, label);
                double annuity = accrual * curve.Discount(terminal);
                double uncappedCall = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, strikes[atmIndex], forward, stdDev, annuity, shift);
                double callAboveUpperBound = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, upperBound, forward, stdDev, annuity, shift);
                double expectedCappedCall = uncappedCall - callAboveUpperBound;
                double modelCall = outputs.ModelCallPremium[0][atmIndex];
                Assert.True(double.IsFinite(modelCall), label);
                Assert.True(
                    Math.Abs(modelCall - expectedCappedCall) < 1.0e-5,
                    $"{label}: model call {modelCall}, expected {expectedCappedCall}");
            }
        }
    }

    [Fact]
    public void TestPayoffExtrapolation()
    {
        // Upstream: QuantLib's master's case for #2912 (markovfunctional.cpp's testPayoffExtrapolation)
        Date expiry = new TARGET().Advance(ReferenceDate, 1, TimeUnit.Years);
        foreach (bool caplet in new[] { false, true })
        {
            foreach (MarkovFunctionalModelSettingsAdjustments extrapolation in new[] { ExtrapolatePayoffFlat, NoPayoffExtrapolation })
            {
                MarkovFunctionalModelSettings settings = new MarkovFunctionalModelSettings()
                    .WithYGridPoints(64)
                    .WithGaussHermitePoints(32)
                    .WithAdjustments(KahaleSmile | SmileExponentialExtrapolation | extrapolation)
                    .WithSmileMoneynessCheckpoints(Checkpoints);
                MarkovFunctional model = caplet
                    ? new MarkovFunctional(
                        MarkovFunctionalTests.FlatYts(Context),
                        0.01,
                        [],
                        [1.0],
                        MarkovFunctionalTests.FlatOptionletVts(Context),
                        [expiry],
                        new Euribor(new Period(6, TimeUnit.Months)),
                        Context,
                        settings)
                    : new MarkovFunctional(
                        MarkovFunctionalTests.FlatYts(Context),
                        0.01,
                        [],
                        [1.0],
                        MarkovFunctionalTests.FlatSwaptionVts(Context),
                        [expiry],
                        [new Period(1, TimeUnit.Years)],
                        new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)),
                        Context,
                        settings);
                MarkovFunctionalModelOutputs outputs = model.ModelOutputs(Context);
                Assert.Single(outputs.Expiries);
                Assert.NotEmpty(outputs.SmileStrikes[0]);
                Assert.True(Math.Abs(outputs.ModelZerorate[0] - outputs.MarketZerorate[0]) < 1.0e-4);
                AssertPremiumsFit(outputs);
            }
        }
    }

    private static void AssertPremiumsFit(MarkovFunctionalModelOutputs outputs)
    {
        for (int strike = 0; strike < outputs.SmileStrikes[0].Length; ++strike)
        {
            string label = $"strike={outputs.SmileStrikes[0][strike]}";
            Assert.True(double.IsFinite(outputs.ModelCallPremium[0][strike]), label);
            Assert.True(double.IsFinite(outputs.ModelPutPremium[0][strike]), label);
            Assert.True(Math.Abs(outputs.ModelCallPremium[0][strike] - outputs.MarketCallPremium[0][strike]) < 1.0e-4, label);
            Assert.True(Math.Abs(outputs.ModelPutPremium[0][strike] - outputs.MarketPutPremium[0][strike]) < 1.0e-4, label);
        }
    }

    // A factory the settings' validation must never call.
    private sealed class ValidationOnlySmileFactory : MarkovFunctionalCustomSmileFactory
    {
        public override MarkovFunctionalCustomSmileSection SmileSection(SmileSection source, double atm) =>
            throw new InvalidOperationException("smile factory must not be called during settings validation");
    }
}
