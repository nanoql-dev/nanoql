// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Markov-functional model's state: it calibrates when it is built, under the context it is given, where QuantLib's
/// calibrates at its first use under the evaluation date then; <see cref="MarkovFunctional.ForceArbitrageIndices"/>
/// returns a copy calibrated again, its Kahale cores starting and ending at the given indices at the least and the most
/// (QuantLib's forced indices, checked on the wheel's Kahale vectors), and leaves the receiver;
/// <see cref="MarkovFunctional.WithParams"/> keeps the smiles and tabulates the numeraire again, QuantLib's
/// <c>setParams</c>; and <see cref="MarkovFunctional.ModelOutputs"/> computes the outputs per call.
/// </summary>
public class MarkovFunctionalStateTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void TheModelIsCalibratedUnderItsConstructorsContext()
    {
        // SABR's smile is fitted on sections whose reference date is the context's evaluation date.
        MarkovFunctional underToday = Model(MarkovFunctionalModelSettingsAdjustments.SabrSmile, Context);
        var later = new EvaluationContext { EvaluationDate = Today + 30 };
        MarkovFunctional underLater = Model(MarkovFunctionalModelSettingsAdjustments.SabrSmile, later);
        Assert.NotEqual(underToday.Numeraire(2.5, 0.3), underLater.Numeraire(2.5, 0.3));

        // Read under another context, a model keeps its own calibration.
        Assert.Equal(underToday.ModelOutputs(Context).MarketCallPremium.SelectMany(x => x), underToday.ModelOutputs(later).MarketCallPremium.SelectMany(x => x));
    }

    [Fact]
    public void ForcingTheArbitrageIndicesReturnsARecalibratedCopy()
    {
        MarkovFunctional model = Model(MarkovFunctionalModelSettingsAdjustments.KahaleSmile, Context);
        (int Left, int Right)[] dynamic = [.. model.ArbitrageIndices().Select(p => p!.Value)];
        (int Left, int Right)[] forced = [.. dynamic.Select(p => (p.Left + 1, p.Right))];
        MarkovFunctional copy = model.ForceArbitrageIndices(forced, Context);

        (int Left, int Right)[] moved = [.. copy.ArbitrageIndices().Select(p => p!.Value)];
        Assert.Equal(forced.Length, moved.Length);
        Assert.All(forced.Zip(moved), p => Assert.True(p.Second.Left >= p.First.Left && p.Second.Right <= p.First.Right, $"{p}"));
        Assert.NotEqual(dynamic, moved);
        Assert.Equal(dynamic, model.ArbitrageIndices().Select(p => p!.Value));
        Assert.NotEqual(model.Numeraire(2.5, 0.3), copy.Numeraire(2.5, 0.3));

        // An empty set computes the regions again.
        Assert.Equal(dynamic, copy.ForceArbitrageIndices([], Context).ArbitrageIndices().Select(p => p!.Value));
    }

    [Fact]
    public void WithParamsTabulatesTheNumeraireAgainAsQuantLibsSetParams()
    {
        MarkovFunctional model = Model(MarkovFunctionalModelSettingsAdjustments.KahaleSmile | MarkovFunctionalModelSettingsAdjustments.SmileExponentialExtrapolation, Context);
        var moved = (MarkovFunctional)model.WithParams(new Vector([1.0, 0.8, 1.2, 1.1]));

        // QuantLib's before and after setParams, on the wheel, within the vectors' tolerance (the C runtime's erf
        // apart, which the calibration moves to 1.5e-13).
        Assert.True(Math.Abs((model.Numeraire(2.5, 0.3) / 0.679826077566786) - 1.0) <= Tolerance.Relative, $"{model.Numeraire(2.5, 0.3)}");
        Assert.True(Math.Abs((moved.Numeraire(2.5, 0.3) / 0.6794519677636444) - 1.0) <= Tolerance.Relative, $"{moved.Numeraire(2.5, 0.3)}");
        Assert.True(Math.Abs((moved.Zerobond(6.0, 2.5, 0.3) / 0.8989351543521948) - 1.0) <= Tolerance.Relative, $"{moved.Zerobond(6.0, 2.5, 0.3)}");
        Assert.Equal([1.0, 1.0, 1.0, 1.0], model.Volatility);
        Assert.Equal([1.0, 0.8, 1.2, 1.1], moved.Volatility);

        // The outputs per call: a second call gives the first's.
        MarkovFunctionalModelOutputs first = moved.ModelOutputs(Context), second = moved.ModelOutputs(Context);
        Assert.Equal(first.Atm, second.Atm);
        Assert.Equal(first.Messages, second.Messages);
        Assert.Equal(first.ModelCallPremium.SelectMany(x => x), second.ModelCallPremium.SelectMany(x => x));
    }

    // A model on three volatility steps calibrated to five CMS 10y swaptions at 20%, the wheel probe's.
    private static MarkovFunctional Model(MarkovFunctionalModelSettingsAdjustments adjustments, EvaluationContext context)
    {
        var target = new TARGET();
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(Today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        Date[] steps = [.. new[] { 1, 2, 3 }.Select(i => target.Advance(Today, i, TimeUnit.Years))];
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => target.Advance(Today, i, TimeUnit.Years))];
        return new MarkovFunctional(
            Curve, 0.01, steps, [1.0, 1.0, 1.0, 1.0], vol, expiries, [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)],
            new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context,
            new MarkovFunctionalModelSettings(32, 7.0, 16, 1e-5, 1e-7, 0.0, 2.0, adjustments));
    }
}
