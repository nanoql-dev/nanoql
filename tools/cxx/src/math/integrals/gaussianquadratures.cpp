// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/gaussianquadratures.hpp, .cpp and gaussianorthogonalpolynomial.hpp, .cpp, the members the wheel
// doesn't expose: the integrators over [a, b] (GaussLegendreIntegrator, GaussChebyshevIntegrator,
// GaussChebyshev2ndIntegrator), their value, count and quadrature's order; and the orthogonal polynomials' members
// (mu_0, alpha, beta, w, logW, value, weightedValue), one a call, so that each refusal is its member's; and the
// inspectors no QuantLib test reads: TabulatedGaussLegendre's order (built at n, then set to another order, so that a
// refusal is the constructor's or order(Size)'s) and MultiDimGaussianIntegration's weights and x (over Gauss-Hermite
// or Gauss-Legendre quadratures of each dimension's order). The integrand is named, as
// tools/vectors/gen/math/integrals/simpsonintegral.py names it.

#include "convert.hpp"

#include <ql/math/integrals/gaussianorthogonalpolynomial.hpp>
#include <ql/math/integrals/gaussianquadratures.hpp>

#include <cmath>
#include <functional>
#include <memory>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> integrand(const std::string& name) {
            if (name == "exp(x)")
                return [](Real x) { return std::exp(x); };
            if (name == "sin(x)")
                return [](Real x) { return std::sin(x); };
            if (name == "x x")
                return [](Real x) { return x * x; };
            if (name == "x x x x")
                return [](Real x) { return x * x * x * x; };
            if (name == "1/(1+x x)")
                return [](Real x) { return 1.0 / (1.0 + x * x); };
            if (name == "sqrt(x)")
                return [](Real x) { return std::sqrt(x); };
            if (name == "exp(-x x/2)")
                return [](Real x) { return std::exp(-x * x / 2.0); };
            if (name == "x")
                return [](Real x) { return x; };
            throw InvalidParams("unknown integrand " + name);
        }

        template <class I>
        json::value integrate(Size n, const std::function<Real(Real)>& f, Real lower, Real upper) {
            I integrator(n);
            Real value = integrator(f, lower, upper);
            return json::value(json::array{value, static_cast<std::int64_t>(integrator.numberOfEvaluations()),
                                           static_cast<std::int64_t>(integrator.getIntegration()->order())});
        }

        std::unique_ptr<GaussianOrthogonalPolynomial> polynomial(const std::string& kind, const std::vector<Real>& p) {
            if (kind == "Laguerre")
                return std::make_unique<GaussLaguerrePolynomial>(p.at(0));
            if (kind == "Hermite")
                return std::make_unique<GaussHermitePolynomial>(p.at(0));
            if (kind == "Jacobi")
                return std::make_unique<GaussJacobiPolynomial>(p.at(0), p.at(1));
            if (kind == "Legendre")
                return std::make_unique<GaussLegendrePolynomial>();
            if (kind == "Chebyshev")
                return std::make_unique<GaussChebyshevPolynomial>();
            if (kind == "Chebyshev2nd")
                return std::make_unique<GaussChebyshev2ndPolynomial>();
            if (kind == "Gegenbauer")
                return std::make_unique<GaussGegenbauerPolynomial>(p.at(0));
            if (kind == "Hyperbolic")
                return std::make_unique<GaussHyperbolicPolynomial>();
            throw InvalidParams("unknown polynomial " + kind);
        }

        const Register unit({
            {"TabulatedGaussLegendre::order",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Integer n = integer(a[0]);
                 Real other = real(a[1]);
                 if (n < 0 || (other != Null<Real>() && (other < 0 || other != std::floor(other))))
                     throw InvalidParams("orders must be non-negative integers");
                 return [=] {
                     TabulatedGaussLegendre quadrature(static_cast<Size>(n));
                     if (other != Null<Real>())
                         quadrature.order(static_cast<Size>(other));
                     return json::value(static_cast<std::int64_t>(quadrature.order()));
                 };
             }},
            {"MultiDimGaussianIntegration",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::string kind = text(a[0]);
                 std::vector<Integer> ns = integers(a[1]);
                 if (kind != "Hermite" && kind != "Legendre")
                     throw InvalidParams("unknown quadrature " + kind);
                 std::vector<Size> orders;
                 for (Integer n : ns) {
                     if (n < 0)
                         throw InvalidParams("orders must be non-negative");
                     orders.push_back(static_cast<Size>(n));
                 }
                 return [=] {
                     MultiDimGaussianIntegration integration(
                         orders, [kind](Size n) -> ext::shared_ptr<GaussianQuadrature> {
                             if (kind == "Hermite")
                                 return ext::make_shared<GaussHermiteIntegration>(n);
                             return ext::make_shared<GaussLegendreIntegration>(n);
                         });
                     json::array weights, x;
                     for (Real w : integration.weights())
                         weights.emplace_back(w);
                     for (const Array& point : integration.x()) {
                         json::array p;
                         for (Real y : point)
                             p.emplace_back(y);
                         x.emplace_back(p);
                     }
                     return json::value(json::object{{"weights", weights}, {"x", x}});
                 };
             }},
            {"GaussianQuadratureIntegrator",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string kind = text(a[0]);
                 Integer n = integer(a[1]);
                 if (n < 0)
                     throw InvalidParams("n must be non-negative");
                 std::function<Real(Real)> f = integrand(text(a[2]));
                 Real lower = real(a[3]), upper = real(a[4]);
                 return [=] {
                     Size order = static_cast<Size>(n);
                     if (kind == "Legendre")
                         return integrate<GaussLegendreIntegrator>(order, f, lower, upper);
                     if (kind == "Chebyshev")
                         return integrate<GaussChebyshevIntegrator>(order, f, lower, upper);
                     if (kind == "Chebyshev2nd")
                         return integrate<GaussChebyshev2ndIntegrator>(order, f, lower, upper);
                     throw InvalidParams("unknown integrator " + kind);
                 };
             }},
            {"GaussianOrthogonalPolynomial",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string kind = text(a[0]);
                 std::vector<Real> p = reals(a[1]);
                 std::string member = text(a[2]);
                 Integer n = integer(a[3]);
                 if (n < 0)
                     throw InvalidParams("n must be non-negative");
                 Real x = real(a[4]);
                 if (member != "mu_0" && member != "alpha" && member != "beta" && member != "w" && member != "logW" &&
                     member != "value" && member != "weightedValue")
                     throw InvalidParams("unknown member " + member);
                 return [=] {
                     std::unique_ptr<GaussianOrthogonalPolynomial> P = polynomial(kind, p);
                     Size i = static_cast<Size>(n);
                     if (member == "mu_0")
                         return json::value(P->mu_0());
                     if (member == "alpha")
                         return json::value(P->alpha(i));
                     if (member == "beta")
                         return json::value(P->beta(i));
                     if (member == "w")
                         return json::value(P->w(x));
                     if (member == "logW")
                         return json::value(P->logW(x));
                     if (member == "value")
                         return json::value(P->value(i, x));
                     return json::value(P->weightedValue(i, x));
                 };
             }},
        });

    }

}
