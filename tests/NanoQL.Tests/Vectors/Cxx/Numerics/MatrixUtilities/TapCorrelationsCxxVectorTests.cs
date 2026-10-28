// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's triangular angles vectors, <c>tests/vectors/cxx/math/matrixutilities/tapcorrelations.json</c>:
/// <see cref="TapCorrelationsFunctions"/>'s general and LMM parametrizations, constrained and not, at sizes 2 to 5 and
/// every rank, the rank-three spiral and its vectorial form, and <see cref="FrobeniusCostFunction"/>'s value and values
/// against a correlation matrix. The wheel exposes none of them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TapCorrelationsCxxVectorTests
{
    private static readonly Dictionary<string, Func<Vector, int, int, Matrix>> Parametrizations = new()
    {
        ["triangularAnglesParametrization"] = TapCorrelationsFunctions.TriangularAnglesParametrization,
        ["lmmTriangularAnglesParametrization"] = TapCorrelationsFunctions.LmmTriangularAnglesParametrization,
        ["triangularAnglesParametrizationUnconstrained"] =
            TapCorrelationsFunctions.TriangularAnglesParametrizationUnconstrained,
        ["lmmTriangularAnglesParametrizationUnconstrained"] =
            TapCorrelationsFunctions.LmmTriangularAnglesParametrizationUnconstrained,
    };

    [Fact]
    public void TapCorrelationsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/tapcorrelations");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string method = c.String("method");
            switch (method)
            {
                case "triangularAnglesParametrizationRankThree":
                    check.Output(c, "result", MatrixVectorTests.Rows(TapCorrelationsFunctions.TriangularAnglesParametrizationRankThree(
                        c.Double("alpha"), c.Double("t0"), c.Double("epsilon"), c.Int("nbRows"))));
                    break;
                case "triangularAnglesParametrizationRankThreeVectorial":
                    check.Output(c, "result", MatrixVectorTests.Rows(TapCorrelationsFunctions.TriangularAnglesParametrizationRankThreeVectorial(
                        new Vector(c.Doubles("values")), c.Int("nbRows"))));
                    break;
                case "FrobeniusCostFunction":
                    var cost = new FrobeniusCostFunction(
                        MatrixVectorTests.FromRows(c.Input("target")),
                        Parametrizations[c.String("parametrization")],
                        c.Int("matrixSize"),
                        c.Int("rank"));
                    var x = new Vector(c.Doubles("values"));
                    check.Output(c, "value", cost.Value(x));
                    check.Output(c, "values", cost.Values(x).ToArray());
                    break;
                default:
                    check.Output(c, "result", MatrixVectorTests.Rows(
                        Parametrizations[method](new Vector(c.Doubles("values")), c.Int("matrixSize"), c.Int("rank"))));
                    break;
            }
        }

        check.Complete();
    }
}
