// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/mfstateprocess.hpp, .cpp: MfStateProcess, the Markov-functional model's state process, which the wheel
// doesn't expose, built inside the call over the spec {"reversion", "times", "vols"} so that its constructor's refusal
// is QuantLib's.
//
//     MfStateProcess::x0            (spec)
//     MfStateProcess::drift         (spec, t, x)
//     MfStateProcess::diffusion     (spec, t, x)
//     MfStateProcess::expectation   (spec, t0, x0, dt)
//     MfStateProcess::stdDeviation  (spec, t0, x0, dt)
//     MfStateProcess::variance      (spec, t0, x0, dt)
//     MfStateProcess::evolve        (spec, t0, x0, dt, dw): StochasticProcess1D's default, from the expectation and
//                                   the standard deviation

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/processes/mfstateprocess.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct MfSpec {
            Real reversion = 0.0;
            std::vector<Real> times, vols;
        };

        MfSpec spec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a state process spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            if (o.size() != 3 || !o.contains("reversion") || !o.contains("times") || !o.contains("vols"))
                throw InvalidParams("a state process spec is {reversion, times, vols}: " + json::serialize(v));
            return {real(o.at("reversion")), reals(o.at("times")), reals(o.at("vols"))};
        }

        MfStateProcess make(const MfSpec& s) {
            return {s.reversion, Array(s.times.begin(), s.times.end()), Array(s.vols.begin(), s.vols.end())};
        }

        // A member over an interval: (spec, t0, x0, dt).
        Call interval(const Args& a, Real (*f)(const MfStateProcess&, Real, Real, Real)) {
            arity(a, 4);
            MfSpec s = spec(a[0]);
            Real t0 = real(a[1]), x0 = real(a[2]), dt = real(a[3]);
            return [=] { return json::value(f(make(s), t0, x0, dt)); };
        }

        // A member at a point: (spec, t, x).
        Call point(const Args& a, Real (*f)(const MfStateProcess&, Real, Real)) {
            arity(a, 3);
            MfSpec s = spec(a[0]);
            Real t = real(a[1]), x = real(a[2]);
            return [=] { return json::value(f(make(s), t, x)); };
        }

        const Register unit({
            {"MfStateProcess::x0",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 MfSpec s = spec(a[0]);
                 return [=] { return json::value(make(s).x0()); };
             }},
            {"MfStateProcess::drift",
             [](const Args& a) {
                 return point(a, [](const MfStateProcess& p, Real t, Real x) { return p.drift(t, x); });
             }},
            {"MfStateProcess::diffusion",
             [](const Args& a) {
                 return point(a, [](const MfStateProcess& p, Real t, Real x) { return p.diffusion(t, x); });
             }},
            {"MfStateProcess::expectation",
             [](const Args& a) {
                 return interval(a, [](const MfStateProcess& p, Real t0, Real x0, Real dt) {
                     return p.expectation(t0, x0, dt);
                 });
             }},
            {"MfStateProcess::stdDeviation",
             [](const Args& a) {
                 return interval(a, [](const MfStateProcess& p, Real t0, Real x0, Real dt) {
                     return p.stdDeviation(t0, x0, dt);
                 });
             }},
            {"MfStateProcess::variance",
             [](const Args& a) {
                 return interval(a, [](const MfStateProcess& p, Real t0, Real x0, Real dt) {
                     return p.variance(t0, x0, dt);
                 });
             }},
            {"MfStateProcess::evolve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 MfSpec s = spec(a[0]);
                 Real t0 = real(a[1]), x0 = real(a[2]), dt = real(a[3]), dw = real(a[4]);
                 return [=] { return json::value(make(s).evolve(t0, x0, dt, dw)); };
             }},
        });

    }

}
