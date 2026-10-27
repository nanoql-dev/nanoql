// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's autocovariance vectors, <c>tests/vectors/cxx/math/autocovariance.json</c>: convolutions,
/// autocovariances and autocorrelations at every lag, and the overloads that remove the mean, with and without reuse,
/// their mean, values and the data after the call.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AutocovarianceCxxVectorTests
{
    [Fact]
    public void AutocovarianceCxxVector()
    {
        var file = VectorFile.Load("cxx/math/autocovariance");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = c.Doubles("data");
            int maxLag = c.Int("maxLag");
            if (c.String("call") == "plain")
            {
                // the centred data as the generator forms them, x less the mean of a sequential sum
                double[] centred = Centred(data);
                var conv = new double[maxLag + 1];
                AutocovarianceFunctions.Convolutions(data, conv, maxLag);
                check.Output(c, "convolutions", conv);
                var cov = new double[maxLag + 1];
                AutocovarianceFunctions.Autocovariances(centred, cov, maxLag);
                check.Output(c, "autocovariances", cov);
                var cor = new double[maxLag + 1];
                AutocovarianceFunctions.Autocorrelations(centred, cor, maxLag);
                check.Output(c, "autocorrelations", cor);
                check.Output(c, "centred", centred);
            }
            else
            {
                bool reuse = c.Bool("reuse");
                double[] covData = [.. data], corData = [.. data];
                var cov = new double[maxLag + 1];
                var cor = new double[maxLag + 1];
                check.Output(c, "covarianceMean", AutocovarianceFunctions.Autocovariances(covData, cov, maxLag, reuse));
                check.Output(c, "covariances", cov);
                check.Output(c, "covarianceData", covData);
                check.Output(c, "correlationMean", AutocovarianceFunctions.Autocorrelations(corData, cor, maxLag, reuse));
                check.Output(c, "correlations", cor);
                check.Output(c, "correlationData", corData);
            }
        }

        check.Complete();
    }

    private static double[] Centred(double[] data)
    {
        double sum = 0.0;
        foreach (double x in data)
        {
            sum += x;
        }

        double mean = sum / data.Length;
        return [.. data.Select(x => x - mean)];
    }
}
