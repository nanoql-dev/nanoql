// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The European Margrabe engine's Greeks at quantities other than 1. QuantLib's gammas drop the quantities and its
/// theta takes them squared (<c>analyticeuropeanmargrabeengine.cpp:101-104</c>): a first gamma of 0.01154 against
/// 0.02307 bumped at Q₁ = 2, a second of 0.02136 against 0.06409 at Q₂ = 3, and a theta of −8.361 against −9.013 by a
/// date bump, on the test's market; QuantLib's own case pins the spreadsheet's values, which reproduce its formulas
/// (<c>margrabeoption.cpp:250</c>, <c>:259</c>, <c>:268</c>, moved here). nanoql takes each quantity once: QuantLib's
/// own prices bumped are the check, and its bits at quantities of 1. QuantLib's master makes the same fix after the
/// pin (#2918, <c>17a720edf</c>), upstream's three expressions token for token, and restates the spreadsheet rows'
/// gammas and theta, the rows above them with each spot divided by its quantity: its rows are below, as a check of
/// the fix.
/// </summary>
public class MargrabeGreeksQuantityTests
{
    private static readonly Date Today = new(16, Month.September, 2015);
    private static readonly DayCounter Dc = new Actual360();
    private static readonly Date ExDate = Today + 180;

    [Fact]
    public void EachGammaIsItsDeltasDerivative()
    {
        foreach ((int q1, int q2) in new[] { (2, 1), (1, 3), (2, 3) })
        {
            var option = new MargrabeOption(q1, q2, new EuropeanExercise(ExDate));
            MargrabeOptionResults r = Price(option, 22.0, 20.0, Today);
            const double du = 22.0 * 1.0e-4;
            double gamma1 = (Price(option, 22.0 + du, 20.0, Today).Delta1!.Value
                - Price(option, 22.0 - du, 20.0, Today).Delta1!.Value) / (2 * du);
            double gamma2 = (Price(option, 22.0, 20.0 + du, Today).Delta2!.Value
                - Price(option, 22.0, 20.0 - du, Today).Delta2!.Value) / (2 * du);
            Assert.Equal(gamma1, r.Gamma1!.Value, 1e-6);
            Assert.Equal(gamma2, r.Gamma2!.Value, 1e-6);
        }
    }

    [Fact]
    public void TheThetaIsTheValuesDecay()
    {
        foreach ((int q1, int q2) in new[] { (2, 1), (1, 3), (2, 3) })
        {
            var option = new MargrabeOption(q1, q2, new EuropeanExercise(ExDate));
            double theta = Price(option, 22.0, 20.0, Today).Theta!.Value;
            double dT = Dc.YearFraction(Today - 1, Today + 1);
            double expected = (Price(option, 22.0, 20.0, Today + 1).NPV!.Value
                - Price(option, 22.0, 20.0, Today - 1).NPV!.Value) / dT;
            Assert.Equal(expected, theta, 1e-4 * 22.0);
        }
    }

    [Theory]
    [InlineData(22.0, 10.0, 1, 2, 0.15, 0.106, 0.511, -0.416)]
    [InlineData(11.0, 20.0, 2, 1, 0.20, 0.397, 0.120, -0.550)]
    [InlineData(11.0, 10.0, 2, 2, 0.25, 0.361, 0.437, -0.741)]
    public void TestEuroExchangeTwoAssetsQuantityRows(
        double s1, double s2, int q1, int q2, double v2, double gamma1, double gamma2, double theta)
    {
        // Upstream: QuantLib's master's rows for #2918 (margrabeoption.cpp's testEuroExchangeTwoAssets, its quantity
        // tests): q1 6%, q2 4%, r 10%, half a year, v1 20%, rho 0.5, tolerance 1e-3
        var context = new EvaluationContext { EvaluationDate = Today };
        YieldTermStructure rTS = new FlatForward(Today, 0.10, Dc);
        var p1 = new BlackScholesMertonProcess(
            s1, new FlatForward(Today, 0.06, Dc), rTS, new BlackConstantVol(Today, new NullCalendar(), 0.20, Dc));
        var p2 = new BlackScholesMertonProcess(
            s2, new FlatForward(Today, 0.04, Dc), rTS, new BlackConstantVol(Today, new NullCalendar(), v2, Dc));
        MargrabeOptionResults r = AnalyticEuropeanMargrabeEngine.Calculate(
            new MargrabeOption(q1, q2, new EuropeanExercise(ExDate)), context, p1, p2, 0.5);
        Assert.True(Math.Abs(r.Gamma1!.Value - gamma1) <= 1.0e-3, $"gamma1 {r.Gamma1}");
        Assert.True(Math.Abs(r.Gamma2!.Value - gamma2) <= 1.0e-3, $"gamma2 {r.Gamma2}");
        Assert.True(Math.Abs(r.Theta!.Value - theta) <= 1.0e-3, $"theta {r.Theta}");
    }

    // QuantLib's testGreeks market: curves moving with the evaluation date.
    private static MargrabeOptionResults Price(MargrabeOption option, double s1, double s2, Date evaluationDate)
    {
        var context = new EvaluationContext { EvaluationDate = evaluationDate };
        YieldTermStructure rTS = new FlatForward(evaluationDate, 0.1, Dc);
        var vol = new BlackConstantVol(evaluationDate, new NullCalendar(), 0.2, Dc);
        var p1 = new BlackScholesMertonProcess(s1, new FlatForward(evaluationDate, 0.06, Dc), rTS, vol);
        var p2 = new BlackScholesMertonProcess(s2, new FlatForward(evaluationDate, 0.04, Dc), rTS, vol);
        return AnalyticEuropeanMargrabeEngine.Calculate(option, context, p1, p2, -0.5);
    }
}
