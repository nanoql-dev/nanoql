// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The calibration contract is the interface (playbook §2's calibration row): a model whose class derives from another
/// base, as later ports' Gaussian and local-volatility models do, implements <see cref="ICalibratedModel"/> over its own
/// arguments, and <c>Calibrate</c> calibrates it through the interface alone, returning a new model of its own type and
/// leaving the receiver unchanged, with the record the same model on <see cref="CalibratedModel"/> gives.
/// </summary>
public class CalibratedModelInterfaceTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.January, 2025) };

    [Fact]
    public void AModelOverAnotherBaseCalibratesThroughTheInterface()
    {
        // Three points of the line 0.5 + 2x, so the least-squares optimum is (0.5, 2).
        CalibrationHelper[] helpers = [new PointHelper(0.0, 0.5), new PointHelper(1.0, 2.5), new PointHelper(2.0, 4.5)];
        var method = new LevenbergMarquardt();
        var endCriteria = new EndCriteria(1000, 100, 1e-12, 1e-12, 1e-12);
        var model = new LineModel(1.0, 1.0);

        CalibrationResults<LineModel> result = model.Calibrate(helpers, method, endCriteria, Context);
        LineModel calibrated = result.Model;
        Assert.IsType<LineModel>(calibrated);
        Assert.NotSame(model, calibrated);
        Assert.Equal(0.5, calibrated.Params()[0], 1e-10);
        Assert.Equal(2.0, calibrated.Params()[1], 1e-10);
        Assert.True(result.FunctionEvaluation > 0, $"function evaluations {result.FunctionEvaluation}");

        // The receiver keeps its parameters.
        Assert.Equal((double[])[1.0, 1.0], model.Params());

        // The same model on CalibratedModel's machinery calibrates alike, its record and its count included.
        CalibrationResults<LineOnCalibratedModel> twin = new LineOnCalibratedModel(1.0, 1.0).Calibrate(helpers, method, endCriteria, Context);
        Assert.Equal(twin.Model.Params(), calibrated.Params());
        Assert.Equal(twin.EndCriteria, result.EndCriteria);
        Assert.Equal(twin.ProblemValues, result.ProblemValues);
        Assert.Equal(twin.FunctionEvaluation, result.FunctionEvaluation);
        Assert.Equal(twin.Model.Value(new Vector([0.4, 1.9]), helpers, Context), calibrated.Value(new Vector([0.4, 1.9]), helpers, Context));

        // Its constraint and fixed parameters reach the optimization: the slope held at 1 by its flag.
        CalibrationResults<LineModel> fixedSlope = model.Calibrate(helpers, method, endCriteria, Context, fixParameters: [false, true]);
        Assert.Equal(1.0, fixedSlope.Model.Params()[1]);
        Assert.Equal(1.5, fixedSlope.Model.Params()[0], 1e-10);
    }

    // A two-parameter model of one's own over object: the intercept and the positive slope of a line, each a constant
    // parameter it holds in its own array.
    private sealed class LineModel : ICalibratedModel
    {
        private readonly ImmutableArray<Parameter> arguments;

        public LineModel(double intercept, double slope)
            : this([new ConstantParameter(intercept, new NoConstraint()), new ConstantParameter(slope, new PositiveConstraint())])
        {
        }

        private LineModel(ImmutableArray<Parameter> arguments)
        {
            this.arguments = arguments;
            Constraint = new SliceConstraint(arguments);
        }

        public ImmutableArray<Parameter> Arguments => arguments;

        public Constraint Constraint { get; }

        public Vector Params() => [arguments[0].Params()[0], arguments[1].Params()[0]];

        public ICalibratedModel WithParams(Vector parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (parameters.Size != 2)
            {
                throw new ArgumentException("two parameters expected", nameof(parameters));
            }

            return new LineModel([arguments[0].SetParam(0, parameters[0]), arguments[1].SetParam(0, parameters[1])]);
        }
    }

    // The same line on CalibratedModel.
    private sealed class LineOnCalibratedModel : CalibratedModel
    {
        public LineOnCalibratedModel(double intercept, double slope)
            : base(2)
        {
            SetArgument(0, new ConstantParameter(intercept, new NoConstraint()));
            SetArgument(1, new ConstantParameter(slope, new PositiveConstraint()));
        }
    }

    // Each argument's constraint on its own parameter.
    private sealed class SliceConstraint(ImmutableArray<Parameter> arguments) : Constraint
    {
        public override bool Test(Vector p) =>
            arguments[0].TestParams(new Vector([p[0]])) && arguments[1].TestParams(new Vector([p[1]]));
    }

    // A helper of one's own, priced by its own formula on any model's parameters: the line's value at a point less the
    // point's target.
    private sealed class PointHelper(double x, double target) : CalibrationHelper
    {
        public override double CalibrationError(ICalibratedModel model, EvaluationContext context)
        {
            Vector p = model.Params();
            return p[0] + (p[1] * x) - target;
        }
    }
}
