// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An optional real input is a <c>double?</c>, whose <see langword="null"/> is "not given", so a value equal to
/// QuantLib's null sentinel, <c>Null&lt;Real&gt;()</c> (3.4028234663852886e38, the largest <c>float</c>), is a value.
/// QuantLib reads its sentinel as "not given" wherever a real input defaults to it. Each fact is one such input.
/// </summary>
public class NullRealSentinelTests
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>
    /// An iterative bootstrap's bound: QuantLib takes the traits' bound for a <c>maxValue</c> of the sentinel
    /// (<c>iterativebootstrap.hpp:276-279</c>), so its curve is the default's, 0.8821578172517457 at four years. nanoql
    /// searches up to that value, and its solver lands elsewhere within the accuracy.
    /// </summary>
    [Fact]
    public void AnIterativeBootstrapsBoundOfQuantLibsNullIsAValue()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                context));
        }

        var dc = new Actual365Fixed();
        var byDefault = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(today, helpers, dc, context);
        var bounded = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, helpers, dc, context, bootstrap: new IterativeBootstrap(maxValue: NullReal));

        // QuantLib's value for both
        Assert.Equal(0.8821578172517457, byDefault.Discount(4.0));
        Assert.Equal(0.8821578172517482, bounded.Discount(4.0));
    }

    /// <summary>
    /// A mixed interpolation's left condition value: QuantLib reads a first-derivative condition whose value is the
    /// sentinel as "match the cubic's derivative at the switch point to the linear part's"
    /// (<c>mixedinterpolation.hpp:73-83</c>), and refuses it under <c>ShareRanges</c>, "matching derivatives is only
    /// supported with SplitRanges". nanoql's <see langword="null"/> is that reading; the sentinel is a first derivative.
    /// </summary>
    [Fact]
    public void AMixedInterpolationsLeftConditionValueOfQuantLibsNullIsAValue()
    {
        double[] x = [0.0, 0.5, 1.0, 2.0, 3.0];
        double[] y = [1.0, 0.98, 0.95, 0.91, 0.86];
        const CubicInterpolationDerivativeApprox spline = CubicInterpolationDerivativeApprox.Spline;
        const CubicInterpolationBoundaryCondition first = CubicInterpolationBoundaryCondition.FirstDerivative;
        const CubicInterpolationBoundaryCondition second = CubicInterpolationBoundaryCondition.SecondDerivative;

        // ShareRanges: QuantLib refuses the sentinel, as nanoql refuses null; nanoql builds on the value.
        Assert.Throws<ArgumentException>(
            () => new LogMixedLinearCubic(1, MixedInterpolationBehavior.ShareRanges, spline, false, first, null)
                .Interpolate(x, y));
        Interpolation shared = new LogMixedLinearCubic(
            1, MixedInterpolationBehavior.ShareRanges, spline, false, first, NullReal).Interpolate(x, y);
        Assert.Equal(1.0, shared.Evaluate(0.0));

        // SplitRanges: QuantLib matches the derivatives for the sentinel, as nanoql does for null; the value differs.
        Interpolation matched = new LogMixedLinearCubic(
            1, MixedInterpolationBehavior.SplitRanges, spline, false, first, null, second).Interpolate(x, y);
        Interpolation valued = new LogMixedLinearCubic(
            1, MixedInterpolationBehavior.SplitRanges, spline, false, first, NullReal, second).Interpolate(x, y);
        Assert.Equal(matched.Derivative(0.5 - 1e-9), matched.Derivative(0.5 + 1e-9), 1e-6);
        Assert.NotEqual(matched.Derivative(0.5 + 1e-9), valued.Derivative(0.5 + 1e-9));
    }

    /// <summary>
    /// A global bootstrap's accuracy: QuantLib takes the curve's, 1e-12, for an accuracy of the sentinel
    /// (<c>globalbootstrap.hpp:255</c>), so its curve is the default's, 0.8822716504192525 at four years. nanoql's
    /// optimizer and end criteria take that value as their tolerances, and stop at once, at the traits' guess: a flat 5%
    /// curve, exp(-0.05 × 4) at four years.
    /// </summary>
    [Fact]
    public void AGlobalBootstrapsAccuracyOfQuantLibsNullIsAValue()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032), (10, 0.033) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                context));
        }

        var dc = new Actual365Fixed();
        var referenceDate = new Date(17, Month.January, 2024);
        var byDefault = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(referenceDate, helpers, dc, context);
        var sentinel = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            referenceDate, helpers, dc, context, bootstrap: new GlobalBootstrap(NullReal));

        // QuantLib's curve is the first for both; nanoql's sentinel curve is the traits' guess
        Assert.Equal(0.8822716504192525, byDefault.Discount(4.0));
        Assert.Equal(0.8187307530779818, sentinel.Discount(4.0));
    }

    /// <summary>
    /// An implied standard deviation's guess: QuantLib's <c>blackFormulaImpliedStdDev</c> reads a guess of the sentinel
    /// as no guess and starts from the approximation (<c>blackformula.cpp:430-432</c>), so its result is the no-guess
    /// one, 0.1999993088780617 for these inputs. nanoql's guess of the sentinel is a guess, which the solver refuses,
    /// being above its upper bound, 24.
    /// </summary>
    [Fact]
    public void AnImpliedStandardDeviationsGuessOfQuantLibsNullIsAGuess()
    {
        double price = BlackFormula(OptionType.Call, 100.0, 110.0, 0.2);

        ArgumentException e = Assert.Throws<ArgumentException>(() =>
            BlackFormulaImpliedStdDev(OptionType.Call, 100.0, 110.0, price, 1.0, 0.0, NullReal));
        Assert.Equal("guess", e.ParamName);
        Assert.StartsWith("guess (3.4028234663852886E+38) > xMax_ (24)", e.Message, StringComparison.Ordinal);

        // Only null is no guess: the result QuantLib gives for the sentinel.
        Assert.Equal(0.1999993088780617, BlackFormulaImpliedStdDev(OptionType.Call, 100.0, 110.0, price, 1.0, 0.0, null));
    }

    /// <summary>
    /// A smile section's ATM level: QuantLib's <c>FlatSmileSection</c> stores <c>atmLevel = Null&lt;Rate&gt;()</c> by
    /// default, and its <c>optionPrice</c>, <c>vega</c> and converted volatility read an ATM level equal to the sentinel
    /// as none (<c>smilesection.cpp:78-80, 120-122, 135-137</c>), refusing "smile section must provide atm level to
    /// compute option price" for it. nanoql's ATM level of the sentinel is a level: the call on a forward that large is
    /// worth about the forward.
    /// </summary>
    [Fact]
    public void ASmileSectionsAtmLevelOfQuantLibsNullIsALevel()
    {
        var section = new FlatSmileSection(1.0, 0.2, null, NullReal);

        Assert.Equal(NullReal, section.AtmLevel());
        double price = section.OptionPrice(0.03);
        Assert.True(double.IsFinite(price) && price > 0.999 * NullReal, $"price {price}");

        // Only null is no ATM level: the refusal QuantLib gives for the sentinel.
        var none = new FlatSmileSection(1.0, 0.2, null);
        Assert.Null(none.AtmLevel());
        Assert.Equal(
            "smile section must provide atm level to compute option price",
            Assert.Throws<NanoQLException>(() => none.OptionPrice(0.03)).Message);
    }
}
