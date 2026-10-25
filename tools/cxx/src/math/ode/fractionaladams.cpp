// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/ode/fractionaladams.hpp: the fractional Adams scheme over Real and std::complex<Real>, the reusable
// Riemann-Liouville weights and the product-trapezoidal Riemann-Liouville integral, which the wheel doesn't expose.
//
//     FractionalAdams::solve              (alpha, equation, y0, tMax, steps)
//     RiemannLiouvilleWeights::integrate  (alpha, steps, y, dt)
//     riemannLiouvilleIntegral            (y, alpha, dt)
//
// An equation is one of the harness's named right-hand sides f(t, y), each written as test-suite/roughhestonmodel.cpp
// writes its own: "power", D^a y = Gamma(5)/Gamma(5 - a) t^(4 - a) + t^4 - y (real); "linear", -y (real);
// "rotation", 1.5i y (complex); "riccati", c0 + (c1 + c2 y) y with the rough Heston engine's coefficients at
// z = 2 - 0.5i, kappa 0.3, sigma 0.4, rho -0.7 (complex). A complex value is [real, imaginary]; y0 and y are a real or
// a complex value, or a list of either; every result is a list of values, or a value, of the same kind.

#include "complexvalue.hpp"
#include "convert.hpp"

#include <ql/math/distributions/gammadistribution.hpp>
#include <ql/math/ode/fractionaladams.hpp>

#include <cmath>
#include <complex>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        typedef std::complex<Real> Complex;

        bool isComplex(const std::string& name) {
            if (name == "power" || name == "linear")
                return false;
            if (name == "rotation" || name == "riccati")
                return true;
            throw InvalidParams("unknown fractional equation " + name);
        }

        Real realEquation(const std::string& name, Real alpha, Real t, Real y) {
            if (name == "power") {
                const Real g = GammaFunction().value(5.0) / GammaFunction().value(5.0 - alpha);
                return g * std::pow(t, 4 - alpha) + t * t * t * t - y;
            }
            return -y;
        }

        Complex complexEquation(const std::string& name, const Complex& y) {
            if (name == "rotation")
                return Complex(0.0, 1.5) * y;
            // the rough Heston engine's riccatiCoefficients at z = 2 - 0.5i, kappa 0.3, sigma 0.4, rho -0.7
            const Complex z(2.0, -0.5), i(0.0, 1.0);
            const Real kappa = 0.3, sigma = 0.4, rho = -0.7;
            const Complex c0 = -0.5 * z * (z + i);
            const Complex c1 = i * (rho * sigma) * z - kappa;
            const Complex c2(0.5 * sigma * sigma);
            return c0 + (c1 + c2 * y) * y;
        }

        bool complexList(const json::value& v) {
            return v.is_array() && !v.get_array().empty() && v.get_array()[0].is_array();
        }

        const Register unit({
            {"FractionalAdams::solve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real alpha = real(a[0]);
                 std::string name = text(a[1]);
                 bool complex = isComplex(name);
                 json::value y0 = a[2];
                 Real tMax = real(a[3]);
                 Integer steps = integer(a[4]);
                 if (steps < 0)
                     throw InvalidParams("steps must be non-negative");
                 return [=] {
                     json::array out;
                     if (complex) {
                         std::vector<Complex> y = FractionalAdams<Complex>(alpha).solve(
                             [name](Real, const Complex& x) { return complexEquation(name, x); }, complexValue(y0),
                             tMax, static_cast<Size>(steps));
                         for (const Complex& z : y)
                             out.push_back(complexResult(z));
                     } else {
                         std::vector<Real> y = FractionalAdams<Real>(alpha).solve(
                             [name, alpha](Real t, const Real& x) { return realEquation(name, alpha, t, x); },
                             real(y0), tMax, static_cast<Size>(steps));
                         for (Real x : y)
                             out.push_back(x);
                     }
                     return json::value(out);
                 };
             }},
            {"RiemannLiouvilleWeights::integrate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real alpha = real(a[0]);
                 Integer steps = integer(a[1]);
                 if (steps < 0)
                     throw InvalidParams("steps must be non-negative");
                 json::value y = a[2];
                 Real dt = real(a[3]);
                 return [=] {
                     RiemannLiouvilleWeights w(alpha, static_cast<Size>(steps));
                     if (complexList(y))
                         return complexResult(w.integrate(complexValues(y), dt));
                     return json::value(w.integrate(reals(y), dt));
                 };
             }},
            {"riemannLiouvilleIntegral",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value y = a[0];
                 Real alpha = real(a[1]), dt = real(a[2]);
                 return [=] {
                     if (complexList(y))
                         return complexResult(riemannLiouvilleIntegral(complexValues(y), alpha, dt));
                     return json::value(riemannLiouvilleIntegral(reals(y), alpha, dt));
                 };
             }},
        });

    }

}
