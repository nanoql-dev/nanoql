// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/correlations/: exponentialCorrelations and the time-homogeneous forward correlation, which the
// wheel doesn't expose.
//
// "exponentialCorrelations" takes (rateTimes, longTermCorr, beta, gamma, t): the matrix.
// "TimeHomogeneousForwardCorrelation" takes (fwdCorrelation, rateTimes): {"times", "rateTimes", "numberOfRates",
// "correlations"}. "TimeHomogeneousForwardCorrelation::evolvedMatrices" takes (fwdCorrelation), with at least its rows
// less one columns: the matrices.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/correlations/expcorrelations.hpp>
#include <ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value matrices(const std::vector<Matrix>& ms) {
            json::array a;
            for (const Matrix& m : ms)
                a.emplace_back(result(m));
            return a;
        }

        const Register unit({
            {"exponentialCorrelations",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Vec rateTimes = reals(a[0]);
                 Real longTermCorr = real(a[1]), beta = real(a[2]), gamma = real(a[3]), t = real(a[4]);
                 return [=] { return result(exponentialCorrelations(rateTimes, longTermCorr, beta, gamma, t)); };
             }},
            {"TimeHomogeneousForwardCorrelation",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Mat fwd = matrix(a[0]);
                 Vec rateTimes = reals(a[1]);
                 return [=] {
                     TimeHomogeneousForwardCorrelation c(makeMatrix(fwd), rateTimes);
                     json::object out;
                     out["times"] = result(c.times());
                     out["rateTimes"] = result(c.rateTimes());
                     out["numberOfRates"] = static_cast<std::int64_t>(c.numberOfRates());
                     out["correlations"] = matrices(c.correlations());
                     return json::value(out);
                 };
             }},
            {"TimeHomogeneousForwardCorrelation::evolvedMatrices",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Mat fwd = matrix(a[0]);
                 if (!fwd.empty() && fwd.front().size() + 1 < fwd.size())
                     throw InvalidParams("QuantLib reads past a matrix with fewer columns than its rows less one");
                 return [=] { return matrices(TimeHomogeneousForwardCorrelation::evolvedMatrices(makeMatrix(fwd))); };
             }},
        });

    }

}
