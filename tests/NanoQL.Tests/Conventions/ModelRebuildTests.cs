// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A model is immutable, and relinking its curve means building a new model (playbook §2's handle row): the new model's
/// initial short rate is its curve's, and the old model keeps its own. It replaces QuantLib's
/// <c>testHullWhiteUpdatesR0WhenTermStructureRelinks</c>, whose model observes a relinkable handle, and
/// <c>testExtendedCoxIngersollRossUpdatesWhenTermStructureRelinks</c>, whose model notifies its observers of a relink,
/// and <c>testG2ProcessObservesTermStructure</c>, whose process reads a quote set in place: the extended
/// Cox-Ingersoll-Ross model and the G2 process rebuilt on another curve each fit that curve, the first kept on its own.
/// </summary>
public class ModelRebuildTests
{
    [Fact]
    public void AHullWhiteBuiltOnAnotherCurveTakesThatCurvesShortRate()
    {
        var today = new Date(19, Month.May, 2026);
        var first = new FlatForward(today, 0.02, new Actual365Fixed());
        var second = new FlatForward(today, 0.05, new Actual365Fixed());

        var model = new HullWhite(first);
        var rebuilt = new HullWhite(second);

        double expected = second.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate;
        Assert.True(Math.Abs(rebuilt.R0 - expected) <= 1.0e-12, $"expected {expected}, calculated {rebuilt.R0}");

        double kept = first.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate;
        Assert.True(Math.Abs(model.R0 - kept) <= 1.0e-12, $"expected {kept}, calculated {model.R0}");
    }

    [Fact]
    public void AnExtendedCoxIngersollRossBuiltOnAnotherCurveFitsThatCurve()
    {
        var today = new Date(19, Month.May, 2026);
        var first = new FlatForward(today, 0.02, new Actual365Fixed());
        var second = new FlatForward(today, 0.05, new Actual365Fixed());

        var model = new ExtendedCoxIngersollRoss(first, 0.02, 1.0, 1e-4, 0.02);
        var rebuilt = new ExtendedCoxIngersollRoss(second, 0.02, 1.0, 1e-4, 0.02);

        foreach (double t in (ReadOnlySpan<double>)[0.5, 2.0, 10.0])
        {
            Assert.Equal(second.Discount(t), rebuilt.Discount(t), 1.0e-12);
            Assert.Equal(first.Discount(t), model.Discount(t), 1.0e-12);
        }
    }

    [Fact]
    public void AG2ProcessBuiltOnAnotherCurveTakesThatCurvesForward()
    {
        const double a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.5;
        var today = new Date(19, Month.May, 2026);
        var first = new FlatForward(today, 0.02, new Actual365Fixed());
        var second = new FlatForward(today, 0.05, new Actual365Fixed());

        var process = new G2Process(a, sigma, b, eta, rho, first);
        double phiBefore = process.Phi(2.0);
        var rebuilt = new G2Process(a, sigma, b, eta, rho, second);

        // QuantLib's check: under a flat curve, 300bp more forward raises phi by as much
        Assert.True(Math.Abs((rebuilt.Phi(2.0) - phiBefore) - 0.03) <= 1e-10, $"delta {rebuilt.Phi(2.0) - phiBefore}");
        Assert.Equal(phiBefore, process.Phi(2.0));
    }
}
