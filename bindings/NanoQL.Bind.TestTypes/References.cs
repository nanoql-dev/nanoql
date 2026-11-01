// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// The bound-engine cases' C# routes: each member given the engine as C#'s lambda, the result a conformance suite
/// compares the bound engine's with, bit for bit.
/// </summary>
public static class References
{
    /// <summary>The swap builder's fixed rate, solved through the discounting engine as C#'s lambda.</summary>
    /// <returns>The fixed rate.</returns>
    public static double SwapFixedRate() =>
        Fixtures.SwapBuilder.WithPricingEngine((s, c) => DiscountingSwapEngine.Calculate(s, c, Fixtures.RiskFree)).ToVanillaSwap(Fixtures.Context).FixedRate;

    /// <summary>The Heston helper's model value through the analytic Heston engine.</summary>
    /// <returns>The model value.</returns>
    public static double HestonModelValue() =>
        Fixtures.HestonHelper.SetPricingEngine<HestonModel>((o, c, m) => AnalyticHestonEngine.Calculate(o, c, m)).ModelValue(Fixtures.Heston, Fixtures.Context);

    /// <summary>The Heston helper's model value through the H1-HW engine, the Hull-White model bound and a correlation of 0.6.</summary>
    /// <returns>The model value.</returns>
    public static double H1HWModelValue() =>
        Fixtures.HestonHelper.SetPricingEngine<HestonModel>((o, c, m) => AnalyticH1HWEngine.Calculate(o, c, m, Fixtures.HullWhite, 0.6)).ModelValue(Fixtures.Heston, Fixtures.Context);

    /// <summary>The Heston helper's model value through the Heston–Hull-White engine, the Hull-White model bound.</summary>
    /// <returns>The model value.</returns>
    public static double HestonHullWhiteModelValue() =>
        Fixtures.HestonHelper.SetPricingEngine<HestonModel>((o, c, m) => AnalyticHestonHullWhiteEngine.Calculate(o, c, m, Fixtures.HullWhite)).ModelValue(Fixtures.Heston, Fixtures.Context);

    /// <summary>The quanto call's NPV, the process going to the analytic European engine.</summary>
    /// <returns>The NPV.</returns>
    public static double QuantoNpv() =>
        QuantoEngine.Calculate(Fixtures.Quanto, Fixtures.Context, Fixtures.BlackScholes, Fixtures.Foreign, Fixtures.ExchangeVolatility, 0.3,
            (o, c, p) => AnalyticEuropeanEngine.Calculate(o, c, p)).NPV!.Value;

    /// <summary>The YoY optionlet helper's implied quote on the market's surface, through the unit-displaced Black engine.</summary>
    /// <returns>The implied quote.</returns>
    public static double YoYImpliedQuote()
    {
        YoYMarket m = YoYMarket.Instance;
        return new YoYOptionletHelper(
                1.0, 10000.0, YoYInflationCapFloorType.Cap, new Period(3, TimeUnit.Months), new Actual365Fixed(), new TARGET(),
                0, m.Index, CPIInterpolationType.Flat, 0.03, 3,
                (cf, c, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(cf, c, m.Index, v, m.Nominal), m.Context)
            .SetTermStructure(m.Surface).ImpliedQuote(m.Context);
    }

    /// <summary>The callable bond's clean price at a zero spread less two, through the tree engine: the OAS case's price.</summary>
    /// <returns>The price.</returns>
    public static double OasPrice() => Fixtures.CallableBond.CleanPrice(Tree(Fixtures.CallableBond, Fixtures.BondContext, 0.0), Fixtures.BondContext) - 2.0;

    /// <summary>The callable bond's OAS at <see cref="OasPrice"/>, the spread going to the tree engine's <c>spread</c>.</summary>
    /// <returns>The OAS.</returns>
    public static double Oas() =>
        Fixtures.CallableBond.OAS(OasPrice(), Fixtures.BondCurve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Tree, Fixtures.BondContext);

    /// <summary>The callable bond's clean price at a 1% OAS through the tree engine.</summary>
    /// <returns>The clean price.</returns>
    public static double CleanPriceOas() =>
        Fixtures.CallableBond.CleanPriceOAS(0.01, Fixtures.BondCurve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Tree, Fixtures.BondContext);

    /// <summary>The callable bond's effective duration at a 1% OAS through the tree engine.</summary>
    /// <returns>The effective duration.</returns>
    public static double EffectiveDuration() =>
        Fixtures.CallableBond.EffectiveDuration(0.01, Fixtures.BondCurve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Tree, Fixtures.BondContext);

    /// <summary>The double barrier's Vanna–Volga NPV, the process and series going to the analytic double barrier engine.</summary>
    /// <returns>The NPV.</returns>
    public static double VannaVolgaNpv() =>
        VannaVolgaDoubleBarrierEngine.Calculate(
            Fixtures.DoubleBarrier, Fixtures.Context, AnalyticDoubleBarrierEngine.Calculate, Fixtures.AtmQuote, Fixtures.Put25, Fixtures.Call25,
            100.0, Fixtures.FxCurve, Fixtures.FxCurve).NPV!.Value;

    /// <summary>The 2x2 swaption helper's model value with GSR, through the Gaussian1d swaption engine as C#'s lambda.</summary>
    /// <returns>The model value.</returns>
    public static double GsrSwaptionModelValue() =>
        Fixtures.Swaptions[1].SetPricingEngine<Gsr>((s, c, m) => Gaussian1dSwaptionEngine.Calculate(s, c, m)).ModelValue(Fixtures.Gsr, Fixtures.CalibrationContext);

    /// <summary>The 5-year cap helper's model value with Hull-White, through the analytic cap engine as C#'s lambda.</summary>
    /// <returns>The model value.</returns>
    public static double HullWhiteCapModelValue() =>
        Fixtures.Caps[1].SetPricingEngine<OneFactorAffineModel>((cf, x, m) => AnalyticCapFloorEngine.Calculate(cf, x, m))
            .ModelValue(Fixtures.CalibrationHullWhite, Fixtures.CalibrationContext);

    /// <summary>A line search's step on a boundary constraint, as C# moves its point in place.</summary>
    /// <returns>The point after the step.</returns>
    public static ImmutableArray<double> ConstraintUpdate()
    {
        var p = new Vector([0.5, 0.2]);
        _ = new BoundaryConstraint(-1.0, 1.0).Update(p, new Vector([0.1, -0.3]), 1.0);
        return [.. p.AsSpan().ToArray()];
    }

    /// <summary>GSR's iterative volatility calibration on the swaption basket through the Gaussian1d swaption engine.</summary>
    /// <returns>The calibrated parameters.</returns>
    public static ImmutableArray<double> GsrVolatilities()
    {
        SwaptionHelper[] basket = [.. Fixtures.Swaptions.Select(h => h.SetPricingEngine<Gsr>((s, c, m) => Gaussian1dSwaptionEngine.Calculate(s, c, m)))];
        return [.. Fixtures.Gsr.CalibrateVolatilitiesIterative(basket, Fixtures.Method, Fixtures.Criteria, Fixtures.CalibrationContext).Model.Params().ToArray()];
    }

    /// <summary>Hull-White's calibration on the cap basket through the analytic cap engine.</summary>
    /// <returns>The calibrated parameters.</returns>
    public static ImmutableArray<double> HullWhiteCaps()
    {
        CapHelper[] caps = [.. Fixtures.Caps.Select(h => h.SetPricingEngine<OneFactorAffineModel>((cf, x, m) => AnalyticCapFloorEngine.Calculate(cf, x, m)))];
        return [.. Fixtures.CalibrationHullWhite.Calibrate(caps, Fixtures.Method, Fixtures.Criteria, Fixtures.CalibrationContext).Model.Params().ToArray()];
    }

    private static CallableBondResults Tree(CallableBond b, EvaluationContext x, double s) =>
        TreeCallableFixedRateBondEngine.Calculate(b, x, Fixtures.BondModel, 40, null, s);
}
