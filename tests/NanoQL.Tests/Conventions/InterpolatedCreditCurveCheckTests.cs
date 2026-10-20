// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The interpolated credit curves' checks of their data, with QuantLib's texts, each an <see cref="ArgumentException"/>
/// naming the data: a count other than the dates', "dates/data count mismatch"; a negative hazard rate or default
/// density; and for the survival-probability curve a first probability other than 1, a probability not above 0, and a
/// probability above the one before it, "negative hazard rate implied by the survival probability 0.98 at … (t=…) after
/// the survival probability 0.97 at … (t=…)". An empty list of dates, whose first QuantLib reads with <c>at()</c>,
/// which throws <c>std::out_of_range</c>, is refused at that point with QuantLib's own "not enough input dates given",
/// naming <c>dates</c> (the playbook's convention for such an exception). Each curve's nodes pair its dates with its
/// data, as QuantLib's <c>nodes()</c> do.
/// </summary>
public class InterpolatedCreditCurveCheckTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly Date[] Dates = [Today, Today + 365, Today + 730];

    [Fact]
    public void CountsAndSignsAreChecked()
    {
        DayCounter dc = new Actual365Fixed();
        AssertRefused("hazardRates", "dates/data count mismatch", () => new InterpolatedHazardRateCurve<BackwardFlat>(Dates, [0.01, 0.02], dc));
        AssertRefused("hazardRates", "negative hazard rate", () => new InterpolatedHazardRateCurve<BackwardFlat>(Dates, [0.01, -0.02, 0.02], dc));
        AssertRefused("densities", "dates/data count mismatch", () => new InterpolatedDefaultDensityCurve<Linear>(Dates, [0.01], dc));
        AssertRefused("densities", "negative default density", () => new InterpolatedDefaultDensityCurve<Linear>(Dates, [0.01, 0.01, -0.01], dc));
        AssertRefused("probabilities", "dates/data count mismatch", () => new InterpolatedSurvivalProbabilityCurve<LogLinear>(Dates, [1.0, 0.99], dc));
    }

    [Fact]
    public void SurvivalProbabilitiesStartAtOneAndFall()
    {
        DayCounter dc = new Actual365Fixed();
        AssertRefused(
            "probabilities",
            "the first probability must be == 1.0 to flag the corresponding date as reference date",
            () => new InterpolatedSurvivalProbabilityCurve<LogLinear>(Dates, [0.99, 0.98, 0.97], dc));
        AssertRefused(
            "probabilities",
            "negative probability",
            () => new InterpolatedSurvivalProbabilityCurve<LogLinear>(Dates, [1.0, 0.0, 0.0], dc));
        AssertRefused(
            "probabilities",
            $"negative hazard rate implied by the survival probability 0.98 at {Dates[2]} (t=2) after the survival "
            + $"probability 0.97 at {Dates[1]} (t=1)",
            () => new InterpolatedSurvivalProbabilityCurve<LogLinear>(Dates, [1.0, 0.97, 0.98], dc));
    }

    [Fact]
    public void NoDatesAreRefusedBeforeTheFirstIsRead()
    {
        // QuantLib's constructors pass dates.at(0) to their base first (interpolatedhazardratecurve.hpp:213,
        // 227, 240, and the density and survival curves alike): std::out_of_range, the wheel's "invalid vector
        // subscript".
        Action[] builds =
        [
            () => _ = new InterpolatedHazardRateCurve<BackwardFlat>([], [], new Actual365Fixed()),
            () => _ = new InterpolatedDefaultDensityCurve<Linear>([], [], new Actual365Fixed()),
            () => _ = new InterpolatedSurvivalProbabilityCurve<LogLinear>([], [], new Actual365Fixed()),
        ];
        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("dates", e.ParamName);
            Assert.StartsWith("not enough input dates given", e.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NodesPairTheDatesWithTheData()
    {
        // QuantLib's nodes(), on the wheel, give each curve's dates and data back
        DayCounter dc = new Actual365Fixed();
        AssertNodes([0.01, 0.015, 0.02], new InterpolatedHazardRateCurve<BackwardFlat>(Dates, [0.01, 0.015, 0.02], dc).Nodes());
        AssertNodes([0.01, 0.012, 0.011], new InterpolatedDefaultDensityCurve<Linear>(Dates, [0.01, 0.012, 0.011], dc).Nodes());
        AssertNodes([1.0, 0.98, 0.9], new InterpolatedSurvivalProbabilityCurve<LogLinear>(Dates, [1.0, 0.98, 0.9], dc).Nodes());
    }

    private static void AssertNodes(double[] data, ImmutableArray<KeyValuePair<Date, double>> nodes) =>
        Assert.Equal(Dates.Zip(data, (d, x) => new KeyValuePair<Date, double>(d, x)), nodes);

    private static void AssertRefused(string paramName, string text, Action build)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
    }
}
