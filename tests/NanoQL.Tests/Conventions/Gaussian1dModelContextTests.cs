// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Gaussian1d models' evaluation date: QuantLib's model copies the evaluation date and the historic-fixings flag
/// from <c>Settings</c> at its calculation (<c>gaussian1dmodel.hpp:200-203</c>) and reads them for every fixing
/// (<c>gaussian1dmodel.cpp:38</c>, <c>:92</c>); here <c>ForwardRate</c> and <c>SwapRate</c> take the context, so a
/// fixing before its date is read from its fixings, on its date when it enforces today's historic fixings; nothing is
/// kept from one call's context to the next.
/// </summary>
public class Gaussian1dModelContextTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void AFixingBeforeTheContextsDateIsReadFromItsFixings()
    {
        var index = new Euribor6M(Curve);
        var swapIndex = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var past = new Date(10, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = Today };
        context = context with { Fixings = context.Fixings.AddFixing(index, past, 0.0123).AddFixing(swapIndex, past, 0.0234) };
        Assert.Equal(0.0123, model.ForwardRate(past, context, null, 0.0, index));
        Assert.Equal(0.0234, model.SwapRate(past, new Period(5, TimeUnit.Years), context, null, 0.0, swapIndex));

        // A context dated after the model's curve: its own date decides, not the curve's.
        var later = new Date(17, Month.January, 2025);
        var afterCurve = new EvaluationContext { EvaluationDate = new Date(20, Month.January, 2025) };
        afterCurve = afterCurve with { Fixings = afterCurve.Fixings.AddFixing(index, later, 0.0345) };
        Assert.Equal(0.0345, model.ForwardRate(later, afterCurve, null, 0.0, index));
    }

    [Fact]
    public void TodaysFixingIsReadFromItsFixingsOnlyWhenTheContextEnforcesThem()
    {
        var index = new Euribor6M(Curve);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var context = new EvaluationContext { EvaluationDate = Today };
        context = context with { Fixings = context.Fixings.AddFixing(index, Today, 0.0456) };
        Assert.Equal(0.0456, model.ForwardRate(Today, context with { EnforcesTodaysHistoricFixings = true }, null, 0.0, index));
        double forecast = model.ForwardRate(Today, context, null, 0.0, index);
        Assert.NotEqual(0.0456, forecast);
        Assert.Equal(forecast, model.ForwardRate(Today, new EvaluationContext { EvaluationDate = Today }, null, 0.0, index));
    }
}
