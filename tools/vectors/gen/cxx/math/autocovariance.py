# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's autocovariance functions (ql/math/autocovariance.hpp), which the wheel doesn't expose, each
output computed by the C++ harness: convolutions, autocovariances and autocorrelations of data of 2 to 64 points at lags
0 to n − 1, on data as given and on centred data, and the overloads that remove the mean, with and without reuse: the
mean, the values and the data after the call.

The harness unit is tools/cxx/src/math/autocovariance.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/AutocovarianceCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for n in (2, 3, 5, 8, 10, 16, 17, 33, 64):
            data = [round(rng.uniform(-5, 5), 6) for _ in range(n)]
            # a sequential sum, as the reader's: Python's sum of floats is compensated since 3.12
            total = 0.0
            for x in data:
                total += x
            mean = total / n
            centred = [x - mean for x in data]
            for lag in sorted({0, 1, n // 2, n - 1}):
                inputs = {"settings": st, "data": data, "maxLag": lag}
                out.append({"name": f"{n} points, lag {lag}", "inputs": dict(inputs, call="plain"),
                            "expected": {"convolutions": H.call("convolutions", data, lag),
                                         "autocovariances": H.call("autocovariances", centred, lag),
                                         "autocorrelations": H.call("autocorrelations", centred, lag),
                                         "centred": centred}})
                for reuse in (False, True):
                    cov = H.call("autocovariancesReuse", data, lag, reuse)
                    cor = H.call("autocorrelationsReuse", data, lag, reuse)
                    out.append({"name": f"{n} points, lag {lag}, the mean removed, reuse {str(reuse).lower()}",
                                "inputs": dict(inputs, call="reuse", reuse=reuse),
                                "expected": {"covarianceMean": cov["mean"], "covariances": cov["values"], "covarianceData": cov["data"],
                                             "correlationMean": cor["mean"], "correlations": cor["values"], "correlationData": cor["data"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/autocovariance.hpp", cases)
