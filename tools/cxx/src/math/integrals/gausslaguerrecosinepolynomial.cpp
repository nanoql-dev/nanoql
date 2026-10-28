// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/gausslaguerrecosinepolynomial.hpp and momentbasedgaussianpolynomial.hpp, which the wheel doesn't
// bind: the Gauss-Laguerre cosine and sine polynomials' moments, recurrence coefficients and weight, and the Gaussian
// quadrature on them.
//
//     GaussLaguerreTrigonometricPolynomial  (kind ("Cosine" or "Sine"), u, moments, order, [x, ...]):
//                                           {"moment": [...], "alpha": [...], "beta": [...], "w": [...],
//                                            "nodes": [...], "weights": [...]}, the moments and the coefficients to
//                                           their counts, the rule's of the order

#include "convert.hpp"

#include <ql/math/integrals/gausslaguerrecosinepolynomial.hpp>
#include <ql/math/integrals/gaussianquadratures.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value report(const MomentBasedGaussianPolynomial<Real>& p, Size moments, Size order,
                           const std::vector<Real>& xs) {
            json::array moment, alpha, beta, w, nodes, weights;
            for (Size i = 0; i < moments; ++i) {
                moment.emplace_back(p.moment(i));
                alpha.emplace_back(p.alpha(i));
                beta.emplace_back(p.beta(i));
            }
            for (Real x : xs)
                w.emplace_back(p.w(x));
            GaussianQuadrature rule(order, p);
            for (Real x : rule.x())
                nodes.emplace_back(x);
            for (Real x : rule.weights())
                weights.emplace_back(x);
            json::object out;
            out["moment"] = std::move(moment);
            out["alpha"] = std::move(alpha);
            out["beta"] = std::move(beta);
            out["w"] = std::move(w);
            out["nodes"] = std::move(nodes);
            out["weights"] = std::move(weights);
            return json::value(std::move(out));
        }

        const Register unit({
            {"GaussLaguerreTrigonometricPolynomial",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string kind = text(a[0]);
                 if (kind != "Cosine" && kind != "Sine")
                     throw InvalidParams("expected Cosine or Sine, got " + kind);
                 Real u = real(a[1]);
                 Integer moments = integer(a[2]), order = integer(a[3]);
                 if (moments < 0 || order < 1)
                     throw InvalidParams("a count of moments is non-negative, an order positive");
                 std::vector<Real> xs = reals(a[4]);
                 return [=] {
                     if (kind == "Cosine")
                         return report(GaussLaguerreCosinePolynomial<Real>(u), static_cast<Size>(moments),
                                       static_cast<Size>(order), xs);
                     return report(GaussLaguerreSinePolynomial<Real>(u), static_cast<Size>(moments),
                                   static_cast<Size>(order), xs);
                 };
             }},
        });

    }

}
