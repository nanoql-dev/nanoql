// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The historical analyses' refusals: no index, where QuantLib reads the first index's calendar past an empty vector,
/// a step of no length, on which QuantLib's loop over the dates never ends, and a forward index of no tenor, on which
/// its loop over the fixing periods never ends once the initial gap is within the horizon; and their classes' outputs,
/// the function's.
/// </summary>
public class HistoricalAnalysisTests
{
    private static readonly Date Start = new(1, Month.September, 2026);
    private static readonly Date End = new(30, Month.September, 2026);

    [Fact]
    public void TheRatesAnalysisRefusesNoIndexAndAStepOfNoLength()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        var euribor = new Euribor6M();
        Assert.Equal(
            "no index given (Parameter 'indexes')",
            Assert.Throws<ArgumentException>(
                () => HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis(
                    new SequenceStatistics(), Start, End, new Period(1, TimeUnit.Days), [], context)).Message);
        Assert.Equal(
            "the step must be positive (Parameter 'step')",
            Assert.Throws<ArgumentException>(
                () => HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis(
                    new SequenceStatistics(), Start, End, new Period(0, TimeUnit.Days), [euribor], context)).Message);
        Assert.Equal(
            "the step must be positive (Parameter 'step')",
            Assert.Throws<ArgumentException>(
                () => HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis<Discount, LogLinear>(
                    new SequenceStatistics(),
                    Start,
                    End,
                    new Period(-1, TimeUnit.Weeks),
                    euribor,
                    new Period(1, TimeUnit.Months),
                    new Period(2, TimeUnit.Years),
                    [euribor],
                    [],
                    new Actual365Fixed(),
                    context)).Message);
    }

    [Fact]
    public void TheForwardAnalysisRefusesAnIndexOfNoTenor()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        var euribor = new Euribor6M();
        var zero = new IborIndex(
            "Zero", new Period(0, TimeUnit.Days), 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360());
        HistoricalForwardRatesAnalysisResults Analyse(Period initialGap) =>
            HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis<Discount, LogLinear>(
                new SequenceStatistics(),
                Start,
                End,
                new Period(1, TimeUnit.Days),
                zero,
                initialGap,
                new Period(2, TimeUnit.Years),
                [euribor],
                [],
                new Actual365Fixed(),
                context);

        Assert.Equal(
            "the index's tenor must be positive (Parameter 'fwdIndex')",
            Assert.Throws<ArgumentException>(() => Analyse(new Period(1, TimeUnit.Months))).Message);

        // past the horizon QuantLib's loop adds no period, so nothing is refused
        Assert.Empty(Analyse(new Period(3, TimeUnit.Years)).FixingPeriods);
    }

    [Fact]
    public void AnAnalysisOfNothingHasNoDates()
    {
        var empty = new HistoricalForwardRatesAnalysisImpl<Discount, LogLinear>();
        Assert.Empty(empty.SkippedDates);
        Assert.Empty(empty.SkippedDatesErrorMessage);
        Assert.Empty(empty.FailedDates);
        Assert.Empty(empty.FailedDatesErrorMessage);
        Assert.Empty(empty.FixingPeriods);
    }
}
