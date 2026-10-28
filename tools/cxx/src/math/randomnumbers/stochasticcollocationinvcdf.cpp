// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/stochasticcollocationinvcdf.hpp, which the wheel doesn't bind: a collocation of an inverse
// cumulative function, value(x) at standard normal deviates and operator()(u) at probabilities. The inverse cumulative
// is the spec {"nonCentralChiSquared": [k, lambda]}, Boost's non-central chi-squared quantile as QuantLib's test takes
// it, or {"exponential": lambda}, -log(1 - u)/lambda; pMax and pMin are reals or null (Null<Real>()).
//
//     StochasticCollocationInvCDF::evaluate  (spec, order, pMax, pMin, xs, us): {"value": [...], "operator()": [...]}

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <ql/math/randomnumbers/stochasticcollocationinvcdf.hpp>

#include <boost/math/distributions/non_central_chi_squared.hpp>

#include <cmath>
#include <cstdint>
#include <functional>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> inverse(const json::value& v) {
            if (!v.is_object() || v.get_object().size() != 1)
                throw InvalidParams("expected an inverse cumulative spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            if (const json::value* chi = o.if_contains("nonCentralChiSquared")) {
                std::vector<Real> p = reals(*chi);
                if (p.size() != 2)
                    throw InvalidParams("a non-central chi-squared spec takes [k, lambda]");
                boost::math::non_central_chi_squared_distribution<Real> d(p[0], p[1]);
                return [d](Real u) { return boost::math::quantile(d, u); };
            }
            if (const json::value* e = o.if_contains("exponential")) {
                Real lambda = real(*e);
                return [lambda](Real u) { return -std::log(1.0 - u) / lambda; };
            }
            throw InvalidParams("expected nonCentralChiSquared or exponential, got " + json::serialize(v));
        }

        const Register unit({
            {"StochasticCollocationInvCDF::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 json::value spec = a[0];
                 inverse(spec);
                 Integer order = integer(a[1]);
                 Real pMax = real(a[2]), pMin = real(a[3]);
                 std::vector<Real> xs = reals(a[4]), us = reals(a[5]);
                 return [=] {
                     StochasticCollocationInvCDF c(inverse(spec), static_cast<Size>(order), pMax, pMin);
                     json::array values, quantiles;
                     for (Real x : xs)
                         values.emplace_back(c.value(x));
                     for (Real u : us)
                         quantiles.emplace_back(c(u));
                     json::object out;
                     out["value"] = std::move(values);
                     out["operator()"] = std::move(quantiles);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
