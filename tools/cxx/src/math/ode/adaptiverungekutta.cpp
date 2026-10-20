// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/ode/adaptiverungekutta.hpp: AdaptiveRungeKutta<std::complex<Real>>, which the wheel doesn't instantiate
// (its RungeKutta is the real one), built inside each call from its settings, over one of the harness's named complex
// equations f'(x) = F(x, f(x)):
//   rotation        1-D   F = i y                      (test-suite/ode.cpp's ode2)
//   dampedRotation  1-D   F = (-0.5 + 2 i) y
//   chirp           1-D   F = i x y
//   decay           1-D   F = -y + (0.25 + 0.5 i), the constant added
//   oscillator      2-D   F = (y1, -y0)                (test-suite/ode.cpp's ode4)
//   coupled         2-D   F = (i y1, i y0 - 0.1 y1)
// each written as test-suite/ode.cpp writes its functors, on std::complex<Real>'s operators.
//
//     AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct1d)   (eps, h1, hmin, name, y1, x1, x2)
//     AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct)     (eps, h1, hmin, name, y1, x1, x2)
//
// A complex value is [real, imaginary]; the vector form's y1 and result are lists of them. The harness refuses an
// initial value of another dimension than its equation's.

#include "convert.hpp"

#include <ql/math/ode/adaptiverungekutta.hpp>

#include <complex>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        typedef std::complex<Real> Complex;

        Complex complexValue(const json::value& v) {
            std::vector<Real> c = reals(v);
            if (c.size() != 2)
                throw InvalidParams("expected a complex value [real, imaginary], got " + json::serialize(v));
            return {c[0], c[1]};
        }

        json::value complexResult(const Complex& z) { return json::value(json::array{z.real(), z.imag()}); }

        Size dimension(const std::string& name) {
            if (name == "rotation" || name == "dampedRotation" || name == "chirp" || name == "decay")
                return 1;
            if (name == "oscillator" || name == "coupled")
                return 2;
            throw InvalidParams("unknown complex equation " + name);
        }

        Complex scalarEquation(const std::string& name, Real x, const Complex& y) {
            if (name == "rotation")
                return Complex(0.0, 1.0) * y;
            if (name == "dampedRotation")
                return Complex(-0.5, 2.0) * y;
            if (name == "chirp")
                return Complex(0.0, x) * y;
            return -y + Complex(0.25, 0.5);
        }

        std::vector<Complex> vectorEquation(const std::string& name, Real x, const std::vector<Complex>& y) {
            std::vector<Complex> r(2);
            if (name == "oscillator") {
                r[0] = y[1];
                r[1] = -y[0];
            } else if (name == "coupled") {
                r[0] = Complex(0.0, 1.0) * y[1];
                r[1] = Complex(0.0, 1.0) * y[0] - 0.1 * y[1];
            } else {
                // a 1-D equation in the vector form
                r.resize(1);
                r[0] = scalarEquation(name, x, y[0]);
            }
            return r;
        }

        struct Settings {
            Real eps, h1, hmin;
            std::string name;
            Real x1, x2;
        };

        Settings settings(const Args& a) {
            return {real(a[0]), real(a[1]), real(a[2]), text(a[3]), real(a[5]), real(a[6])};
        }

        const Register unit({
            {"AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct1d)",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Settings s = settings(a);
                 if (dimension(s.name) != 1)
                     throw InvalidParams(s.name + " isn't a 1-D equation");
                 Complex y1 = complexValue(a[4]);
                 return [=] {
                     AdaptiveRungeKutta<Complex> rk(s.eps, s.h1, s.hmin);
                     AdaptiveRungeKutta<Complex>::OdeFct1d f = [&](Real x, const Complex& y) {
                         return scalarEquation(s.name, x, y);
                     };
                     return complexResult(rk(f, y1, s.x1, s.x2));
                 };
             }},
            {"AdaptiveRungeKutta<std::complex<Real>>::operator()(OdeFct)",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Settings s = settings(a);
                 if (!a[4].is_array())
                     throw InvalidParams("expected a list of complex values, got " + json::serialize(a[4]));
                 std::vector<Complex> y1;
                 for (const json::value& c : a[4].get_array())
                     y1.push_back(complexValue(c));
                 if (y1.size() != dimension(s.name))
                     throw InvalidParams(s.name + " is an equation of another dimension");
                 return [=] {
                     AdaptiveRungeKutta<Complex> rk(s.eps, s.h1, s.hmin);
                     AdaptiveRungeKutta<Complex>::OdeFct f = [&](Real x, const std::vector<Complex>& y) {
                         return vectorEquation(s.name, x, y);
                     };
                     std::vector<Complex> y = rk(f, y1, s.x1, s.x2);
                     json::array out;
                     for (const Complex& z : y)
                         out.push_back(complexResult(z));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
