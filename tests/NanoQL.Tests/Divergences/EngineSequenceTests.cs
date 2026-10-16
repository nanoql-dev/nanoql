// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A static engine reproduces QuantLib's <c>Instrument::calculate</c> in order: if the instrument has expired, its
/// expired results; otherwise the arguments' validation, the engine body, and the instrument's fetch of the results.
/// The engine checks its own arguments first, so an expired swap priced without a curve throws, where QuantLib returns
/// the expired results without reading the engine.
/// </summary>
public class EngineSequenceTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void ALiveSwapRunsValidateThenTheBodyThenFetchResults()
    {
        var log = new List<string>();
        var swap = new RecordingSwap(LiveLeg(0.03), LiveLeg(0.025), log);

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Equal(["IsExpired", "Validate", "FetchResults"], log);

        // FetchResults saw the body's results: priced, with a valuation date and per-leg values.
        SwapResults seen = Assert.IsType<SwapResults>(swap.Seen);
        Assert.Equal(Today, seen.ValuationDate);
        Assert.Equal(2, seen.LegNPV.Length);
        Assert.NotEqual(0.0, seen.NPV);
        Assert.Equal(seen.LegNPV[0] + seen.LegNPV[1], seen.NPV);
        Assert.Equal(seen, results);
    }

    [Fact]
    public void AnExpiredSwapReturnsItsExpiredResultsWithoutTheBody()
    {
        var log = new List<string>();
        var swap = new RecordingSwap(ExpiredLeg(0.03), ExpiredLeg(0.025), log);

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Equal(["IsExpired", "SetupExpired"], log);
        Assert.Equal(swap.SetupExpired(), results);
    }

    [Fact]
    public void ValidateRunsBeforeTheBody()
    {
        var log = new List<string>();
        var swap = new RecordingSwap(LiveLeg(0.03), LiveLeg(0.025), log) { FailValidation = true };

        // A settlement date before the curve's reference date fails the body; Validate fails first.
        var e = Assert.Throws<NanoQLException>(() =>
            DiscountingSwapEngine.Calculate(swap, Context, Curve, settlementDate: Today - 1));

        Assert.Equal("invalid swap", e.Message);
        Assert.Equal(["IsExpired", "Validate"], log);
    }

    [Fact]
    public void TheResultsAreWhatFetchResultsReturns()
    {
        var log = new List<string>();
        var swap = new RecordingSwap(LiveLeg(0.03), LiveLeg(0.025), log) { Tag = "derived" };

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Equal("derived", results.AdditionalResults["tag"]);
        Assert.Equal(swap.Seen!.NPV, results.NPV);
    }

    [Fact]
    public void TheArgumentChecksRunBeforeTheExpiryCheck()
    {
        var log = new List<string>();
        var expired = new RecordingSwap(ExpiredLeg(0.03), ExpiredLeg(0.025), log);

        // QuantLib returns the expired results here: Instrument::calculate never reaches the engine.
        Assert.Equal("discountCurve", Assert.Throws<ArgumentNullException>(() =>
            DiscountingSwapEngine.Calculate(expired, Context, null!)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() =>
            DiscountingSwapEngine.Calculate(expired, null!, Curve)).ParamName);
        Assert.Equal("swap", Assert.Throws<ArgumentNullException>(() =>
            DiscountingSwapEngine.Calculate(null!, Context, Curve)).ParamName);
        Assert.Empty(log);

        // A settlement date before the curve is checked in the body, as in QuantLib: an expired swap returns zeros.
        SwapResults results = DiscountingSwapEngine.Calculate(expired, Context, Curve, settlementDate: Today - 1);
        Assert.Equal(0.0, results.NPV);
        Assert.Equal(["IsExpired", "SetupExpired"], log);
    }

    [Fact]
    public void ALegFailureNamesTheLegAndKeepsTheCause()
    {
        // discountingswapengine.cpp wraps a leg's failure as QL_FAIL(io::ordinal(i+1) << " leg: " << e.what()). A
        // coupon whose accrual ends before it starts fails in its amount.
        ImmutableArray<CashFlow> broken =
        [
            new FixedRateCoupon(
                new Date(13, Month.April, 2027),
                1e6,
                0.03,
                new Actual360(),
                new Date(13, Month.April, 2027),
                new Date(13, Month.January, 2027)),
        ];

        var first = Assert.Throws<NanoQLException>(() =>
            DiscountingSwapEngine.Calculate(new Swap(broken, LiveLeg(0.025)), Context, Curve));
        Assert.Equal("1st leg: d1 (April 13th, 2027) later than d2 (January 13th, 2027)", first.Message);
        Assert.IsType<ArgumentException>(first.InnerException);

        var second = Assert.Throws<NanoQLException>(() =>
            DiscountingSwapEngine.Calculate(new Swap(LiveLeg(0.03), broken), Context, Curve));
        Assert.StartsWith("2nd leg: ", second.Message);

        // A failure that isn't an ArgumentException keeps its text whole: QuantLib's own "not supported" for a
        // mixed-compounding rate's compound factor.
        var mixed = new InterestRate(0.03, new Actual360(), Compounding.SimpleThenCompounded, Frequency.Semiannual);
        ImmutableArray<CashFlow> unsupported =
        [
            new FixedRateCoupon(
                new Date(13, Month.April, 2027),
                1e6,
                mixed,
                new Date(13, Month.October, 2026),
                new Date(13, Month.April, 2027)),
        ];
        var third = Assert.Throws<NanoQLException>(() =>
            DiscountingSwapEngine.Calculate(new Swap(unsupported, LiveLeg(0.025)), Context, Curve));
        NanoQLException cause = Assert.IsType<NanoQLException>(third.InnerException);
        Assert.Equal("1st leg: " + cause.Message, third.Message);
        Assert.Contains("not supported", cause.Message);
    }

    private static ImmutableArray<CashFlow> LiveLeg(double rate) => FixedLeg(
        new Date(15, Month.January, 2027), new Date(15, Month.January, 2032), rate);

    private static ImmutableArray<CashFlow> ExpiredLeg(double rate) => FixedLeg(
        new Date(15, Month.January, 2020), new Date(15, Month.January, 2024), rate);

    private static ImmutableArray<CashFlow> FixedLeg(Date start, Date end, double rate) =>
        new FixedRateLeg(new MakeSchedule()
                .From(start)
                .To(end)
                .WithFrequency(Frequency.Semiannual)
                .WithCalendar(new TARGET())
                .ToSchedule())
            .WithNotionals(1e6)
            .WithCouponRates(rate, new Actual360())
            .ToLeg();

    // A swap that records the engine's calls to the steps it owns.
    private sealed class RecordingSwap(ImmutableArray<CashFlow> first, ImmutableArray<CashFlow> second, List<string> log)
        : Swap(first, second)
    {
        public bool FailValidation { get; init; }

        public string? Tag { get; init; }

        public InstrumentResults? Seen { get; private set; }

        public override bool IsExpired(EvaluationContext context)
        {
            log.Add("IsExpired");
            return base.IsExpired(context);
        }

        public override SwapResults SetupExpired()
        {
            log.Add("SetupExpired");
            return base.SetupExpired();
        }

        public override void Validate()
        {
            log.Add("Validate");
            if (FailValidation)
            {
                throw new NanoQLException("invalid swap");
            }

            base.Validate();
        }

        public override SwapResults FetchResults(InstrumentResults results)
        {
            log.Add("FetchResults");
            Seen = results;
            SwapResults fetched = base.FetchResults(results);
            return Tag is null
                ? fetched
                : fetched with { AdditionalResults = fetched.AdditionalResults.Add("tag", Tag) };
        }
    }
}
