// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GeneralLinearLeastSquares vectors, <c>tests/vectors/cxx/math/generallinearleastsquares.json</c>:
/// one- and multi-factor fits on the Longstaff-Schwartz basis systems, their coefficients, residuals, modelling
/// uncertainty, size and dimension, and their standard errors on two-function bases, where QuantLib's n − 2 degrees of
/// freedom are nanoql's n − p; and the refusal of fewer samples than functions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GeneralLinearLeastSquaresCxxVectorTests
{
    [Fact]
    public void GeneralLinearLeastSquaresCxxVector()
    {
        var file = VectorFile.Load("cxx/math/generallinearleastsquares");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var type = (LsmBasisSystemPolynomialType)c.Int("polynomialType");
            double[] ys = c.Doubles("ys");
            Func<GeneralLinearLeastSquares> fit = c.Inputs.TryGetProperty("xs", out JsonElement xs)
                ? () => GeneralLinearLeastSquares.Create(EquityInputs.Doubles(xs), ys, LsmBasisSystem.PathBasisSystem(c.Int("order"), type))
                : () => GeneralLinearLeastSquares.Create(
                    [.. c.Input("points").EnumerateArray().Select(p => new Vector(EquityInputs.Doubles(p)))],
                    ys,
                    LsmBasisSystem.MultiPathBasisSystem(c.Int("dim"), c.Int("order"), type));
            if (!c.Expected.TryGetProperty("residuals", out _))
            {
                check.Output(c, "coefficients", () => fit().Coefficients().AsSpan().ToArray());
                continue;
            }

            GeneralLinearLeastSquares ls = fit();
            check.Output(c, "coefficients", ls.Coefficients().AsSpan().ToArray());
            check.Output(c, "residuals", ls.Residuals().AsSpan().ToArray());
            check.Output(c, "error", ls.Error().AsSpan().ToArray());
            check.Output(c, "size", ls.Size);
            check.Output(c, "dim", ls.Dim);
            if (c.Expected.TryGetProperty("standardErrors", out _))
            {
                check.Output(c, "standardErrors", ls.StandardErrors().AsSpan().ToArray());
            }
        }

        check.Complete();
    }
}
