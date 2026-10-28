// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/distribution.hpp, .cpp, which the wheel doesn't expose. A distribution spec is {"nBuckets",
// "xmin", "xmax", "values" (added), "densities" ([[bucket, density]...], added), "averages" ([[bucket, value]...],
// added)}, built inside the call:
//
//     Distribution::summary          (spec): distributionspec.hpp's summary
//     Distribution::expectedValue    (spec)
//     Distribution::expectedSquare   (spec): the template expectedValue(f) on f(x) = x * x
//     Distribution::confidenceLevel  (spec, level)
//     Distribution::cumulativeDensity (spec, x)
//     Distribution::trancheExpectedValue (spec, a, d)
//     Distribution::cumulativeExcessProbability (spec, a, b)
//     Distribution::locate           (spec, x)
//     Distribution::dx               (spec, x)
//     Distribution::tranche          (spec, a, d): after the tranche, [size, x, cumulative, the densities and excess
//                                     probabilities but the last]
//     ManipulateDistribution::convolve (spec, spec): the convolution's summary

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/distributionspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        Distribution build(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a distribution spec");
            const json::object& o = v.get_object();
            auto at = [&](const char* k) -> const json::value& {
                const json::value* x = o.if_contains(k);
                if (x == nullptr)
                    throw InvalidParams(std::string("a distribution spec needs ") + k);
                return *x;
            };
            Distribution d(integer(at("nBuckets")), real(at("xmin")), real(at("xmax")));
            for (Real x : reals(at("values")))
                d.add(x);
            for (const json::value& p : at("densities").get_array()) {
                std::vector<Real> bv = reals(p);
                d.addDensity(static_cast<int>(bv[0]), bv[1]);
            }
            for (const json::value& p : at("averages").get_array()) {
                std::vector<Real> bv = reals(p);
                d.addAverage(static_cast<int>(bv[0]), bv[1]);
            }
            return d;
        }

        template <class F>
        Method on(std::size_t n, F f) {
            return [=](const Args& a) -> Call {
                arity(a, n);
                json::value spec = a[0];
                std::vector<Real> rest;
                for (std::size_t i = 1; i < n; i++)
                    rest.push_back(real(a[i]));
                return [=] {
                    Distribution d = build(spec);
                    return f(d, rest);
                };
            };
        }

        const Register unit({
            {"Distribution::summary", on(1, [](Distribution& d, const std::vector<Real>&) { return credit::summary(d); })},
            {"Distribution::expectedValue",
             on(1, [](Distribution& d, const std::vector<Real>&) { return json::value(d.expectedValue()); })},
            {"Distribution::expectedSquare",
             on(1, [](Distribution& d, const std::vector<Real>&) {
                 auto square = [](Real x) { return x * x; };
                 return json::value(d.expectedValue(square));
             })},
            {"ManipulateDistribution::convolve",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value s1 = a[0], s2 = a[1];
                 return [=] {
                     Distribution d = ManipulateDistribution::convolve(build(s1), build(s2));
                     return credit::summary(d);
                 };
             }},
            {"Distribution::confidenceLevel",
             on(2, [](Distribution& d, const std::vector<Real>& r) { return json::value(d.confidenceLevel(r[0])); })},
            {"Distribution::cumulativeDensity",
             on(2, [](Distribution& d, const std::vector<Real>& r) { return json::value(d.cumulativeDensity(r[0])); })},
            {"Distribution::trancheExpectedValue",
             on(3, [](Distribution& d, const std::vector<Real>& r) {
                 return json::value(d.trancheExpectedValue(r[0], r[1]));
             })},
            {"Distribution::cumulativeExcessProbability",
             on(3, [](Distribution& d, const std::vector<Real>& r) {
                 return json::value(d.cumulativeExcessProbability(r[0], r[1]));
             })},
            {"Distribution::locate",
             on(2, [](Distribution& d, const std::vector<Real>& r) {
                 return json::value(static_cast<std::int64_t>(d.locate(r[0])));
             })},
            {"Distribution::dx", on(2, [](Distribution& d, const std::vector<Real>& r) { return json::value(d.dx(r[0])); })},
            {"Distribution::tranche",
             on(3, [](Distribution& d, const std::vector<Real>& r) {
                 d.tranche(r[0], r[1]);
                 json::array x, cumulative, density, excess;
                 for (Size k = 0; k < d.size(); k++) {
                     x.emplace_back(d.x(k));
                     cumulative.emplace_back(d.cumulative(k));
                 }
                 for (Size k = 0; k + 1 < d.size(); k++) {
                     density.emplace_back(d.density(k));
                     excess.emplace_back(d.excess(k));
                 }
                 json::array out;
                 out.emplace_back(static_cast<std::int64_t>(d.size()));
                 for (auto* a : {&x, &cumulative, &density, &excess})
                     out.emplace_back(std::move(*a));
                 return json::value(std::move(out));
             })},
        });

    }

}
