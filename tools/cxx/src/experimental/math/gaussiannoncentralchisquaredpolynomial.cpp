// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/gaussiannoncentralchisquaredpolynomial.hpp, .cpp: the non-central chi-squared polynomial, its
// recurrence computed by a multi-precision oracle (the S-xrt plan's XRR8). QuantLib's MomentBasedGaussianPolynomial
// template runs at boost::multiprecision's cpp_bin_float_50 on the moments from the cumulants 2^(j−1)(j−1)!(ν + jλ) in
// that type, ν and λ the doubles they are; its alpha and beta round once to double, its weight is Boost's density, as
// GaussNonCentralChiSquaredPolynomial's. QuantLib's own double polynomial, whose ill-conditioned double recursion
// nanoql doesn't port, isn't the oracle.
//
// "GaussNonCentralChiSquaredPolynomial::values" takes (nu, lambda, n) and returns {"alpha", "beta", "x", "weights"}:
// the first n recurrence coefficients and the n-point rule's nodes and weights.

// MSVC reports Boost.Math's forceinline functions it doesn't inline (C4714), as the harness's Boost sources do.
#pragma warning(disable : 4714)

#include "convert.hpp"

#include <ql/math/integrals/gaussianquadratures.hpp>
#include <ql/math/integrals/momentbasedgaussianpolynomial.hpp>

#include <boost/math/distributions/non_central_chi_squared.hpp>
#include <boost/multiprecision/cpp_bin_float.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using mp_real = boost::multiprecision::cpp_bin_float_50;

        class ExactNonCentralChiSquaredPolynomial : public MomentBasedGaussianPolynomial<mp_real> {
          public:
            ExactNonCentralChiSquaredPolynomial(Real nu, Real lambda) : nu_(nu), lambda_(lambda) {}

            Real w(Real x) const override {
                return boost::math::pdf(boost::math::non_central_chi_squared_distribution<Real>(nu_, lambda_), x);
            }

            mp_real moment(Size n) const override {
                while (m_.size() <= n) {
                    const Size k = m_.size();
                    if (k == 0) {
                        m_.emplace_back(1);
                        continue;
                    }
                    // m_k = sum_j C(k-1, j-1) kappa_j m_{k-j}, kappa_j = 2^(j-1)(j-1)!(nu + j lambda)
                    mp_real sum = 0, binomial = 1, factor = 1;
                    for (Size j = 1; j <= k; ++j) {
                        if (j > 1) {
                            binomial = binomial * mp_real(k - j + 1) / mp_real(j - 1);
                            factor *= mp_real(2 * (j - 1));
                        }
                        mp_real kappa = factor * (mp_real(nu_) + mp_real(j) * mp_real(lambda_));
                        sum += binomial * kappa * m_[k - j];
                    }
                    m_.push_back(sum);
                }
                return m_[n];
            }

          private:
            const Real nu_, lambda_;
            mutable std::vector<mp_real> m_;
        };

        const Register unit({
            {"GaussNonCentralChiSquaredPolynomial::values",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real nu = real(a[0]), lambda = real(a[1]);
                 Size n = static_cast<Size>(integer(a[2]));
                 return [=] {
                     ExactNonCentralChiSquaredPolynomial p(nu, lambda);
                     json::array alpha, beta, x, weights;
                     for (Size i = 0; i < n; ++i) {
                         alpha.emplace_back(p.alpha(i));
                         beta.emplace_back(p.beta(i));
                     }
                     GaussianQuadrature rule(n, p);
                     for (Real v : rule.x())
                         x.emplace_back(v);
                     for (Real v : rule.weights())
                         weights.emplace_back(v);
                     json::object out;
                     out["alpha"] = alpha;
                     out["beta"] = beta;
                     out["x"] = x;
                     out["weights"] = weights;
                     return json::value(out);
                 };
             }},
        });

    }

}
