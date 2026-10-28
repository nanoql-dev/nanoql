// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit portfolio's own rules (the S-xcr plan's XR1, XR2, XR8): the basket's binding of its loss model, an
/// engine's evaluation date, the loss model contract's refusals, the realized defaults' refusals, the n-th-to-default's
/// order, the event counts' widths and the default correlation's undefined points, each refusal's type, message and
/// parameter.
/// </summary>
public class CreditPortfolioConventionTests
{
    [Fact]
    public void ABasketWithNoModelRefusesItsStatistics()
    {
        Basket basket = Three(0.02);

        var e = Assert.Throws<NanoQLException>(() => basket.ProbAtLeastNEvents(1, D5));
        Assert.Equal("Basket has no default loss model assigned.", e.Message);
        Assert.Null(basket.SetLossModel(null).LossModel);
        Assert.Throws<NanoQLException>(() => basket.RemainingTrancheNotional());
    }

    [Fact]
    public void AnEngineRefusesABasketOfAnotherEvaluationDate()
    {
        Basket basket = Three(0.02).SetLossModel(Constant(3, 0.3));
        var ntd = new NthToDefault(basket, 1, ProtectionSide.Seller, FiveYears(), 0.0, 0.02, new Actual360(), 30.0, true);
        var later = new EvaluationContext { EvaluationDate = Today + 1 };

        var e = Assert.Throws<NanoQLException>(() => IntegralNtdEngine.Calculate(ntd, later, new Period(1, TimeUnit.Weeks), Discount()));
        Assert.Contains("the basket's evaluation date", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContractsStatisticsRefuseWithQuantLibsTexts()
    {
        IDefaultLossModel m = Constant(3, 0.3).SetBasket(Three(0.02));
        (Action Call, string Text)[] refusals =
        [
            (() => m.ExpectedTrancheLoss(D5), "expectedTrancheLoss Not implemented for this model."),
            (() => m.ProbOverLoss(D5, 0.5), "probOverLoss Not implemented for this model."),
            (() => m.Percentile(D5, 0.9), "percentile Not implemented for this model."),
            (() => m.ExpectedShortfall(D5, 0.9), "eSF Not implemented for this model."),
            (() => m.SplitVaRLevel(D5, 1.0), "splitVaRLevel Not implemented for this model."),
            (() => m.SplitESFLevel(D5, 1.0), "splitESFLevel Not implemented for this model."),
            (() => m.LossDistribution(D5), "lossDistribution Not implemented for this model."),
            (() => m.DensityTrancheLoss(D5, 0.5), "densityTrancheLoss Not implemented for this model."),
            (() => m.ProbsBeingNthEvent(1, D5), "probsBeingNthEvent Not implemented for this model."),
        ];
        foreach ((Action call, string text) in refusals)
        {
            Assert.Equal(text, Assert.Throws<NanoQLException>(call).Message);
        }
    }

    [Fact]
    public void TheBaseClassAndTheInterfaceRefuseEveryStatistic()
    {
        // the abstract base's virtual defaults and the interface's default members, on models overriding none
        var unbound = new BaseModel();
        Assert.Null(unbound.Basket);
        var bound = (BaseModel)unbound.SetBasket(Three(0.02));
        Assert.Equal(3, bound.Basket!.Size);
        IDefaultLossModel bare = new BareModel();
        Assert.Same(bare, bare.SetBasket(Three(0.02)));
        foreach (IDefaultLossModel m in new IDefaultLossModel[] { bound, bare })
        {
            (Action Call, string Text)[] refusals =
            [
                (() => m.ExpectedTrancheLoss(D5), "expectedTrancheLoss Not implemented for this model."),
                (() => m.ProbOverLoss(D5, 0.5), "probOverLoss Not implemented for this model."),
                (() => m.Percentile(D5, 0.9), "percentile Not implemented for this model."),
                (() => m.ExpectedShortfall(D5, 0.9), "eSF Not implemented for this model."),
                (() => m.SplitVaRLevel(D5, 1.0), "splitVaRLevel Not implemented for this model."),
                (() => m.SplitESFLevel(D5, 1.0), "splitESFLevel Not implemented for this model."),
                (() => m.LossDistribution(D5), "lossDistribution Not implemented for this model."),
                (() => m.DensityTrancheLoss(D5, 0.5), "densityTrancheLoss Not implemented for this model."),
                (() => m.ProbsBeingNthEvent(1, D5), "probsBeingNthEvent Not implemented for this model."),
                (() => m.DefaultCorrelation(D5, 0, 1), "defaultCorrelation Not implemented for this model."),
                (() => m.ProbAtLeastNEvents(1, D5), "probAtLeastNEvents Not implemented for this model."),
                (() => m.ExpectedRecovery(D5, 0, Key()), "expected recovery Not implemented for this model."),
            ];
            foreach ((Action call, string text) in refusals)
            {
                Assert.Equal(text, Assert.Throws<NanoQLException>(call).Message);
            }
        }
    }

    [Fact]
    public void TheTypesKeysAndEventsCompareByValue()
    {
        var bankruptcy = new DefaultType(AtomicDefaultType.Bankruptcy);
        object same = new DefaultType(AtomicDefaultType.Bankruptcy);
        var other = new DefaultType(AtomicDefaultType.Restructuring, RestructuringType.ModifiedRestructuring);
        Assert.True(bankruptcy.Equals(same) && !bankruptcy.Equals(other) && bankruptcy != other);
        Assert.Equal(bankruptcy.GetHashCode(), same.GetHashCode());

        object key = Key();
        Assert.True(Key().Equals(key) && Key().Equals(Key(Seniority.SecDom)));
        Assert.True(!Key().Equals(Key(Seniority.SubLT2)) && Key() != Key(Seniority.SubLT2));
        Assert.Equal(Key().GetHashCode(), key.GetHashCode());

        object june = June2006Bankruptcy();
        var later = new BankruptcyEvent(new Date(2, June, 2006), new EURCurrency(), Seniority.SeniorSec, null, 0.4);
        Assert.True(June2006Bankruptcy().Equals(june) && June2006Bankruptcy() != later);
        Assert.Equal(June2006Bankruptcy().GetHashCode(), june.GetHashCode());
    }

    [Fact]
    public void TheIssuersTwoConstructorsAndTheBasketsKeysAgree()
    {
        FlatHazardRate curve = Flat(Today, 0.02);
        var byLists = new Issuer([Key().EventTypes], [new EURCurrency()], [Seniority.SeniorSec], [curve]);
        Assert.Same(curve, byLists.DefaultProbability(Key()));
        Assert.Equal(
            "Incompatible size of Issuer parameters.",
            Assert.Throws<ArgumentException>(() => new Issuer([Key().EventTypes], [new EURCurrency()], [], [curve])).Message
                .Split(" (Parameter")[0]);

        Basket basket = Three(0.02);
        Assert.Equal(basket.RemainingDefaultKeys(Today), basket.RemainingDefaultKeys());
    }

    [Fact]
    public void ThePoolsDefaultKeyHasNoGracePeriod()
    {
        // QuantLib's Pool::add key is NorthAmericaCorpDefaultKey(Currency(), SeniorSec, Period(), 1.): a failure to pay
        // ten days before the evaluation date, settled at 0.4, is a default on it, remaining size 1 and settled loss 6
        // (the S-xcr C1 review's rv_c1_pool.cpp); on a 30-day key QuantLib's reads 2 and 0
        var key = new NorthAmericaCorpDefaultKey(null, Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0);
        var failure = new FailureToPayEvent(Today - 10, null, Seniority.SeniorSec, 1.0e6, Today - 5, 0.4);
        Pool pool = new Pool()
            .Add("A", new Issuer([(key, Flat(Today, 0.02))], [failure]))
            .Add("B", new Issuer([(key, Flat(Today, 0.02))]));

        var basket = new Basket(Today - 30, ["A", "B"], [10.0, 10.0], pool, Context);

        Assert.Equal(1, basket.RemainingSize());
        Assert.Equal(6.0, basket.SettledLoss(), 1e-14);
    }

    [Fact]
    public void AnEatenUpTrancheIsCertainToLose()
    {
        // a basket of zero notionals binds its model; QuantLib answers 1 before asking it (the S-xcr C1 review's
        // rv_c1_zero.cpp: remaining notional 0, probOverLoss(5y, 0.5) 1)
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        Basket basket = new Basket(Today, ["A", "B", "C"], [0.0, 0.0, 0.0], PoolOf(["A", "B", "C"], curves), Context)
            .SetLossModel(Constant(3, 0.3));

        Assert.Equal(0.0, basket.RemainingNotional());
        Assert.Equal(1.0, basket.ProbOverLoss(D5, 0.5));
    }

    [Fact]
    public void AnNthToDefaultOnAWhollyDefaultedBasketIsRefused()
    {
        // no live name binds no model: the engine refuses at its first statistic, with the basket's text
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02)];
        var basket = new Basket(new Date(1, January, 2006), ["A"], [10.0], PoolOf(["A"], curves, [[June2006Bankruptcy()]]), Context);

        Assert.Equal("Basket has no default loss model assigned.", Assert.Throws<NanoQLException>(() => Ntd(basket, 1, 10.0)).Message);
    }

    [Fact]
    public void AMissingDateIsRefused()
    {
        // default(Date) isn't a date before every curve: the reads refuse it, as the curves' own do
        Basket basket = Three(0.02);
        Basket bound = basket.SetLossModel(Constant(3, 0.3));
        var model = new GaussianDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature).ResetBasket(basket);

        Assert.Equal("d", Assert.Throws<ArgumentException>(() => basket.Probabilities(default)).ParamName);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => bound.ProbAtLeastNEvents(1, default)).ParamName);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => bound.DefaultCorrelation(default, 0, 1)).ParamName);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => model.ProbOfDefault(0, default)).ParamName);
        Assert.Equal("date", Assert.Throws<ArgumentException>(() => model.ProbAtLeastNEvents(1, default)).ParamName);
    }

    [Fact]
    public void ADefaultNotSettledForItsSeniorityIsRefused()
    {
        // an event settles its own seniority whatever its form, so a name's default lacks its key's seniority only
        // unsettled, or on a key of NoSeniority, whose read QuantLib's settlement refuses
        var unsettled = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, null, 0.4);
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02)];
        var x = Assert.Throws<NanoQLException>(() => new Basket(
            new Date(1, January, 2006), ["A", "B"], [10.0, 20.0], PoolOf(["A", "B"], curves, [null, [unsettled]]), Context));
        Assert.Contains("has defaulted with no settlement for its seniority", x.Message, StringComparison.Ordinal);

        NorthAmericaCorpDefaultKey any = Key(Seniority.NoSeniority);
        Pool pool = new Pool().Add("A", new Issuer([(any, curves[0])]), any)
            .Add("B", new Issuer([(any, curves[1])], [June2006Bankruptcy()]), any);
        var y = Assert.Throws<ArgumentException>(() => new Basket(new Date(1, January, 2006), ["A", "B"], [10.0, 20.0], pool, Context));
        Assert.Equal("seniority", y.ParamName);
    }

    [Fact]
    public void ABasketWhoseNamesHaveAllDefaultedBindsNoModel()
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02)];
        var basket = new Basket(new Date(1, January, 2006), ["A"], [10.0], PoolOf(["A"], curves, [[June2006Bankruptcy()]]), Context);

        Assert.Null(basket.PricingBasket);
        Assert.Equal(6.0, basket.SettledLoss(), 1e-14);
        Assert.Throws<NanoQLException>(() => basket.SetLossModel(Constant(1, 0.3)));
    }

    [Fact]
    public void AnNthToDefaultOfOrderZeroIsRefused()
    {
        Basket basket = Three(0.02).SetLossModel(Constant(3, 0.3));

        var e = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NthToDefault(basket, 0, ProtectionSide.Seller, FiveYears(), 0.0, 0.02, new Actual360(), 30.0, true));
        Assert.Equal("n", e.ParamName);
        Assert.Throws<ArgumentException>(() =>
            new NthToDefault(basket, 4, ProtectionSide.Seller, FiveYears(), 0.0, 0.02, new Actual360(), 30.0, true));
    }

    [Fact]
    public void TheEventCountsRefuseWhatTheirMaskCantCount()
    {
        // QuantLib counts the configurations in a 32-bit BigNatural: 2^32 is 0, and 1 << 31 passes the sign
        var curves = Enumerable.Range(0, 32).Select(_ => (DefaultProbabilityTermStructure)Flat(Today, 0.02)).ToArray();
        string[] names = [.. Enumerable.Range(0, 32).Select(i => $"N{i:D2}")];
        Basket big = new Basket(Today, names, Enumerable.Repeat(1.0, 32), PoolOf(names, curves), Context).SetLossModel(Constant(32, 0.3));
        Basket small = Three(0.02).SetLossModel(Constant(3, 0.3));

        Assert.Contains("32 names or more", Assert.Throws<NanoQLException>(() => big.ProbAtLeastNEvents(1, D5)).Message, StringComparison.Ordinal);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => small.ProbAtLeastNEvents(31, D5)).ParamName);
        Assert.Equal(0.0, small.ProbAtLeastNEvents(30, D5));
    }

    [Fact]
    public void TheDefaultCorrelationIsUndefinedAtProbabilitiesOfZero()
    {
        // QuantLib divides 0 by 0 at the evaluation date, where every default probability is 0
        Basket basket = Three(0.02).SetLossModel(Constant(3, 0.3));

        var e = Assert.Throws<NanoQLException>(() => basket.DefaultCorrelation(Today, 0, 1));
        Assert.Contains("default probability of 0 or 1", e.Message, StringComparison.Ordinal);
        Assert.Equal(1.0, basket.DefaultCorrelation(D5, 1, 1), 1e-14);
    }

    [Fact]
    public void ALatentModelRefusesABasketOfAnotherSize()
    {
        var model = new GaussianDefProbLM(0.3, 2, LatentModelIntegrationType.GaussianQuadrature);

        Assert.Equal("Incompatible new basket and model sizes.", Assert.Throws<NanoQLException>(() => model.ResetBasket(Three(0.02))).Message);
        Assert.Equal("No portfolio basket set.", Assert.Throws<NanoQLException>(() => model.ProbOfDefault(0, D5)).Message);
    }

    [Fact]
    public void TheBasketCopiesItsInputs()
    {
        double[] notionals = [10.0, 20.0, 30.0];
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        var basket = new Basket(Today, ["A", "B", "C"], notionals, PoolOf(["A", "B", "C"], curves), Context);

        notionals[0] = 1000.0;

        Assert.Equal(10.0, basket.Notionals[0]);
        Assert.Equal(60.0, basket.BasketNotional);
    }

    [Fact]
    public void ALossOrdersAndComparesByTime()
    {
        var a = new Loss(1.0, 5.0);
        var b = new Loss(1.0, 7.0);
        var c = new Loss(2.0, 1.0);

        Assert.True(a == b);
        Assert.True(a < c && c > a && a <= b && b >= a);
        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TheDefaultTypesRefuseAnIncoherentPair()
    {
        Assert.Equal(
            "restructuringType",
            Assert.Throws<ArgumentException>(() => new DefaultType(AtomicDefaultType.Restructuring, RestructuringType.XR)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultType((AtomicDefaultType)99));
        Assert.Throws<ArgumentException>(() => new DefaultProbKey([new DefaultType(), new DefaultType()], null, Seniority.SecDom));
        Assert.Throws<ArgumentException>(() => new RecoveryRateQuote(1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecoveryRateQuote.MakeIsdaMap(new double[7]));
    }

    // DefaultLossModel and IDefaultLossModel with every default kept.
    private sealed class BaseModel : DefaultLossModel
    {
        protected override void ResetModel()
        {
        }
    }

    private sealed class BareModel : IDefaultLossModel
    {
        public Basket? Basket => null;

        public IDefaultLossModel SetBasket(Basket basket) => this;
    }

    private static Basket Three(double h)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, h), Flat(Today, h), Flat(Today, h)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }
}
