// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Markov-functional model's custom smile, an extension point no QuantLib case uses: a custom section that is the
/// model's own Kahale section and inverts its digitals as the model does, within the rate bounds by Brent's solver to the
/// market rate accuracy, gives the Kahale model's numeraire within that accuracy, and its arbitrage indices are
/// <see langword="null"/>, QuantLib's pair of <c>Null&lt;Size&gt;()</c>.
/// </summary>
public class MarkovFunctionalCustomSmileTests
{
    private const double Gap = 1e-5;
    private const double Accuracy = 1e-7;

    [Fact]
    public void ACustomSmileInvertingAsTheModelGivesItsCalibration()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var target = new TARGET();
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => target.Advance(today, i, TimeUnit.Years))];
        Period[] tenors = [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)];
        MarkovFunctional Model(MarkovFunctionalModelSettingsAdjustments adjustments, MarkovFunctionalCustomSmileFactory? factory) => new(
            curve, 0.01, [], [1.0], vol, expiries, tenors, new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context,
            new MarkovFunctionalModelSettings(32, 7.0, 16, Gap, Accuracy, 0.0, 2.0, adjustments, default, factory));

        MarkovFunctional kahale = Model(KahaleSmile | SmileExponentialExtrapolation, null);
        MarkovFunctional custom = Model(CustomSmile, new KahaleFactory());

        Assert.All(custom.ArbitrageIndices(), p => Assert.Null(p));
        foreach (double t in new[] { 1.0, 2.5, 4.0 })
        {
            foreach (double y in new[] { -2.0, 0.0, 0.3, 2.0 })
            {
                Assert.True(Math.Abs((custom.Numeraire(t, y) / kahale.Numeraire(t, y)) - 1.0) <= 1e-6, $"({t}, {y}): {custom.Numeraire(t, y)} against {kahale.Numeraire(t, y)}");
            }
        }
    }

    private sealed class KahaleFactory : MarkovFunctionalCustomSmileFactory
    {
        public override MarkovFunctionalCustomSmileSection SmileSection(SmileSection source, double atm) =>
            new KahaleCustom(new KahaleSmileSection(source, atm, false, true, false, default, Gap));
    }

    // The model's own inversion (MarkovFunctional's marketSwapRate and its bounds' digitals), on a Kahale section.
    private sealed class KahaleCustom(KahaleSmileSection kahale) : MarkovFunctionalCustomSmileSection
    {
        public override Date? ExerciseDate => kahale.ExerciseDate;

        public override double ExerciseTime => kahale.ExerciseTime;

        public override DayCounter? DayCounter => kahale.DayCounter;

        public override double Shift => kahale.Shift;

        public override Date ReferenceDate() => kahale.ReferenceDate();

        public override double MinStrike() => kahale.MinStrike();

        public override double MaxStrike() => kahale.MaxStrike();

        public override double? AtmLevel() => kahale.AtmLevel();

        public override double OptionPrice(double strike, OptionType type = OptionType.Call, double discount = 1.0) =>
            kahale.OptionPrice(strike, type, discount);

        public override double InverseDigitalCall(double price, double discount = 1.0)
        {
            if (price >= kahale.DigitalOptionPrice(0.0, OptionType.Call, discount, Gap))
            {
                return 0.0;
            }

            if (price <= kahale.DigitalOptionPrice(2.0, OptionType.Call, discount, Gap))
            {
                return 2.0;
            }

            return new Brent().Solve(k => kahale.DigitalOptionPrice(k, OptionType.Call, discount, Gap) - price, Accuracy, 0.03, 0.0, 2.0);
        }

        protected override double VolatilityImpl(double strike) => kahale.Volatility(strike);
    }
}
