// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The finite-difference vector files' specs, as tools/vectors/gen/methods/finitedifferences/meshers/
/// fdmblackscholesmesher.py and the generators beside it write them: 1-D and composite meshers and quanto helpers.
/// </summary>
internal static class FdInputs
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>The 1-D mesher a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which the processes and the dividends' amounts
    /// read.</param>
    /// <returns>The mesher.</returns>
    public static Fdm1dMesher Mesher1dOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        switch (type)
        {
            case "Uniform":
                return new Uniform1dMesher(Real(spec, "start"), Real(spec, "end"), spec.GetProperty("size").GetInt32());
            case "Concentrating":
                JsonElement c = spec.GetProperty("cPoint");
                return new Concentrating1dMesher(
                    Real(spec, "start"),
                    Real(spec, "end"),
                    spec.GetProperty("size").GetInt32(),
                    (NullableReal(c[0]), NullableReal(c[1])),
                    spec.GetProperty("requireCPoint").GetBoolean());
            case "ConcentratingPoints":
                return new Concentrating1dMesher(
                    Real(spec, "start"),
                    Real(spec, "end"),
                    spec.GetProperty("size").GetInt32(),
                    [.. spec.GetProperty("points").EnumerateArray()
                        .Select(p => (p[0].GetDouble(), p[1].GetDouble(), p[2].GetBoolean()))],
                    Real(spec, "tol"));
            case "BlackScholes":
                JsonElement cPoint = spec.GetProperty("cPoint");
                return new FdmBlackScholesMesher(
                    spec.GetProperty("size").GetInt32(),
                    ProcessOf(spec.GetProperty("process"), context),
                    Real(spec, "maturity"),
                    Real(spec, "strike"),
                    NullableReal(spec.GetProperty("xMin")),
                    NullableReal(spec.GetProperty("xMax")),
                    Real(spec, "eps"),
                    Real(spec, "scaleFactor"),
                    cPoint.ValueKind == JsonValueKind.Null ? default : (cPoint[0].GetDouble(), cPoint[1].GetDouble()),
                    DividendsOf(spec.GetProperty("dividends")),
                    QuantoOf(spec.GetProperty("quanto"), context),
                    Real(spec, "spotAdjustment"),
                    context);
            default:
                throw new InvalidOperationException($"unknown mesher '{type}'");
        }
    }

    /// <summary>
    /// The composite mesher a spec describes: its 1-D meshers, in their directions, on an explicit layout where the
    /// spec gives one.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The mesher.</returns>
    public static FdmMesherComposite MesherOf(JsonElement spec, EvaluationContext context)
    {
        Fdm1dMesher[] meshers = [.. spec.GetProperty("meshers").EnumerateArray().Select(m => Mesher1dOf(m, context))];
        return spec.TryGetProperty("layout", out JsonElement layout) && layout.ValueKind != JsonValueKind.Null
            ? new FdmMesherComposite(new FdmLinearOpLayout(layout.EnumerateArray().Select(d => d.GetInt32())), meshers)
            : new FdmMesherComposite(meshers);
    }

    /// <summary>
    /// The Black-Scholes operator a spec of tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesop.py
    /// describes, on its mesher.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The operator and its mesher.</returns>
    public static (FdmBlackScholesOp Op, FdmMesherComposite Mesher) OperatorOf(JsonElement spec, EvaluationContext context)
    {
        FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), context);
        var op = new FdmBlackScholesOp(
            mesher,
            ProcessOf(spec.GetProperty("process"), context),
            Real(spec, "strike"),
            spec.GetProperty("localVol").GetBoolean(),
            NullableReal(spec.GetProperty("illegalLocalVolOverwrite")),
            spec.GetProperty("direction").GetInt32(),
            QuantoOf(spec.GetProperty("quanto"), context));
        return (op, mesher);
    }

    /// <summary>The inner-value calculator a spec describes, on a mesher.</summary>
    /// <param name="spec">The spec: its type, payoff and direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <returns>The calculator.</returns>
    public static FdmInnerValueCalculator CalculatorOf(JsonElement spec, FdmMesher mesher) =>
        spec.GetProperty("type").GetString() switch
        {
            "Zero" => new FdmZeroInnerValue(),
            "Log" => new FdmLogInnerValue(PayoffOf(spec.GetProperty("payoff")), mesher, spec.GetProperty("direction").GetInt32()),
            "CellAveraging" => new FdmCellAveragingInnerValue(
                PayoffOf(spec.GetProperty("payoff")), mesher, spec.GetProperty("direction").GetInt32()),
            string t => throw new InvalidOperationException($"unknown calculator '{t}'"),
            null => throw new InvalidOperationException("no calculator type"),
        };

    /// <summary>The step condition a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The inner-value calculator the exercise conditions read.</param>
    /// <param name="referenceDate">The reference date times are measured from.</param>
    /// <param name="dayCounter">The day counter times are measured in.</param>
    /// <param name="context">The case's evaluation context, which the dividends' amounts read.</param>
    /// <returns>The condition.</returns>
    public static StepCondition ConditionOf(
        JsonElement spec,
        FdmMesher mesher,
        FdmInnerValueCalculator calculator,
        Date referenceDate,
        DayCounter dayCounter,
        EvaluationContext context) =>
        spec.GetProperty("type").GetString() switch
        {
            "American" => new FdmAmericanStepCondition(mesher, calculator),
            "Bermudan" => new FdmBermudanStepCondition(
                spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())), referenceDate, dayCounter, mesher, calculator),
            "Snapshot" => new FdmSnapshotCondition(spec.GetProperty("t").GetDouble()),
            "Dividend" => new FdmDividendHandler(
                DividendsOf(spec.GetProperty("dividends")), mesher, referenceDate, dayCounter, spec.GetProperty("direction").GetInt32(), context),
            string t => throw new InvalidOperationException($"unknown condition '{t}'"),
            null => throw new InvalidOperationException("no condition type"),
        };

    /// <summary>The scheme description a spec describes: QuantLib's type, theta and mu.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc SchemeOf(JsonElement spec) =>
        new((FdmSchemeDescFdmSchemeType)spec.GetProperty("type").GetInt32(), Real(spec, "theta"), Real(spec, "mu"));

    /// <summary>
    /// Rolls back a case's initial values as a rollback spec of tools/vectors/gen/methods/finitedifferences/solvers/
    /// fdmbackwardsolver.py describes: its operator, conditions and scheme, from and to its times.
    /// </summary>
    /// <param name="spec">The rollback spec.</param>
    /// <param name="initial">The initial values.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The rolled-back values.</returns>
    public static double[] Rollback(JsonElement spec, double[] initial, EvaluationContext context)
    {
        (FdmBlackScholesOp op, FdmMesherComposite mesher) = OperatorOf(spec.GetProperty("op"), context);
        FdmInnerValueCalculator calculator = CalculatorOf(spec.GetProperty("calculator"), mesher);
        FdmStepConditionComposite composite = CompositeOf(spec, mesher, calculator, context);
        double[] values = [.. initial];
        new FdmBackwardSolver(op, [], composite, SchemeOf(spec.GetProperty("scheme"))).Rollback(
            values, Real(spec, "from"), Real(spec, "to"), spec.GetProperty("steps").GetInt32(), spec.GetProperty("dampingSteps").GetInt32());
        return values;
    }

    /// <summary>
    /// The conditions' composite a rollback or solver spec describes: its conditions, read with its reference date and
    /// day counter, and its stopping times.
    /// </summary>
    /// <param name="spec">The rollback or solver spec.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The inner-value calculator the exercise conditions read.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The composite.</returns>
    public static FdmStepConditionComposite CompositeOf(
        JsonElement spec, FdmMesher mesher, FdmInnerValueCalculator calculator, EvaluationContext context)
    {
        var referenceDate = new Date(spec.GetProperty("referenceDate").GetInt32());
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        StepCondition[] conditions =
        [
            .. spec.GetProperty("conditions").EnumerateArray()
                .Select(c => ConditionOf(c, mesher, calculator, referenceDate, dayCounter, context)),
        ];
        return new FdmStepConditionComposite([Doubles(spec.GetProperty("stoppingTimes"))], conditions);
    }

    /// <summary>
    /// The exercise a finite-difference spec describes: European at its last date, American from the evaluation date to
    /// it, or Bermudan at every date.
    /// </summary>
    /// <param name="spec">The spec: its type and dates.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The exercise.</returns>
    public static Exercise FdExerciseOf(JsonElement spec, EvaluationContext context)
    {
        Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        return spec.GetProperty("type").GetString() switch
        {
            "European" => new EuropeanExercise(dates[^1]),
            "American" => new AmericanExercise(context.EvaluationDate, dates[^1]),
            "Bermudan" => new BermudanExercise(dates),
            string s => throw new InvalidOperationException($"unknown exercise '{s}'"),
            null => throw new InvalidOperationException("no exercise type"),
        };
    }

    /// <summary>
    /// Prices a pricing spec of tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py with
    /// <see cref="FdBlackScholesVanillaEngine"/>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    /// <remarks>
    /// The engine is <see cref="MakeFdBlackScholesVanillaEngine"/>'s, every setting through its setter, so the vectors
    /// check each setter against QuantLib's engine given the same argument.
    /// </remarks>
    public static OneAssetOptionResults FdPrice(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<Dividend> dividends = DividendsOf(spec.GetProperty("dividends"));
        MakeFdBlackScholesVanillaEngine builder = new MakeFdBlackScholesVanillaEngine(ProcessOf(spec.GetProperty("process"), context))
            .WithQuantoHelper(QuantoOf(spec.GetProperty("quanto"), context))
            .WithTGrid(spec.GetProperty("tGrid").GetInt32())
            .WithXGrid(spec.GetProperty("xGrid").GetInt32())
            .WithDampingSteps(spec.GetProperty("dampingSteps").GetInt32())
            .WithFdmSchemeDesc(SchemeOf(spec.GetProperty("scheme")))
            .WithLocalVol(spec.GetProperty("localVol").GetBoolean())
            .WithCashDividendModel((FdBlackScholesVanillaEngineCashDividendModel)spec.GetProperty("cashDividendModel").GetInt32());
        if (NullableReal(spec.GetProperty("illegalLocalVolOverwrite")) is double overwrite)
        {
            builder = builder.WithIllegalLocalVolOverwrite(overwrite);
        }

        if (!dividends.IsEmpty)
        {
            // the specs' dividends are fixed amounts, which the builder takes as dates and amounts
            builder = builder.WithCashDividends(dividends.Select(d => d.Date), dividends.Select(d => d.Amount(0.0)));
        }

        var option = new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context));
        return builder.ToEngine()(option, context);
    }

    /// <summary>
    /// Compares a pricing case: the NPV, delta, gamma and theta, each where the file holds it, or the refusal.
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    public static void CheckFdPricing(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        var priced = new Lazy<OneAssetOptionResults>(() => FdPrice(c.Input("pricing"), context));
        (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
        [
            ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ];
        foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, () => get(priced.Value));
            }
        }
    }

    /// <summary>A step condition applied to a copy of an array at a time.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="a">The array, left as it is.</param>
    /// <param name="t">The time.</param>
    /// <returns>The copy, as the condition left it.</returns>
    public static double[] Applied(StepCondition condition, double[] a, double t)
    {
        double[] x = [.. a];
        condition.ApplyTo(x, t);
        return x;
    }

    /// <summary>Compares a rollback case: its values, or its refusal.</summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    public static void CheckRollback(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        double[] initial = Doubles(c.Input("initial"));
        check.Output(c, "values", () => Rollback(c.Input("rollback"), initial, context));
    }

    /// <summary>
    /// Compares a case of a scheme stepped on its own: the values after each step back from the case's time, and the
    /// scheme's number of iterations after the last.
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="make">The scheme on an operator, with its number of iterations.</param>
    public static void CheckDirectScheme(VectorAssert check, VectorCase c, Func<FdmLinearOpComposite, (IFdmScheme Scheme, Func<int> Iterations)> make)
    {
        JsonElement spec = c.Input("scheme");
        (FdmBlackScholesOp op, _) = OperatorOf(spec.GetProperty("op"), c.Context());
        (IFdmScheme scheme, Func<int> iterations) = make(op);
        double dt = spec.GetProperty("dt").GetDouble();
        double t = spec.GetProperty("from").GetDouble();
        double[] a = Doubles(c.Input("initial"));
        scheme.SetStep(dt);
        var values = new List<object?>();
        for (int k = 0; k < spec.GetProperty("steps").GetInt32(); ++k)
        {
            scheme.Step(a, t);
            t -= dt;
            values.Add(a.ToArray());
        }

        check.Output(c, "values", values);
        check.Output(c, "numberOfIterations", iterations());
    }

    /// <summary>
    /// Compares a 1-D mesher case: its locations, dplus and dminus, or its refusal where the file expects a
    /// <c>mesher</c> output.
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    public static void CheckMesher1d(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        if (c.Expected.TryGetProperty("mesher", out _))
        {
            check.Output(c, "mesher", () => Mesher1dOf(c.Input("mesher"), context));
            return;
        }

        (double[] locations, double?[] dplus, double?[] dminus) = Read(Mesher1dOf(c.Input("mesher"), context));
        check.Output(c, "locations", locations);
        check.Output(c, "dplus", dplus);
        check.Output(c, "dminus", dminus);
    }

    /// <summary>The quanto helper a spec describes, or <see langword="null"/> for a null spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The helper.</returns>
    public static FdmQuantoHelper? QuantoOf(JsonElement spec, EvaluationContext context) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new FdmQuantoHelper(
                CurveOf(spec.GetProperty("riskFree")),
                CurveOf(spec.GetProperty("foreign")),
                BlackVolOf(spec.GetProperty("fxVolatility"), context),
                Real(spec, "correlation"),
                Real(spec, "atmLevel"));

    /// <summary>
    /// Compares a derivative operator case of tools/vectors/gen/methods/finitedifferences/operators/
    /// triplebandlinearop.py: the operator applied to each seeded vector, and its splitting solve on the first at each
    /// (a, b).
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="make">Builds the operator in a direction on a mesher.</param>
    public static void CheckDerivativeOp(VectorAssert check, VectorCase c, Func<int, FdmMesher, TripleBandLinearOp> make)
    {
        FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
        TripleBandLinearOp op = make(c.Int("direction"), mesher);
        Vector[] rs = [.. c.Input("r").EnumerateArray().Select(VectorOf)];
        check.Output(c, "apply", rs.Select(r => (object?)op.Apply(r).ToArray()).ToArray());
        check.Output(
            c,
            "solveSplitting",
            c.Input("splitting").EnumerateArray()
                .Select(ab => (object?)op.SolveSplitting(rs[0], ab[0].GetDouble(), ab[1].GetDouble()).ToArray()).ToArray());
    }

    /// <summary>A 1-D mesher's locations, dplus and dminus, QuantLib's null distances as
    /// <see langword="null"/>.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <returns>The three lists, as the vector files write them.</returns>
    public static (double[] Locations, double?[] Dplus, double?[] Dminus) Read(Fdm1dMesher mesher) =>
        ([.. mesher.Locations()],
         [.. Enumerable.Range(0, mesher.Size).Select(i => Nullable(mesher.Dplus(i)))],
         [.. Enumerable.Range(0, mesher.Size).Select(i => Nullable(mesher.Dminus(i)))]);

    /// <summary>A value as the vector files write it: QuantLib's <c>Null&lt;Real&gt;()</c> as
    /// <see langword="null"/>.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public static double? Nullable(double x) => x == NullReal ? null : x;

    /// <summary>The doubles a JSON array holds, as a vector.</summary>
    /// <param name="spec">The array.</param>
    /// <returns>The vector.</returns>
    public static Vector VectorOf(JsonElement spec) => new(Doubles(spec));

    private static double Real(JsonElement spec, string name) => spec.GetProperty(name).GetDouble();

    private static double? NullableReal(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : spec.GetDouble();
}
