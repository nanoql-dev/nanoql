// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/hybridhestonhullwhiteprocess.hpp, .cpp: the Heston-Hull-White hybrid process, which the wheel doesn't
// expose. Each method takes the process first, processes/hybridspec.hpp's spec.
//
// "HybridHestonHullWhiteProcess::summary" takes (process) and returns {"size", "factors", "initialValues", "eta"}.
// "HybridHestonHullWhiteProcess::drift" and "::diffusion" take (process, t, x) and return the drift and the diffusion,
// its rows flattened. "HybridHestonHullWhiteProcess::evolve" takes (process, t0, x0, dt, dw) and returns the state.
// "HybridHestonHullWhiteProcess::numeraire" takes (process, t, x) and returns the numeraire. A refusal is QuantLib's.

#include "convert.hpp"
#include "processes/hybridspec.hpp"

#include <ql/math/array.hpp>
#include <ql/math/matrix.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value values(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        Array array(const std::vector<Real>& x) {
            return Array(x.begin(), x.end());
        }

        const Register unit({
            {"HybridHestonHullWhiteProcess::summary",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 hybrid::ProcessSpec s = hybrid::process(a[0]);
                 return [=] {
                     auto p = hybrid::makeProcess(s);
                     json::object out;
                     out["size"] = p->size();
                     out["factors"] = p->factors();
                     out["initialValues"] = values(p->initialValues());
                     out["eta"] = p->eta();
                     return json::value(std::move(out));
                 };
             }},
            {"HybridHestonHullWhiteProcess::drift",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 hybrid::ProcessSpec s = hybrid::process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] { return values(hybrid::makeProcess(s)->drift(t, array(x))); };
             }},
            {"HybridHestonHullWhiteProcess::diffusion",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 hybrid::ProcessSpec s = hybrid::process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] {
                     Matrix m = hybrid::makeProcess(s)->diffusion(t, array(x));
                     json::array out;
                     for (Size i = 0; i < m.rows(); ++i)
                         for (Size j = 0; j < m.columns(); ++j)
                             out.emplace_back(m[i][j]);
                     return json::value(std::move(out));
                 };
             }},
            {"HybridHestonHullWhiteProcess::evolve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 hybrid::ProcessSpec s = hybrid::process(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Real dt = real(a[3]);
                 std::vector<Real> dw = reals(a[4]);
                 return [=] { return values(hybrid::makeProcess(s)->evolve(t0, array(x0), dt, array(dw))); };
             }},
            {"HybridHestonHullWhiteProcess::numeraire",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 hybrid::ProcessSpec s = hybrid::process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] { return json::value(hybrid::makeProcess(s)->numeraire(t, array(x))); };
             }},
        });

    }

}
