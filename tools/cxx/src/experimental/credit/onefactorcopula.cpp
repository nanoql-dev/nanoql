// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/onefactorcopula.hpp and its Gaussian and Student t forms, which the wheel doesn't expose. A
// copula is experimental/credit/portfoliospec.hpp's:
//
//     OneFactorCopula::members (copula, ms, zs, ys, ps, probabilities): [density(m) for each m, cumulativeZ(z) for
//         each z, cumulativeY(y) for each y, inverseCumulativeY(p) for each p, conditionalProbability(prob, m) for each
//         probability and m, conditionalProbability(probabilities, m) for each m, integral(prob) for each probability,
//         and the Gaussian's testCumulativeY(y) for each y (null for the others)]
//     OneFactorCopula::checkMoments (copula, tolerance): checkMoments's result, or its refusal
//     OneFactorCopula::integral (copula, nBuckets, maximum, nominals, probabilities): the loss distribution the
//         bucketing gives, integrated over the factor (experimental/credit/distributionspec.hpp's summary)
//     OneFactorCopula::integralOfSum (copula, probabilities): integral(f, probabilities) with f the sum of the
//         conditional probabilities

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/distributionspec.hpp"
#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/lossdistribution.hpp>
#include <ql/experimental/credit/onefactorgaussiancopula.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // integral(f, probabilities)'s functor: the sum of the conditional probabilities
        struct Sum {
            Real operator()(const std::vector<Real>& p) const {
                Real s = 0.0;
                for (Real x : p)
                    s += x;
                return s;
            }
        };

        const Register unit({
            {"OneFactorCopula::members",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 json::value spec = a[0];
                 std::vector<Real> ms = reals(a[1]), zs = reals(a[2]), ys = reals(a[3]), ps = reals(a[4]),
                                   probs = reals(a[5]);
                 return [=] {
                     ext::shared_ptr<OneFactorCopula> c = credit::portfolio::makeOneFactorCopula(spec);
                     json::array density, cumZ, cumY, inv, cond, condV, integ, test;
                     for (Real m : ms)
                         density.emplace_back(c->density(m));
                     for (Real z : zs)
                         cumZ.emplace_back(c->cumulativeZ(z));
                     for (Real y : ys)
                         cumY.emplace_back(c->cumulativeY(y));
                     for (Real p : ps)
                         inv.emplace_back(c->inverseCumulativeY(p));
                     for (Real p : probs)
                         for (Real m : ms)
                             cond.emplace_back(c->conditionalProbability(p, m));
                     for (Real m : ms)
                         condV.push_back(credit::portfolio::array(c->conditionalProbability(probs, m)));
                     for (Real p : probs)
                         integ.emplace_back(c->integral(p));
                     auto g = ext::dynamic_pointer_cast<OneFactorGaussianCopula>(c);
                     for (Real y : ys)
                         test.emplace_back(g != nullptr ? json::value(g->testCumulativeY(y)) : json::value(nullptr));
                     json::array out;
                     for (auto* x : {&density, &cumZ, &cumY, &inv, &cond, &condV, &integ, &test})
                         out.emplace_back(std::move(*x));
                     return json::value(std::move(out));
                 };
             }},
            {"OneFactorCopula::checkMoments",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value spec = a[0];
                 Real tolerance = real(a[1]);
                 return [=] {
                     return json::value(static_cast<std::int64_t>(credit::portfolio::makeOneFactorCopula(spec)->checkMoments(tolerance)));
                 };
             }},
            {"OneFactorCopula::integral",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value spec = a[0];
                 auto nBuckets = static_cast<Size>(integer(a[1]));
                 Real maximum = real(a[2]);
                 std::vector<Real> nominals = reals(a[3]), probs = reals(a[4]);
                 return [=] {
                     LossDistBucketing op(nBuckets, maximum);
                     std::vector<Real> n = nominals, p = probs;
                     Distribution d = credit::portfolio::makeOneFactorCopula(spec)->integral(op, n, p);
                     return credit::summary(d);
                 };
             }},
            {"OneFactorCopula::integralOfSum",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value spec = a[0];
                 std::vector<Real> probs = reals(a[1]);
                 return [=] {
                     std::vector<Real> p = probs;
                     return json::value(credit::portfolio::makeOneFactorCopula(spec)->integral(Sum(), p));
                 };
             }},
        });

    }

}
