// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.BrownianBridgeVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the curves, short-rate models and lattices the model and lattice vector files describe in their inputs, as
/// their generators and the C++ harness build them (<c>tools/cxx/src/models/modelspec.hpp</c>), and compares the
/// outputs the model files share.
/// </summary>
/// <remarks>
/// A curve is <c>{"type": "FlatForward", "referenceDate", "forward", "dayCounter", "compounding", "frequency"}</c>,
/// <c>{"type": "DiscountCurve", "dates", "discounts", "dayCounter"}</c> (an
/// <see cref="InterpolatedDiscountCurve{TInterpolator}"/> of <see cref="LogLinear"/>), or, in the wheel's files only,
/// <c>{"type": "PiecewiseYieldCurve", "curve", "helpers"}</c>, a bootstrapped curve of <see cref="BootstrapInputs"/>
/// on rate helpers of <see cref="RateHelperInputs"/>, built in the case's context. A model is <c>{"type": "Vasicek",
/// "r0", "a", "b", "sigma", "lambda", "params"}</c> or <c>{"type": "HullWhite", "curve", "a", "sigma", "params"}</c>,
/// with <c>"params"</c> null or the parameters set on it with <see cref="CalibratedModelExtensions.SetParams"/>. A
/// lattice is <c>{"model", "timeGrid", "spread"}</c>: the model's <see cref="ShortRateModel.Tree"/> on the grid, with
/// <see cref="OneFactorModelShortRateTree.SetSpread"/> for a spread that isn't null.
/// </remarks>
internal static class ModelInputs
{
    /// <summary>The curve a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, which a bootstrapped curve is built in.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure CurveOf(JsonElement spec, EvaluationContext? context = null) =>
        spec.GetProperty("type").GetString() switch
        {
            "FlatForward" => new FlatForward(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                Number(spec.GetProperty("forward")),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                (Compounding)spec.GetProperty("compounding").GetInt32(),
                (Frequency)spec.GetProperty("frequency").GetInt32()),
            "DiscountCurve" => new InterpolatedDiscountCurve<LogLinear>(
                spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                Numbers(spec.GetProperty("discounts")),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"))),
            "PiecewiseYieldCurve" => Bootstrapped(spec, context ?? throw new ArgumentNullException(nameof(context))),
            string type => throw new InvalidOperationException($"unknown curve {type}"),
            null => throw new InvalidOperationException("a curve spec without a type"),
        };

    /// <summary>The model a spec describes, with its parameters set when the spec gives them.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, which a bootstrapped curve is built in.</param>
    /// <returns>The model.</returns>
    public static Vasicek ModelOf(JsonElement spec, EvaluationContext? context = null)
    {
        Vasicek model = spec.GetProperty("type").GetString() switch
        {
            "Vasicek" => new Vasicek(
                Number(spec.GetProperty("r0")),
                Number(spec.GetProperty("a")),
                Number(spec.GetProperty("b")),
                Number(spec.GetProperty("sigma")),
                Number(spec.GetProperty("lambda"))),
            "HullWhite" => new HullWhite(
                CurveOf(spec.GetProperty("curve"), context),
                Number(spec.GetProperty("a")),
                Number(spec.GetProperty("sigma"))),
            string type => throw new InvalidOperationException($"unknown model {type}"),
            null => throw new InvalidOperationException("a model spec without a type"),
        };
        JsonElement parameters = spec.GetProperty("params");
        return parameters.ValueKind == JsonValueKind.Null ? model : model.SetParams(VectorOf(parameters));
    }

    /// <summary>
    /// The G2 model a spec describes, <c>{"type": "G2", "curve", "a", "sigma", "b", "eta", "rho", "params"}</c>, with
    /// its parameters set when the spec gives them.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, which a bootstrapped curve is built in.</param>
    /// <returns>The model.</returns>
    public static G2 G2Of(JsonElement spec, EvaluationContext? context = null)
    {
        var model = new G2(
            CurveOf(spec.GetProperty("curve"), context),
            Number(spec.GetProperty("a")),
            Number(spec.GetProperty("sigma")),
            Number(spec.GetProperty("b")),
            Number(spec.GetProperty("eta")),
            Number(spec.GetProperty("rho")));
        JsonElement parameters = spec.GetProperty("params");
        return parameters.ValueKind == JsonValueKind.Null ? model : model.SetParams(VectorOf(parameters));
    }

    /// <summary>
    /// The Black-Karasinski model a spec describes, <c>{"type": "BlackKarasinski", "curve", "a", "sigma", "params"}</c>,
    /// with its parameters set when the spec gives them.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, which a bootstrapped curve is built in.</param>
    /// <returns>The model.</returns>
    public static BlackKarasinski BlackKarasinskiOf(JsonElement spec, EvaluationContext? context = null)
    {
        var model = new BlackKarasinski(
            CurveOf(spec.GetProperty("curve"), context), Number(spec.GetProperty("a")), Number(spec.GetProperty("sigma")));
        JsonElement parameters = spec.GetProperty("params");
        return parameters.ValueKind == JsonValueKind.Null ? model : model.SetParams(VectorOf(parameters));
    }

    /// <summary>
    /// The lattice a spec describes: the model's tree on the grid, with the spread when the spec gives one.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The lattice.</returns>
    public static OneFactorModelShortRateTree LatticeOf(JsonElement spec)
    {
        OneFactorModelShortRateTree tree = TreeOf(ModelOf(spec.GetProperty("model")), spec.GetProperty("timeGrid"));
        JsonElement spread = spec.GetProperty("spread");
        return spread.ValueKind == JsonValueKind.Null ? tree : tree.SetSpread(Number(spread));
    }

    /// <summary>
    /// Compares an affine model case's outputs: the parameters, the discounts, the discount bonds and the options.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="model">The model.</param>
    public static void CheckAffine(VectorAssert check, VectorCase c, Vasicek model)
    {
        check.Output(c, "params", Elements(model.Params()));
        check.Output(c, "discount", Numbers(c.Input("times")).Select(t => Lazy(() => model.Discount(t))).ToArray());
        double[][] bonds = Points(c, "bondPoints");
        check.Output(c, "discountBond", Each(bonds, b => model.DiscountBond(b[0], b[1], b[2])));
        if (c.Expected.TryGetProperty("discountBondFactors", out _))
        {
            check.Output(
                c, "discountBondFactors", Each(bonds, b => model.DiscountBond(b[0], b[1], new Vector([b[2]]))));
            check.Output(
                c, "discountBondTwoFactors", Each(bonds, b => model.DiscountBond(b[0], b[1], new Vector([b[2], 9.0]))));
        }

        check.Output(c, "discountBondOption", Each(
            Points(c, "options"), o => Lazy(() => model.DiscountBondOption((OptionType)(int)o[0], o[1], o[2], o[3]))));
    }

    /// <summary>
    /// Compares a harness model case's outputs (<c>tools/vectors/gen/cxx/models/shortrate/onefactormodels/</c>): the
    /// inspectors, the dynamics and its process at the case's points, the five-argument option through
    /// <see cref="IAffineModel"/>, the constraint at the case's parameters, and the tree on the case's grid (each
    /// column's size, and the outputs among the underlying, the discounts, the state prices, the descendants and the
    /// probabilities that the case holds).
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="model">The model the case's spec describes.</param>
    public static void CheckModel(VectorAssert check, VectorCase c, Vasicek model)
    {
        check.Output(c, "a", model.A());
        check.Output(c, "b", model.B());
        check.Output(c, "sigma", model.Sigma);
        check.Output(c, "lambda", model.Lambda);
        check.Output(c, "r0", model.R0);
        OneFactorModelShortRateDynamics dynamics = model.Dynamics();
        StochasticProcess1D process = dynamics.Process;
        double[][] processPoints = Points(c, "processPoints");
        double[][] stepPoints = Points(c, "stepPoints");
        check.Output(c, "variable", Each(Points(c, "variablePoints"), p => dynamics.Variable(p[0], p[1])));
        check.Output(c, "shortRate", Each(Points(c, "shortRatePoints"), p => dynamics.ShortRate(p[0], p[1])));
        check.Output(c, "processX0", process.X0);
        check.Output(c, "drift", Each(processPoints, p => process.Drift(p[0], p[1])));
        check.Output(c, "diffusion", Each(processPoints, p => process.Diffusion(p[0], p[1])));
        check.Output(c, "expectation", Each(stepPoints, p => process.Expectation(p[0], p[1], p[2])));
        check.Output(c, "stdDeviation", Each(stepPoints, p => process.StdDeviation(p[0], p[1], p[2])));
        check.Output(c, "variance", Each(stepPoints, p => process.Variance(p[0], p[1], p[2])));
        IAffineModel affine = model;
        check.Output(c, "discountBondOption", Each(
            Points(c, "options"), o => affine.DiscountBondOption((OptionType)(int)o[0], o[1], o[2], o[3], o[4])));
        Vector[] parameters = [.. c.Input("constraintParams").EnumerateArray().Select(VectorOf)];
        Constraint constraint = model.Constraint;
        check.Output(c, "constraintTest", parameters.Select(p => (object?)constraint.Test(p)).ToArray());
        check.Output(c, "upperBound", parameters.Select(p => (object?)Elements(constraint.UpperBound(p))).ToArray());
        check.Output(c, "lowerBound", parameters.Select(p => (object?)Elements(constraint.LowerBound(p))).ToArray());

        OneFactorModelShortRateTree tree = TreeOf(model, c.Input("timeGrid"));
        int columns = tree.TimeGrid.Size;
        int[] sizes = [.. Enumerable.Range(0, columns).Select(tree.Size)];
        check.Output(c, "treeSize", sizes.Select(n => (object?)n).ToArray());
        if (c.Expected.TryGetProperty("treeUnderlying", out _))
        {
            check.Output(c, "treeUnderlying", Nodes(sizes, columns, (i, j) => tree.Underlying(i, j)));
        }

        check.Output(c, "treeDiscount", Nodes(sizes, columns - 1, (i, j) => tree.Discount(i, j)));
        check.Output(c, "statePrices", Columns(columns, i => tree.StatePrices(i)));
        if (c.Expected.TryGetProperty("treeDescendant", out _))
        {
            int steps = columns - 1;
            check.Output(c, "treeDescendant", Nodes(sizes, steps, (i, j) => Branches(b => tree.Descendant(i, j, b))));
            check.Output(c, "treeProbability", Nodes(sizes, steps, (i, j) => Branches(b => tree.Probability(i, j, b))));
        }
    }

    /// <summary>
    /// Compares a harness model refusal case's output: the first column's size of the tree on the case's grid, or the
    /// five-argument option at the case's first point.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    public static void CheckRefusal(VectorAssert check, VectorCase c)
    {
        if (c.Expected.TryGetProperty("treeSize", out _))
        {
            OneFactorModelShortRateTree Tree() => TreeOf(ModelOf(c.Input("model")), c.Input("timeGrid"));
            check.Output(c, "treeSize", new[] { Lazy(() => Tree().Size(0)) });
        }
        else
        {
            double[] o = Points(c, "options")[0];
            IAffineModel Model() => ModelOf(c.Input("model"));
            check.Output(
                c,
                "discountBondOption",
                new[] { Lazy(() => Model().DiscountBondOption((OptionType)(int)o[0], o[1], o[2], o[3], o[4])) });
        }
    }

    /// <summary>A vector of reals, encoded as the vectors encode them.</summary>
    /// <param name="e">The JSON array.</param>
    /// <returns>The vector.</returns>
    public static Vector VectorOf(JsonElement e) => new(Numbers(e));

    /// <summary>An input's points: a list of lists of reals.</summary>
    /// <param name="c">The case.</param>
    /// <param name="name">The input's name.</param>
    /// <returns>The points.</returns>
    public static double[][] Points(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(Numbers)];

    /// <summary>A vector's elements, for an expected array.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>Its elements.</returns>
    public static object?[] Elements(Vector v) => [.. Enumerable.Range(0, v.Size).Select(i => (object?)v[i])];

    private static YieldTermStructure Bootstrapped(JsonElement spec, EvaluationContext context) =>
        BootstrapInputs.CurveOf(
            spec.GetProperty("curve"), RateHelperInputs.HelpersOf(spec.GetProperty("helpers"), context), context).Curve;

    private static OneFactorModelShortRateTree TreeOf(Vasicek model, JsonElement grid) =>
        (OneFactorModelShortRateTree)model.Tree(Grid(grid));

    // An output computed when compared, so that a throw is checked.
    private static object Lazy(Func<object?> f) => f;

    private static object?[] Each(double[][] points, Func<double[], object?> f) => [.. points.Select(f)];

    private static object?[] Columns(int columns, Func<int, object?> f) => [.. Enumerable.Range(0, columns).Select(f)];

    // One element per node of each column before the given one.
    private static object?[] Nodes(int[] sizes, int columns, Func<int, int, object?> f) =>
        Columns(columns, i => Enumerable.Range(0, sizes[i]).Select(j => f(i, j)).ToArray());

    private static object?[] Branches(Func<int, object?> f) => [.. Enumerable.Range(0, 3).Select(f)];
}
