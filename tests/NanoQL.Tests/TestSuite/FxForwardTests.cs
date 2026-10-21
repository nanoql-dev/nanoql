// SPDX-FileCopyrightText: Copyright (C) 2026 Chirag Desai
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fxforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// QuantLib's relinkable handles are the curves and the spot passed to the engine: a case that relinks one prices again
// with the new value.
[Trait("Oracle", "QuantLib")]
public class FxForwardTests
{
    [Fact]
    public void TestFxForwardConstruction()
    {
        // Testing FX-forward construction...

        var vars = new CommonVars();

        // Test constructor with two nominals
        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd1 = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true); // pay USD, receive SGD

        Assert.Equal(usdNominal, fwd1.SourceNominal);
        Assert.Equal(sgdNominal, fwd1.TargetNominal);
        Assert.True(fwd1.SourceCurrency == vars.Usd);
        Assert.True(fwd1.TargetCurrency == vars.Sgd);
        Assert.Equal(vars.MaturityDate, fwd1.MaturityDate);
        Assert.True(fwd1.PaySourceCurrency);
        Assert.False(fwd1.IsExpired(vars.Context));
    }

    [Fact]
    public void TestFxForwardConstructionWithRate()
    {
        // Testing FX-forward construction with rate...

        var vars = new CommonVars();

        double nominal = 1000000.0;
        double forwardRate = 1.36; // SGD/USD forward rate

        var fwd = new FxForward(nominal, vars.Usd, vars.Sgd, forwardRate, vars.MaturityDate, true); // sell USD

        Assert.Equal(nominal, fwd.SourceNominal);
        CheckClose(fwd.TargetNominal, nominal * forwardRate, 1.0e-4); // 0.0001% tolerance
        Assert.True(fwd.SourceCurrency == vars.Usd);
        Assert.True(fwd.TargetCurrency == vars.Sgd);
    }

    [Fact]
    public void TestContractedForwardRate()
    {
        // Testing FX-forward contracted rate...

        var vars = new CommonVars();

        // Test with explicit nominals
        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;
        double expectedRate = sgdNominal / usdNominal; // 1.35

        var fwd1 = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        CheckClose(fwd1.ForwardRate, expectedRate, 1.0e-10);

        // Test with rate constructor
        double inputRate = 1.36;
        var fwd2 = new FxForward(usdNominal, vars.Usd, vars.Sgd, inputRate, vars.MaturityDate, true);

        CheckClose(fwd2.ForwardRate, inputRate, 1.0e-10);

        // Verify contracted rate differs from fair forward rate
        FxForwardResults results = vars.Price(fwd1);

        double fairRate = results.FairForwardRate!.Value;
        double contractedRate = fwd1.ForwardRate;

        // These should generally be different unless the contract is at fair value
        Assert.True(contractedRate != fairRate);
    }

    [Fact]
    public void TestFxForwardExpiry()
    {
        // Testing FX-forward expiry...

        var vars = new CommonVars();

        // Create a forward that has already expired
        Date pastDate = vars.Today - 1;

        var expiredFwd = new FxForward(1000000.0, vars.Usd, 1350000.0, vars.Sgd, pastDate, true);

        Assert.True(expiredFwd.IsExpired(vars.Context));
    }

    [Fact]
    public void TestDiscountingFxForwardEngine()
    {
        // Testing discounting FX-forward engine...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true); // pay USD, receive SGD

        FxForwardResults results = vars.Price(fwd);

        // Check that NPV is computed
        Assert.NotNull(results.NPV);

        // Check that fair forward rate is computed
        Assert.True(results.FairForwardRate > 0.0);
    }

    [Fact]
    public void TestFairForwardRate()
    {
        // Testing fair forward rate calculation...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        // Fair forward rate = Spot * (DFsource / DFtarget)
        // The engine calculates discount factors from settlement date to maturity
        // With USD as source currency (domestic) and SGD as target currency (foreign):
        // F = S * (DF_USD / DF_SGD)
        Date settlementDate = fwd.SettlementDate(vars.Context);
        double spotFx = vars.SpotFx;
        double dfUsd = vars.UsdCurve.Discount(vars.MaturityDate) / vars.UsdCurve.Discount(settlementDate);
        double dfSgd = vars.SgdCurve.Discount(vars.MaturityDate) / vars.SgdCurve.Discount(settlementDate);
        double expectedFairRate = spotFx * dfUsd / dfSgd;

        double calculatedFairRate = vars.Price(fwd).FairForwardRate!.Value;

        CheckClose(calculatedFairRate, expectedFairRate, 1.0e-4); // 0.0001% tolerance

        var atmFwd = new FxForward(usdNominal, vars.Usd, vars.Sgd, calculatedFairRate, vars.MaturityDate, true);

        CheckSmall(vars.Price(atmFwd).NPV!.Value, 1.0e-4);
    }

    [Fact]
    public void TestAtTheMoney()
    {
        // Testing that an FX forward at-the-money has zero NPV...

        var vars = new CommonVars();

        // For an ATM forward, we need NPV = 0
        // The engine calculates discount factors from settlement date to maturity:
        //   dfSource = DF(maturity) / DF(settlement)
        //   dfTarget = DF(maturity) / DF(settlement)
        // NPV = -sourceNominal * dfSource + targetNominal * dfTarget / spotFx = 0
        // Solving: targetNominal = sourceNominal * dfSource * spotFx / dfTarget
        double spotFx = vars.SpotFx;

        // We need to use a forward with default settlement (2 days) to compute the correct DFs
        double usdNominal = 1000000.0;

        // Create a temporary forward to get the settlement date
        var tempFwd = new FxForward(usdNominal, vars.Usd, usdNominal, vars.Sgd, vars.MaturityDate, true);
        Date settlementDate = tempFwd.SettlementDate(vars.Context);

        // Calculate discount factors from settlement to maturity (as the engine does)
        double dfUsd = vars.UsdCurve.Discount(vars.MaturityDate) / vars.UsdCurve.Discount(settlementDate);
        double dfSgd = vars.SgdCurve.Discount(vars.MaturityDate) / vars.SgdCurve.Discount(settlementDate);

        // Create a forward at the ATM strike
        // ATM condition: targetNominal = sourceNominal * dfSource * spotFx / dfTarget
        double sgdNominal = usdNominal * dfUsd * spotFx / dfSgd;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        // At-the-money forward should have NPV close to zero
        double npv = vars.Price(fwd).NPV!.Value;

        CheckSmall(npv, 1.0e-4); // NPV should be essentially zero
    }

    [Fact]
    public void TestPositionDirection()
    {
        // Testing FX-forward position direction...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        // Long USD (pay SGD, receive USD) - paySourceCurrency = false
        var longUsd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, false);

        // Short USD (pay USD, receive SGD) - paySourceCurrency = true
        var shortUsd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        double npvLong = vars.Price(longUsd).NPV!.Value;
        double npvShort = vars.Price(shortUsd).NPV!.Value;

        // Long and short positions should have opposite NPVs
        CheckClose(npvLong, -npvShort, 1.0e-4); // 0.0001% tolerance
    }

    [Fact]
    public void TestIRCurveSensitivity()
    {
        // Testing FX-forward sensitivity to IR curves...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        double npvBase = vars.Price(fwd).NPV!.Value;

        // Shift USD curve up by 10bp
        vars.UsdCurve = FlatRate(vars.Today, 0.051, new Actual365Fixed());
        double npvUsdUp = vars.Price(fwd).NPV!.Value;

        // Reset USD curve and shift SGD curve up by 10bp
        vars.UsdCurve = FlatRate(vars.Today, 0.05, new Actual365Fixed());
        vars.SgdCurve = FlatRate(vars.Today, 0.036, new Actual365Fixed());
        double npvSgdUp = vars.Price(fwd).NPV!.Value;

        // When paying USD (source) and receiving SGD (target):
        // - Higher USD rates -> lower DF for USD leg -> less negative PV for paying USD -> NPV
        // increases
        // - Higher SGD rates -> lower DF for SGD leg -> less positive PV for receiving SGD -> NPV
        // decreases
        Assert.True(npvUsdUp > npvBase);
        Assert.True(npvSgdUp < npvBase);
    }

    [Fact]
    public void TestSpotFxSensitivity()
    {
        // Testing FX-forward sensitivity to spot FX...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true); // pay USD, receive SGD

        double npvBase = vars.Price(fwd).NPV!.Value;

        // Increase spot FX (spot = SGD/USD, so higher spot means USD strengthens / SGD weakens)
        vars.SpotFx = 1.40;
        double npvSpotUp = vars.Price(fwd).NPV!.Value;

        // Decrease spot FX (USD weakens / SGD strengthens)
        vars.SpotFx = 1.30;
        double npvSpotDown = vars.Price(fwd).NPV!.Value;

        // Spot FX convention: spotFx = SGD/USD (1 USD = spotFx SGD)
        // When paying USD (source) and receiving SGD (target):
        // - Target leg PV in source currency terms = SGD_PV / spotFx
        // - If spot goes UP (USD strengthens), SGD is worth LESS in USD terms, NPV DECREASES
        // - If spot goes DOWN (USD weakens), SGD is worth MORE in USD terms, NPV INCREASES
        Assert.True(npvSpotUp < npvBase);
        Assert.True(npvSpotDown > npvBase);
    }

    [Fact]
    public void TestAdditionalResults()
    {
        // Testing FX-forward additional results...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true);

        // Trigger calculation
        FxForwardResults results = vars.Price(fwd);

        // Check additional results are available
        var additionalResults = results.AdditionalResults;

        Assert.True(additionalResults.ContainsKey("spotFx"));
        Assert.True(additionalResults.ContainsKey("sourceCurrencyDiscountFactor"));
        Assert.True(additionalResults.ContainsKey("targetCurrencyDiscountFactor"));

        double spotFx = results.Result<double>("spotFx");
        double dfSource = results.Result<double>("sourceCurrencyDiscountFactor");
        double dfTarget = results.Result<double>("targetCurrencyDiscountFactor");

        CheckClose(spotFx, 1.35, 1.0e-4); // 0.0001% tolerance
        Assert.True(dfSource > 0.0 && dfSource < 1.0);
        Assert.True(dfTarget > 0.0 && dfTarget < 1.0);
    }

    [Fact]
    public void TestSettlementDays()
    {
        // Testing FX-forward settlement days...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        // Test different settlement conventions
        // Overnight (O/N): 0 days
        var overnightFwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true, 0);
        Assert.Equal(0, overnightFwd.SettlementDays);
        Assert.Equal(vars.Today, overnightFwd.SettlementDate(vars.Context));

        // TomNext (T/N): 1 day
        var tomNextFwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true, 1);
        Assert.Equal(1, tomNextFwd.SettlementDays);
        Assert.Equal(vars.Today + 1, tomNextFwd.SettlementDate(vars.Context));

        // Spot (S/N): 2 days (default)
        var spotFwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true); // default is 2
        Assert.Equal(2, spotFwd.SettlementDays);
        Assert.Equal(vars.Today + 2, spotFwd.SettlementDate(vars.Context));
    }

    [Fact]
    public void TestSettlementDaysWithCalendar()
    {
        // Testing FX-forward settlement days with calendar...

        var vars = new CommonVars();

        double usdNominal = 1000000.0;
        double sgdNominal = 1350000.0;

        // Use a calendar that skips weekends
        Calendar cal = new TARGET();

        // Find a Friday to test weekend skipping
        var friday = new Date(15, Month.March, 2024); // March 15, 2024 is a Friday
        EvaluationContext context = vars.Context with { EvaluationDate = friday };

        // With 2 settlement days on a Friday, settlement should be Tuesday (skipping weekend)
        var fwd = new FxForward(usdNominal, vars.Usd, sgdNominal, vars.Sgd, vars.MaturityDate, true, 2, cal);

        Date expectedSettlementDate = cal.Advance(friday, 2, TimeUnit.Days);
        Assert.Equal(expectedSettlementDate, fwd.SettlementDate(context));
    }

    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) => new FlatForward(today, forward, dc);

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |value| within the tolerance.
    private static void CheckSmall(double value, double tolerance) =>
        Assert.True(Math.Abs(value) <= tolerance, $"{value} is not small, tolerance {tolerance}");

    private sealed class CommonVars
    {
        public CommonVars()
        {
            Today = new Date(15, Month.March, 2024);
            Context = new EvaluationContext { EvaluationDate = Today };
            MaturityDate = Today + (6 * TimeUnit.Months);

            Usd = new USDCurrency();
            Sgd = new SGDCurrency();

            // USD discount rate: 5%
            UsdCurve = FlatRate(Today, 0.05, new Actual365Fixed());

            // SGD discount rate: 3.5%
            SgdCurve = FlatRate(Today, 0.035, new Actual365Fixed());

            // Spot FX rate: 1.35 SGD/USD (1 USD = 1.35 SGD)
            SpotFx = 1.35;
        }

        public Date Today { get; }

        public EvaluationContext Context { get; }

        public Date MaturityDate { get; }

        public Currency Usd { get; }

        public Currency Sgd { get; }

        public YieldTermStructure UsdCurve { get; set; }

        public YieldTermStructure SgdCurve { get; set; }

        public double SpotFx { get; set; }

        public FxForwardResults Price(FxForward fwd) =>
            DiscountingFxForwardEngine.Calculate(fwd, Context, UsdCurve, SgdCurve, SpotFx);
    }
}
