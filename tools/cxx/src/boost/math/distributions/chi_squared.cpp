// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/distributions/chi_squared.hpp for double under the default policy: the quantile, with its complement,
// which Boost's non-central chi-squared quantile takes its first guess from, and the pdf and cdf.
//
//     chi_squared::pdf                  (df, x): boost::math::pdf(chi_squared_distribution<double>(df), x)
//     chi_squared::cdf                  (df, x): boost::math::cdf(chi_squared_distribution<double>(df), x)
//     chi_squared::cdf_complement       (df, x): boost::math::cdf(complement(chi_squared_distribution<double>(df), x))
//     chi_squared::quantile             (df, p): boost::math::quantile(chi_squared_distribution<double>(df), p)
//     chi_squared::quantile_complement  (df, q): boost::math::quantile(complement(chi_squared_distribution<double>(df),
//                                                q))
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/distributions/chi_squared.hpp>

#include <functional>

using namespace QuantLib;

namespace harness {

    namespace {

        using chi2 = boost::math::chi_squared_distribution<Real>;

        Method two(std::function<Real(Real, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 2);
                Real df = real(a[0]), x = real(a[1]);
                return [=] { return json::value(f(df, x)); };
            };
        }

        const Register unit({
            {"chi_squared::pdf", two([](Real df, Real x) { return boost::math::pdf(chi2(df), x); })},
            {"chi_squared::cdf", two([](Real df, Real x) { return boost::math::cdf(chi2(df), x); })},
            {"chi_squared::cdf_complement",
             two([](Real df, Real x) { return boost::math::cdf(boost::math::complement(chi2(df), x)); })},
            {"chi_squared::quantile", two([](Real df, Real p) { return boost::math::quantile(chi2(df), p); })},
            {"chi_squared::quantile_complement",
             two([](Real df, Real q) { return boost::math::quantile(boost::math::complement(chi2(df), q)); })},
        });

    }

}
