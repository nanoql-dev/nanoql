// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference barrier and rebate engines on structures that measure time differently. QuantLib takes the
/// maturity from the process's time, the risk-free curve's (<c>fdblackscholesbarrierengine.cpp:89</c>,
/// <c>fdblackscholesrebateengine.cpp:73</c>), and the dividends' and the knock-in's vanilla conditions' reference date
/// and day counter from the risk-free curve (<c>:118-120</c>, <c>:106-107</c>), while the operator and the mesher read
/// the other structures at those times on their own clocks: on a risk-free curve on Actual/360, settling two days
/// later, or beside a dividend curve and volatility dated later, its barrier engine is off its own engine on the market
/// restated from the earliest date by 1.6e-3 to 9.6e-3 relative, the knock-ins most, and its rebate engine by 1.1e-4 to
/// 3.6e-4 (QuantLib-Python 1.44). nanoql's engines take their times from the engine's clock (<see
/// cref="FdmBlackScholesOp.Clock"/>), the earliest reference date of the structures on the risk-free curve's day
/// counter, the operator reading each structure at the instants those times denote, nothing accrued before its own date
/// (the playbook's row for structures on another clock), so a knock-in's three parts price on one clock. QuantLib's
/// check: its own engines on the market restated from the earliest date on the clock's day counter (a curve dated later
/// a discount curve at 1 up to its date, a volatility dated later a variance curve at 0 up to its date), to 1e-12, as
/// the finite-difference vanilla engine meets it. Where every structure shares the risk-free curve's date and day
/// counter, the clock is the risk-free curve, and the engines keep QuantLib's bits.
/// </summary>
public class FdBarrierMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly Date Settlement = new(19, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    // a one-year call at 100 with a rebate of 2.5: (type, barrier)
    private static readonly (BarrierType Type, double Barrier)[] Options =
    [
        (BarrierType.DownOut, 85.0),
        (BarrierType.DownIn, 85.0),
        (BarrierType.UpOut, 120.0),
        (BarrierType.UpIn, 120.0),
    ];

    [Fact]
    public void OnAnAct360RiskFreeCurveTheBarrierEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the discount factors of 5% on Actual/365 Fixed, on an Actual/360 curve: QuantLib on the market restated on
        // Actual/360, the clock's day counter, the same discount factors and variances at every date; restated on
        // Actual/365 Fixed its grid runs in another time unit and lands up to 1.4e-10 apart (11.190433449168754, ...).
        // On the market as given QuantLib gives 11.219567380861552, 2.3706455975120555, 1.8115362267035466 and
        // 11.772691779474696
        GeneralizedBlackScholesProcess process = Process(
            new FlatForward(Today, 0.05 * 360.0 / 365.0, new Actual360()),
            new FlatForward(Today, 0.02, A365),
            Vol(Today));
        Check(process, [], [11.19043344902839, 2.349955273493938, 1.817884696847138, 11.716661364871836]);
    }

    [Fact]
    public void OnACurveSettlingLaterWithACashDividendTheBarrierEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the risk-free curve settling T+2, a cash dividend of 2.0 on 15 April 2027; on the market as given QuantLib
        // gives 10.314034690256637, 2.1355901696404764, 1.7376827696946138 and 10.703574926581366
        GeneralizedBlackScholesProcess process = Process(
            new FlatForward(Settlement, 0.05, A365), new FlatForward(Today, 0.02, A365), Vol(Today));
        Check(
            process,
            [new FixedDividend(2.0, new Date(15, April, 2027))],
            [10.3375765814793, 2.1543018145022472, 1.7335092469882205, 10.749667496583559]);
    }

    [Fact]
    public void OnStructuresDatedLaterTheBarrierEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the dividend curve and the volatility dated T+2; on the market as given QuantLib gives its values on the
        // market dated today, 11.190433449168754, 2.349955273421809, 1.8178846967922442 and 11.71666136499491, reading
        // the later structures as though dated today
        GeneralizedBlackScholesProcess process = Process(
            new FlatForward(Today, 0.05, A365), new FlatForward(Settlement, 0.02, A365), Vol(Settlement));
        Check(process, [], [11.172604662330084, 2.327623694569409, 1.8227210662869573, 11.67204435055643]);
    }

    [Fact]
    public void OnACurveSettlingLaterTheRebateEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the knock-outs' rebates; on the market as given QuantLib gives 2.416796105826177 and 2.411021839572354
        GeneralizedBlackScholesProcess process = Process(
            new FlatForward(Settlement, 0.05, A365), new FlatForward(Today, 0.02, A365), Vol(Today));
        (BarrierType Type, double Barrier, double QuantLib)[] cases =
        [
            (BarrierType.DownOut, 85.0, 2.417655914483584),
            (BarrierType.UpOut, 120.0, 2.4114984679666676),
        ];
        foreach (var c in cases)
        {
            double value = FdBlackScholesRebateEngine.Calculate(
                Option(c.Type, c.Barrier), Context, process, tGrid: 200, xGrid: 400).NPV!.Value;
            Assert.Equal(c.QuantLib, value, 1e-12);
        }
    }

    // QuantLib's barrier engine (200 x 400) on the restated market, in the order of Options
    private static void Check(
        GeneralizedBlackScholesProcess process, ImmutableArray<Dividend> dividends, double[] quantLib)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            double value = FdBlackScholesBarrierEngine.Calculate(
                Option(Options[i].Type, Options[i].Barrier), Context, process, dividends, 200, 400).NPV!.Value;
            Assert.Equal(quantLib[i], value, 1e-12);
        }
    }

    private static BarrierOption Option(BarrierType type, double barrier) => new(
        type, barrier, 2.5, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    // spot 100, volatility 25% on Actual/365 Fixed
    private static BlackScholesMertonProcess Process(
        YieldTermStructure riskFree, YieldTermStructure dividendYield, BlackVolTermStructure volatility) =>
        new(100.0, dividendYield, riskFree, volatility);

    private static BlackConstantVol Vol(Date referenceDate) => new(referenceDate, new NullCalendar(), 0.25, A365);
}
