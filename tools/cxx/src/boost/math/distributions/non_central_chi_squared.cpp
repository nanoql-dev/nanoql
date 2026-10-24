// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/distributions/non_central_chi_squared.hpp for double under the default policy, as QuantLib's CEV engine
// and density calculator call it, with both tails:
//
//     non_central_chi_squared::pdf                  (k, l, x): boost::math::pdf(nc_chi2(k, l), x)
//     non_central_chi_squared::cdf                  (k, l, x): boost::math::cdf(nc_chi2(k, l), x)
//     non_central_chi_squared::cdf_complement       (k, l, x): boost::math::cdf(complement(nc_chi2(k, l), x))
//     non_central_chi_squared::quantile             (k, l, p): boost::math::quantile(nc_chi2(k, l), p)
//     non_central_chi_squared::quantile_complement  (k, l, q): boost::math::quantile(complement(nc_chi2(k, l), q))
//
// nc_chi2 is boost::math::non_central_chi_squared_distribution<double>, whose constructor checks its parameters.
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/distributions/non_central_chi_squared.hpp>

#include <functional>

using namespace QuantLib;

namespace harness {

    namespace {

        using nc_chi2 = boost::math::non_central_chi_squared_distribution<Real>;

        Method three(std::function<Real(Real, Real, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 3);
                Real k = real(a[0]), l = real(a[1]), x = real(a[2]);
                return [=] { return json::value(f(k, l, x)); };
            };
        }

        const Register unit({
            {"non_central_chi_squared::pdf",
             three([](Real k, Real l, Real x) { return boost::math::pdf(nc_chi2(k, l), x); })},
            {"non_central_chi_squared::cdf",
             three([](Real k, Real l, Real x) { return boost::math::cdf(nc_chi2(k, l), x); })},
            {"non_central_chi_squared::cdf_complement",
             three([](Real k, Real l, Real x) { return boost::math::cdf(boost::math::complement(nc_chi2(k, l), x)); })},
            {"non_central_chi_squared::quantile",
             three([](Real k, Real l, Real p) { return boost::math::quantile(nc_chi2(k, l), p); })},
            {"non_central_chi_squared::quantile_complement",
             three([](Real k, Real l, Real q) {
                 return boost::math::quantile(boost::math::complement(nc_chi2(k, l), q));
             })},
        });

    }

}
