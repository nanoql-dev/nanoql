// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The forward rate's end date (QuantLib's #2848, taken ahead of the pin; the S-g1d plan's G1R5 (i)): QuantLib 1.44's
/// <c>Gaussian1dModel::forwardRate</c> ends the period by advancing the value date by the index's tenor on its fixing
/// calendar (<c>gaussian1dmodel.cpp:44-46</c>); QuantLib's master after the pin ends it at the index's
/// <c>maturityDate</c>, which an index of its own overrides. For EURLibor 6M fixed on 20 February 2025, the value date
/// is 24 February and the plain advance 26 August, where its maturity date is 25 August: on the wheel, a GSR model on a
/// flat 3% curve (a = σ = 0.01) gives 0.029812686783318977 at y = 0.4. nanoql's check: the model's own zero bonds over
/// the period to the maturity date.
/// </summary>
public class Gaussian1dForwardRateEndDateTests
{
    [Fact]
    public void ThePeriodEndsAtTheIndexsMaturityDate()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new EURLibor6M(curve);
        Gsr model = Gaussian1dInputs.Constant(curve, 0.01, 0.01);
        var fixing = new Date(20, Month.February, 2025);
        Date valueDate = index.ValueDate(fixing);
        Date end = index.MaturityDate(valueDate);
        Assert.Equal(new Date(25, Month.August, 2025), end);

        double rate = model.ForwardRate(fixing, context, null, 0.4, index);
        double expected = (model.Zerobond(valueDate, null, 0.4, curve) - model.Zerobond(end, null, 0.4, curve))
            / (index.DayCounter.YearFraction(valueDate, end) * model.Zerobond(end, null, 0.4, curve));
        Assert.Equal(expected, rate);

        // QuantLib 1.44's, over the plain advance to 26 August.
        Assert.True(Math.Abs(rate - 0.029812686783318977) > 1e-6, $"{rate}");
    }
}
