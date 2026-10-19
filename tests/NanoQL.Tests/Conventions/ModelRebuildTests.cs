// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A model is immutable, and relinking its curve means building a new model (playbook §2's handle row): the new model's
/// initial short rate is its curve's, and the old model keeps its own. It replaces QuantLib's
/// <c>testHullWhiteUpdatesR0WhenTermStructureRelinks</c>, whose model observes a relinkable handle.
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
}
