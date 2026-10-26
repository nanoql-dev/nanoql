// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The cliquet and performance engines' theta. QuantLib's reads each period's rate over the period after its reset
/// (<c>analyticcliquetengine.cpp:92-94</c>, <c>analyticperformanceengine.cpp:91-93</c>), where the period's value decays
/// with its weight over the time before the reset: a one-period cliquet's theta 0.2813 where QuantLib's forward
/// engine's is 0.0 on a dividend curve flat at zero before the reset. nanoql reads the rate to each reset (the S-exo
/// plan's ER5 (xxii)). The check is QuantLib's forward and forward-performance engines on each period's forward-start
/// option: the values and thetas summed period by period, on the rising market, whose dividends are 0 to three months.
/// </summary>
public class CliquetThetaTests
{
    private static readonly Date[] Resets = [Today + 91, Today + 182];
    private static readonly Date Maturity = Today + 365;

    [Fact]
    public void TheCliquetThetaIsTheForwardEnginesPeriodByPeriod()
    {
        GeneralizedBlackScholesProcess p = Rising(100.0);
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            OneAssetOptionResults cliquet = AnalyticCliquetEngine.Calculate(Cliquet(type), Context, p);
            (double value, double theta) = Periods(type, p, ForwardVanillaEngine.Calculate);
            Assert.Equal(value, cliquet.NPV!.Value, 1e-12 * value);
            Assert.Equal(theta, cliquet.Theta!.Value, 1e-12 * Math.Abs(theta));
        }
    }

    [Fact]
    public void ThePerformanceThetaIsTheForwardPerformanceEnginesPeriodByPeriod()
    {
        GeneralizedBlackScholesProcess p = Rising(100.0);
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            OneAssetOptionResults performance = AnalyticPerformanceEngine.Calculate(Cliquet(type), Context, p);
            (double value, double theta) = Periods(type, p, ForwardPerformanceVanillaEngine.Calculate);
            Assert.Equal(value, performance.NPV!.Value, 1e-12 * value);
            Assert.Equal(theta, performance.Theta!.Value, 1e-12 * Math.Abs(theta));
        }
    }

    private static CliquetOption Cliquet(OptionType type) =>
        new(new PercentageStrikePayoff(type, 1.05), new EuropeanExercise(Maturity), Resets);

    // Each period's forward-start option, from its reset to the next or to maturity, priced by a forward engine.
    private static (double Value, double Theta) Periods(
        OptionType type,
        GeneralizedBlackScholesProcess p,
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess,
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>,
            OneAssetOptionResults> engine)
    {
        Date[] dates = [.. Resets, Maturity];
        double value = 0.0, theta = 0.0;
        for (int i = 1; i < dates.Length; ++i)
        {
            var period = new ForwardVanillaOption(
                1.05, dates[i - 1], new PlainVanillaPayoff(type, 0.0), new EuropeanExercise(dates[i]));
            OneAssetOptionResults r = engine(period, Context, p, AnalyticEuropeanEngine.Calculate);
            value += r.NPV!.Value;
            theta += r.Theta!.Value;
        }

        return (value, theta);
    }
}
