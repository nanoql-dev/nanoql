// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
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
/// time, the Thomas sweep's zero pivot, and an operator's sparse matrices where it has none; and the values, directions
/// and meshers QuantLib reads past: a Dirichlet boundary's values short of its last index, a composite's direction it
/// has no mesher for, and a ZABR operator's part on a mesher of one direction; and the indices, coordinates, grids and
/// results it reads past: the mesher's and the modified triple-band operator's accessors, a layout's index of more
/// coordinates than its directions, a jump operator's second direction of one point, a density calculator's grid of no
/// step and a quanto helper's short result.
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

    [Fact]
    public void ValuesDirectionsAndMeshersQuantLibReadsPastAreRefused()
    {
        // A Dirichlet boundary writes each of its indices of the values, through the scheme helper as on its own.
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 5));
        var upper = new FdmDirichletBoundary(mesher, 1.0, 0, BoundaryConditionSide.Upper);
        var timeDep = new FdmTimeDepDirichletBoundary(mesher, t => 1.0, 0, BoundaryConditionSide.Upper);
        var helper = new BoundaryConditionSchemeHelper([upper]);
        double[] four = new double[4];
        foreach (Action call in new Action[]
                 {
                     () => upper.ApplyAfterApplying(four), () => upper.ApplyAfterSolving(four), () => timeDep.ApplyAfterApplying(four),
                     () => timeDep.ApplyAfterSolving(four), () => helper.ApplyAfterApplying(four), () => helper.ApplyAfterSolving(four),
                 })
        {
            Refused(call, "a", "4 values don't reach the boundary's index 4");
        }

        double[] five = new double[5];
        helper.ApplyAfterSolving(five);
        Assert.Equal([0.0, 0.0, 0.0, 0.0, 1.0], five);

        // Their steps before applying and before solving are QuantLib's empty ones: the values are left as they are.
        var op = new FirstDerivativeOp(0, mesher);
        double[] rhs = [1.0, 2.0, 3.0, 4.0, 5.0];
        upper.ApplyBeforeApplying(op);
        upper.ApplyBeforeSolving(op, rhs);
        timeDep.ApplyBeforeApplying(op);
        timeDep.ApplyBeforeSolving(op, rhs);
        Assert.Equal([1.0, 2.0, 3.0, 4.0, 5.0], rhs);

        // A direction the composite has no mesher for.
        var plane = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 3), new Uniform1dMesher(0.0, 1.0, 4));
        Assert.Equal("direction", Assert.Throws<ArgumentOutOfRangeException>(() => plane.Locations(2)).ParamName);
        Assert.Equal("direction", Assert.Throws<ArgumentOutOfRangeException>(() => plane.Locations(-1)).ParamName);
        Assert.Equal(12, plane.Locations(1).Size);

        // The ZABR operator's parts read a forward and a volatility direction.
        Refused(() => _ = new FdmZabrUnderlyingPart(mesher, 0.5, 0.3, -0.2, 1.0), "mesher", "a mesher of 1 directions; the ZABR operator takes 2");
        Refused(() => _ = new FdmZabrVolatilityPart(mesher, 0.5, 0.3, -0.2, 1.0), "mesher", "a mesher of 1 directions; the ZABR operator takes 2");
    }

    [Fact]
    public void IndicesCoordinatesGridsAndResultsQuantLibReadsPastAreRefused()
    {
        // The mesher's accessors and the modified triple-band operator's bands, QuantLib's unchecked reads, refuse an
        // index past their points or a negative one with Array::at's text.
        var line = new Uniform1dMesher(0.0, 1.0, 5);
        var mod = new ModTripleBandLinearOp(0, new FdmMesherComposite(line));
        foreach ((string param, int index, Action call) in new (string, int, Action)[]
                 {
                     ("index", 5, () => line.Dplus(5)), ("index", -1, () => line.Dplus(-1)),
                     ("index", 5, () => line.Dminus(5)), ("index", -1, () => line.Dminus(-1)),
                     ("index", 5, () => line.Location(5)), ("index", -1, () => line.Location(-1)),
                     ("i", 5, () => mod.Lower(5)), ("i", -1, () => mod.Lower(-1)),
                     ("i", 5, () => mod.Diag(5)), ("i", -1, () => mod.Diag(-1)),
                     ("i", 5, () => mod.Upper(5)), ("i", -1, () => mod.Upper(-1)),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith($"index ({index}) must be less than 5: array access out of range", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal((0.25, 0.25, 1.0), (line.Dplus(3), line.Dminus(4), line.Location(4)));
        mod.Diag(4) = 2.0;
        Assert.Equal(2.0, mod.Diag(4));

        // A layout's index of more coordinates than its directions, whose spacing QuantLib reads past.
        FdmLinearOpLayout layout = new FdmMesherComposite(line, new Uniform1dMesher(0.0, 1.0, 3)).Layout;
        Refused(() => layout.Index([1, 2, 0]), "coordinates", "3 coordinates given for 2 directions");
        Assert.Equal(11, layout.Index([1, 2]));

        // The jump operator's second direction, whose second last location QuantLib reads on one point.
        var rTS = new FlatForward(Today, 0.05, new Actual365Fixed());
        var power = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(1.0, 0.5, 3.0, _ => 3.0), 0.0, 5.0, 1.0, 2.0);
        Refused(
            () => _ = new FdmExtOUJumpOp(new FdmMesherComposite(line, new Fdm1dMesher(1)), power, rTS, [], 4),
            "mesher",
            "the jump operator's second direction needs at least two points, not 1");
        Assert.Equal(2, new FdmExtOUJumpOp(new FdmMesherComposite(line, new Uniform1dMesher(0.0, 1.0, 2)), power, rTS, [], 4).Size);

        // The density calculator's first step, which the port reads where QuantLib's at() refuses.
        var vol = new LocalConstantVol(Today, 0.2, new Actual365Fixed());
        Refused(() => _ = new LocalVolRNDCalculator(100.0, rTS, rTS, vol, new TimeGrid([0.0])), "timeGrid", "the time grid has no step");
        Refused(() => _ = new LocalVolRNDCalculator(100.0, rTS, rTS, vol, new TimeGrid()), "timeGrid", "the time grid has no step");

        // The quanto helper's span form writes one adjustment per volatility.
        var quanto = new FdmQuantoHelper(rTS, rTS, new BlackConstantVol(Today, new NullCalendar(), 0.1, new Actual365Fixed()), 0.3, 1.0);
        Refused(() => quanto.QuantoAdjustment([0.2, 0.3], 0.0, 0.5, new double[1]), "result", "the result is shorter than the volatilities");
        double[] adjusted = new double[2];
        quanto.QuantoAdjustment([0.2, 0.3], 0.0, 0.5, adjusted);
        Assert.Equal(quanto.QuantoAdjustment(0.3, 0.0, 0.5), adjusted[1]);
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
