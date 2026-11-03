// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The American approximations on an exercise window opening after the reference date. QuantLib's Barone-Adesi-Whaley,
/// Bjerksund-Stensland, Ju, QD+ and QD fixed-point engines read only the last date
/// (<c>baroneadesiwhaleyengine.cpp:161-166</c>, <c>bjerksundstenslandengine.cpp:463-469</c>,
/// <c>juquadraticengine.cpp:61-66</c>, <c>qdplusamericanengine.cpp:178-184</c>), so a put (S 80, K 100, σ 25%, r 5%, a
/// year) exercisable in its last 3 months prices as exercisable now: 20.28 to 20.40, where QuantLib's FD engine gives
/// 18.999. nanoql prices the window by the discount to its opening times the expectation, over S(t₁)'s density, of the
/// engine's own American from the opening (<see cref="ApproximationWindow"/>). QuantLib's check: its FD engine on the
/// window (4,000 × 4,000), each engine within its own error on the option exercisable now (its value against the FD
/// engine's there) (QuantLib-Python 1.44). A window opening on the reference date is QuantLib's, bit for bit.
/// </summary>
public class ApproximationWindowTests
{
    private const double FdValue = 18.998902405989497;
    private const double FdDelta = -0.7394597338304947;
    private const double FdGamma = 0.016940903067026024;

    // QuantLib's FD engine (2,000 × 2,000) on the option exercisable now
    private const double FdNowValue = 20.363252437468653;
    private const double FdNowDelta = -0.8592054762431228;
    private const double FdNowGamma = 0.026618399071106063;

    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    // each engine on the window, and QuantLib's value on the option exercisable now (its value on the window, by its
    // last date alone)
    [Theory]
    [InlineData("Barone-Adesi-Whaley", 20.2776703084083)]
    [InlineData("QD+", 20.403274193057847)]
    [InlineData("QD fixed point", 20.363795119282738)]
    public void AnEnginesWindowValueIsTheFdEnginesToItsOwnError(string engine, double now)
    {
        double value = Engine(engine)(Put(Today + 273), Context, Process()).NPV!.Value;
        Assert.True(Math.Abs(value - FdValue) <= Math.Abs(now - FdNowValue), $"{engine}: {value} against FD's {FdValue}");
    }

    // and QuantLib's delta and gamma on the option exercisable now
    [Theory]
    [InlineData("Bjerksund-Stensland", 20.3080079332352, -0.8695563876834225, 0.027093717899920686)]
    [InlineData("Ju", 20.342240544522518, -0.8610007648405373, 0.026535711987436338)]
    public void AnEnginesWindowValueDeltaAndGammaAreTheFdEnginesToItsOwnError(
        string engine, double now, double nowDelta, double nowGamma)
    {
        OneAssetOptionResults results = Engine(engine)(Put(Today + 273), Context, Process());
        Assert.True(Math.Abs(results.NPV!.Value - FdValue) <= Math.Abs(now - FdNowValue), $"{engine}: {results.NPV}");
        Assert.True(Math.Abs(results.Delta!.Value - FdDelta) <= Math.Abs(nowDelta - FdNowDelta), $"{engine}: delta {results.Delta}");
        Assert.True(Math.Abs(results.Gamma!.Value - FdGamma) <= Math.Abs(nowGamma - FdNowGamma), $"{engine}: gamma {results.Gamma}");
    }

    [Fact]
    public void AWindowOpeningOnTheReferenceDateIsQuantLibs()
    {
        // QuantLib's five values on the option exercisable now
        (string Engine, double Value)[] cases =
        [
            ("Barone-Adesi-Whaley", 20.2776703084083), ("Bjerksund-Stensland", 20.3080079332352), ("Ju", 20.342240544522518),
            ("QD+", 20.403274193057847), ("QD fixed point", 20.363795119282738),
        ];
        foreach ((string engine, double value) in cases)
        {
            Assert.Equal(value, Engine(engine)(Put(Today), Context, Process()).NPV!.Value);
        }
    }

    [Fact]
    public void ANodesRefusalRefusesTheWindow()
    {
        // r -1%, q -2%: Barone-Adesi-Whaley's critical price refuses a risk-free discount above 1, at every node
        var process = new BlackScholesMertonProcess(
            80.0,
            new FlatForward(Today, -0.02, A365),
            new FlatForward(Today, -0.01, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => BaroneAdesiWhaleyApproximationEngine.Calculate(Put(Today + 273), Context, process));
        Assert.StartsWith("the Barone-Adesi-Whaley approximation is not applicable with negative interest rates", e.Message);
    }

    private static Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> Engine(string name) =>
        name switch
        {
            "Barone-Adesi-Whaley" => BaroneAdesiWhaleyApproximationEngine.Calculate,
            "Bjerksund-Stensland" => BjerksundStenslandApproximationEngine.Calculate,
            "Ju" => JuQuadraticApproximationEngine.Calculate,
            "QD+" => (o, c, p) => QdPlusAmericanEngine.Calculate(o, c, p),
            "QD fixed point" => (o, c, p) => QdFpAmericanEngine.Calculate(o, c, p),
            _ => throw new ArgumentException(name, nameof(name)),
        };

    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(
            80.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static VanillaOption Put(Date opening) =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(opening, Today + 365));
}
