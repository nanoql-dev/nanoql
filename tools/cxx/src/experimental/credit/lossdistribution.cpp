// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/lossdistribution.hpp, .cpp, which the wheel doesn't expose:
//
//     LossDist::binomialProbabilityOfNEvents        (n, p)
//     LossDist::binomialProbabilityOfAtLeastNEvents (n, p)
//     LossDist::probabilityOfNEvents                (p): every count's probability
//     LossDist::probabilityOfNEventsAt              (k, p)
//     LossDist::probabilityOfAtLeastNEvents         (k, p)
//     ProbabilityOfNEvents, ProbabilityOfAtLeastNEvents, BinomialProbabilityOfAtLeastNEvents (n, p): the functors
//     LossDistBinomial::limits        (nBuckets, maximum): [buckets, maximum] (its evaluation reads a member it never sets)
//     LossDistHomogeneous::evaluate   (nBuckets, maximum, volume, p): [summary, size, volume, probability,
//                                      excessProbability, buckets, maximum]
//     LossDistHomogeneous::onVectors  (nBuckets, maximum, volumes, p): the summary, on the first volume
//     LossDistBucketing::evaluate     (nBuckets, maximum, epsilon, volumes, p): [summary, buckets, maximum]
//     LossDistMonteCarlo::evaluate    (nBuckets, maximum, simulations, seed, epsilon, volumes, p): [summary, buckets,
//                                      maximum]
//
// each summary distributionspec.hpp's.

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/distributionspec.hpp"

#include <ql/experimental/credit/lossdistribution.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value arr(const std::vector<Real>& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return json::value(std::move(out));
        }

        template <class F>
        Method counted(F f) {
            return [=](const Args& a) -> Call {
                arity(a, 2);
                int n = integer(a[0]);
                std::vector<Real> p = reals(a[1]);
                return [=] {
                    std::vector<Real> q = p;
                    return json::value(f(n, q));
                };
            };
        }

        json::value withLimits(json::value summary, const LossDist& l) {
            json::array out;
            out.push_back(std::move(summary));
            out.emplace_back(static_cast<std::int64_t>(l.buckets()));
            out.emplace_back(l.maximum());
            return json::value(std::move(out));
        }

        const Register unit({
            {"LossDist::binomialProbabilityOfNEvents",
             counted([](int n, std::vector<Real>& p) { return LossDist::binomialProbabilityOfNEvents(n, p); })},
            {"LossDist::binomialProbabilityOfAtLeastNEvents",
             counted([](int n, std::vector<Real>& p) { return LossDist::binomialProbabilityOfAtLeastNEvents(n, p); })},
            {"LossDist::probabilityOfNEventsAt",
             counted([](int n, std::vector<Real>& p) { return LossDist::probabilityOfNEvents(n, p); })},
            {"LossDist::probabilityOfAtLeastNEvents",
             counted([](int n, std::vector<Real>& p) { return LossDist::probabilityOfAtLeastNEvents(n, p); })},
            {"ProbabilityOfNEvents", counted([](int n, std::vector<Real>& p) { return ProbabilityOfNEvents(n)(p); })},
            {"ProbabilityOfAtLeastNEvents",
             counted([](int n, std::vector<Real>& p) { return ProbabilityOfAtLeastNEvents(n)(p); })},
            {"BinomialProbabilityOfAtLeastNEvents",
             counted([](int n, std::vector<Real>& p) { return BinomialProbabilityOfAtLeastNEvents(n)(p); })},
            {"LossDist::probabilityOfNEvents",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Real> p = reals(a[0]);
                 return [=] {
                     std::vector<Real> q = p;
                     return arr(LossDist::probabilityOfNEvents(q));
                 };
             }},
            {"LossDistBinomial::limits",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 auto nBuckets = static_cast<Size>(integer(a[0]));
                 Real maximum = real(a[1]);
                 return [=] {
                     LossDistBinomial l(nBuckets, maximum);
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(l.buckets()));
                     out.emplace_back(l.maximum());
                     return json::value(std::move(out));
                 };
             }},
            {"LossDistHomogeneous::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto nBuckets = static_cast<Size>(integer(a[0]));
                 Real maximum = real(a[1]), volume = real(a[2]);
                 std::vector<Real> p = reals(a[3]);
                 return [=] {
                     LossDistHomogeneous l(nBuckets, maximum);
                     Distribution d = l(volume, p);
                     json::array out;
                     out.push_back(credit::summary(d));
                     out.emplace_back(static_cast<std::int64_t>(l.size()));
                     out.emplace_back(l.volume());
                     out.push_back(arr(l.probability()));
                     out.push_back(arr(l.excessProbability()));
                     out.emplace_back(static_cast<std::int64_t>(l.buckets()));
                     out.emplace_back(l.maximum());
                     return json::value(std::move(out));
                 };
             }},
            {"LossDistHomogeneous::onVectors",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto nBuckets = static_cast<Size>(integer(a[0]));
                 Real maximum = real(a[1]);
                 std::vector<Real> volumes = reals(a[2]), p = reals(a[3]);
                 return [=] {
                     LossDistHomogeneous l(nBuckets, maximum);
                     Distribution d = l(volumes, p);
                     return credit::summary(d);
                 };
             }},
            {"LossDistBucketing::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 auto nBuckets = static_cast<Size>(integer(a[0]));
                 Real maximum = real(a[1]), epsilon = real(a[2]);
                 std::vector<Real> volumes = reals(a[3]), p = reals(a[4]);
                 return [=] {
                     LossDistBucketing l(nBuckets, maximum, epsilon);
                     Distribution d = l(volumes, p);
                     return withLimits(credit::summary(d), l);
                 };
             }},
            {"LossDistMonteCarlo::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 auto nBuckets = static_cast<Size>(integer(a[0]));
                 Real maximum = real(a[1]);
                 auto simulations = static_cast<Size>(integer(a[2]));
                 auto seed = static_cast<long>(int64(a[3]));
                 Real epsilon = real(a[4]);
                 std::vector<Real> volumes = reals(a[5]), p = reals(a[6]);
                 return [=] {
                     LossDistMonteCarlo l(nBuckets, maximum, simulations, seed, epsilon);
                     Distribution d = l(volumes, p);
                     return withLimits(credit::summary(d), l);
                 };
             }},
        });

    }

}
