// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the finite-difference core's refusals that the vector files can't
/// tell apart (they record a refusal as <c>{"throws": true}</c>, accepting either type), and those of the inputs
/// QuantLib leaves undefined: meshers of fewer than two points, a one-point solve, an empty layout, more meshers than
/// dimensions and bands of different sizes
/// (<c>Divergences/FdmUndefinedPathRefusalTests.cs</c>); and the argument checks no vector reaches: the dividend
/// schedules' null and default values, the backward solver's null boundary condition, the model's rollback forward in
/// time, the Thomas sweep's zero pivot, and an operator's sparse matrices where it has none.
/// </summary>
public class FdmErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void UndefinedPathRefusals()
    {
        Refused(() => new Uniform1dMesher(0.0, 1.0, 0), "size", "a mesher needs at least two points, not 0");
        Refused(() => new Uniform1dMesher(0.0, 1.0, 1), "size", "a mesher needs at least two points, not 1");
        Refused(() => new Concentrating1dMesher(0.0, 1.0, 1, (0.5, 0.1)), "size", "a mesher needs at least two points, not 1");
        Refused(
            () => new Concentrating1dMesher(0.0, 1.0, 0, [(0.5, 0.1, false)]), "size", "a mesher needs at least two points, not 0");
        var onePoint = new FirstDerivativeOp(0, new FdmMesherComposite(new Fdm1dMesher(1)));
        Refused(() => onePoint.SolveSplitting(new Vector(1, 1.0), 0.5, 1.0), "r", "a triple-band solve needs at least two points, not 1");
        Refused(() => new FdmLinearOpLayout([]), "dim", "a layout needs at least one dimension");
        Refused(
            () => new FdmMesherComposite(new FdmLinearOpLayout([5]), [new Uniform1dMesher(0.0, 1.0, 5), new Uniform1dMesher(0.0, 1.0, 4)]),
            "mesher",
            "more 1d meshers (2) than the layout's dimensions (1)");
        var x = new FirstDerivativeOp(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 5)));
        var y = new SecondDerivativeOp(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 7)));
        Refused(() => x.Add(y), "m", "inconsistent size of rhs");
        Refused(() => x.Mult(new Vector(4, 1.0)), "u", "inconsistent size of rhs");
    }

    [Fact]
    public void DividendScheduleRefusals()
    {
        const string NullDividend = "null dividend in schedule";
        const string DefaultSchedule = "default(ImmutableArray) is not a dividend schedule; an empty one is []";
        var context = new EvaluationContext { EvaluationDate = Today };
        DayCounter dc = new Actual365Fixed();
        YieldTermStructure r = new FlatForward(Today, 0.05, dc), q = new FlatForward(Today, 0.02, dc);
        var process = new BlackScholesMertonProcess(100.0, q, r, new BlackConstantVol(Today, new NullCalendar(), 0.25, dc));
        ImmutableArray<Dividend> withNull = [null!];
        ImmutableArray<Dividend> one = [new FixedDividend(2.0, Today + 91)];
        var mesher = new FdmMesherComposite(new Uniform1dMesher(3.0, 6.0, 11));
        var calculator = new FdmLogInnerValue(new PlainVanillaPayoff(OptionType.Put, 100.0), mesher, 0);
        var exercise = new EuropeanExercise(Today + 365);

        Refused(() => new FdmBlackScholesMesher(11, process, 1.0, 100.0, dividendSchedule: withNull, context: context), "dividendSchedule", NullDividend);
        Refused(
            () => new FdmBlackScholesMesher(11, process, 1.0, 100.0, dividendSchedule: one),
            "context",
            "an evaluation context is needed to read the dividends' amounts");
        Refused(() => FdmStepConditionComposite.VanillaComposite(default, exercise, mesher, calculator, Today, dc, context), "schedule", DefaultSchedule);
        Refused(() => FdmStepConditionComposite.VanillaComposite(withNull, exercise, mesher, calculator, Today, dc, context), "schedule", NullDividend);
        Refused(() => new EscrowedDividendAdjustment(default, r, q, d => dc.YearFraction(Today, d), 1.0, context), "dividendSchedule", DefaultSchedule);
        Refused(() => new EscrowedDividendAdjustment(withNull, r, q, d => dc.YearFraction(Today, d), 1.0, context), "dividendSchedule", NullDividend);
        Refused(() => new FdmDividendHandler(default, mesher, Today, dc, 0, context), "schedule", DefaultSchedule);
        Refused(() => new FdmDividendHandler(withNull, mesher, Today, dc, 0, context), "schedule", NullDividend);
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), exercise);
        Refused(() => FdBlackScholesVanillaEngine.Calculate(option, context, process, withNull), "dividends", NullDividend);
    }

    [Fact]
    public void SolverRefusals()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 4.0, 5));
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, dc), new FlatForward(Today, 0.05, dc), new BlackConstantVol(Today, new NullCalendar(), 0.25, dc));
        var op = new FdmBlackScholesOp(mesher, process, 100.0);
        Refused(() => new FdmBackwardSolver(op, [null!], null, FdmSchemeDesc.Douglas()), "bcSet", "null boundary condition in set");

        var model = new FiniteDifferenceModel<DouglasScheme>(new DouglasScheme(0.5, op));
        Refused(() => model.Rollback(new double[5], 0.0, 1.0, 10), "from", "trying to roll back from 0 to 1");

        // on unit spacing the second derivative's first inner row makes b + a·(diag − temp·lower) = 2 + (−2) = 0:
        // QuantLib's QL_ENSURE inside the Thomas sweep (triplebandlinearop.cpp:292)
        var second = new SecondDerivativeOp(0, mesher);
        var e = Assert.Throws<NanoQLException>(() => second.SolveSplitting(new Vector(5, 1.0), 1.0, 2.0));
        Assert.Equal("division by zero", e.Message);
    }

    [Fact]
    public void AnOperatorWithoutSparseMatricesRefusesThem()
    {
        // QuantLib's FdmLinearOpComposite::toMatrixDecomp, which an operator overrides where it has a sparse form
        var op = new NoMatrixOp();
        Assert.Equal(" ublas representation is not implemented", Assert.Throws<NanoQLException>(() => op.ToMatrixDecomp()).Message);
        Assert.Equal(" ublas representation is not implemented", Assert.Throws<NanoQLException>(() => op.ToMatrix()).Message);
    }

    private static void Refused(Action call, string paramName, string message)
    {
        var e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    // An operator of one direction that keeps QuantLib's default toMatrixDecomp.
    private sealed class NoMatrixOp : FdmLinearOpComposite
    {
        public override int Size => 1;

        public override void SetTime(double t1, double t2)
        {
        }

        public override void Apply(ReadOnlySpan<double> r, Span<double> result) => r.CopyTo(result);

        public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result.Clear();

        public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result) => r.CopyTo(result);

        public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result) => r.CopyTo(result);

        public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) => r.CopyTo(result);
    }
}
