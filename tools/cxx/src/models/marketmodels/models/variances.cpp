// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp, .cpp, piecewiseconstantvariance.hpp, .cpp and
// volatilityinterpolationspecifierabcd.hpp, .cpp, which the wheel doesn't expose.
//
// A variance spec is {"a", "b", "c", "d", "resetIndex", "rateTimes"}; a variance's reads are {"variances",
// "volatilities", "rateTimes", "variance", "volatility", "totalVariance", "totalVolatility"}, the last four each
// step's.
//
// "PiecewiseConstantAbcdVariance" takes (variance): its reads and "abcd" (getABCD's a, b, c and d).
// "VolatilityInterpolationSpecifierabcd" takes (period, offset, variances, timesForSmallRates, lastCapletVol, scales,
// newLastCapletVol): the specifier, then setScalingFactors(scales) unless they are null, then
// setLastCapletVol(newLastCapletVol) unless it is null; its "period", "offset", "noBigRates", "noSmallRates", and the
// reads of its "interpolatedVariances" and "originalVariances".

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp>
#include <ql/models/marketmodels/models/volatilityinterpolationspecifierabcd.hpp>

#include <cstdint>
#include <optional>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        struct VarianceSpec {
            Real a = 0.0, b = 0.0, c = 0.0, d = 0.0;
            Size resetIndex = 0;
            Vec rateTimes;
        };

        VarianceSpec variance(const json::value& v) {
            const std::string what = "a variance";
            const json::object& o = object(v, what);
            onlyKeys(o, {"a", "b", "c", "d", "resetIndex", "rateTimes"}, what);
            VarianceSpec s;
            s.a = real(key(o, "a", what));
            s.b = real(key(o, "b", what));
            s.c = real(key(o, "c", what));
            s.d = real(key(o, "d", what));
            s.resetIndex = size(key(o, "resetIndex", what));
            s.rateTimes = reals(key(o, "rateTimes", what));
            return s;
        }

        PiecewiseConstantAbcdVariance makeVariance(const VarianceSpec& s) {
            return PiecewiseConstantAbcdVariance(s.a, s.b, s.c, s.d, s.resetIndex, s.rateTimes);
        }

        json::value reads(const PiecewiseConstantVariance& v) {
            json::object out;
            out["variances"] = result(v.variances());
            out["volatilities"] = result(v.volatilities());
            out["rateTimes"] = result(v.rateTimes());
            json::array variance, volatility, totalVariance, totalVolatility;
            for (Size i = 0; i < v.variances().size(); ++i) {
                variance.emplace_back(v.variance(i));
                volatility.emplace_back(v.volatility(i));
                totalVariance.emplace_back(v.totalVariance(i));
                totalVolatility.emplace_back(v.totalVolatility(i));
            }
            out["variance"] = variance;
            out["volatility"] = volatility;
            out["totalVariance"] = totalVariance;
            out["totalVolatility"] = totalVolatility;
            return json::value(out);
        }

        json::value reads(const std::vector<ext::shared_ptr<PiecewiseConstantVariance> >& vs) {
            json::array out;
            for (const auto& v : vs)
                out.emplace_back(reads(*v));
            return json::value(out);
        }

        const Register unit({
            {"PiecewiseConstantAbcdVariance",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 VarianceSpec s = variance(a[0]);
                 return [=] {
                     PiecewiseConstantAbcdVariance v = makeVariance(s);
                     json::value out = reads(v);
                     Real va, vb, vc, vd;
                     v.getABCD(va, vb, vc, vd);
                     out.as_object()["abcd"] = result(Vec{va, vb, vc, vd});
                     return out;
                 };
             }},
            {"VolatilityInterpolationSpecifierabcd",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Size period = size(a[0]), offset = size(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("expected an array of variances, got " + json::serialize(a[2]));
                 std::vector<VarianceSpec> specs;
                 for (const json::value& v : a[2].get_array())
                     specs.push_back(variance(v));
                 Vec times = reals(a[3]);
                 Real lastCapletVol = real(a[4]);
                 std::optional<Vec> scales;
                 if (!a[5].is_null())
                     scales = reals(a[5]);
                 std::optional<Real> newLastCapletVol;
                 if (!a[6].is_null())
                     newLastCapletVol = real(a[6]);
                 return [=] {
                     std::vector<PiecewiseConstantAbcdVariance> variances;
                     for (const VarianceSpec& s : specs)
                         variances.push_back(makeVariance(s));
                     VolatilityInterpolationSpecifierabcd spec(period, offset, variances, times, lastCapletVol);
                     if (scales)
                         spec.setScalingFactors(*scales);
                     if (newLastCapletVol)
                         spec.setLastCapletVol(*newLastCapletVol);
                     json::object out;
                     out["period"] = static_cast<std::int64_t>(spec.getPeriod());
                     out["offset"] = static_cast<std::int64_t>(spec.getOffset());
                     out["noBigRates"] = static_cast<std::int64_t>(spec.getNoBigRates());
                     out["noSmallRates"] = static_cast<std::int64_t>(spec.getNoSmallRates());
                     out["interpolatedVariances"] = reads(spec.interpolatedVariances());
                     out["originalVariances"] = reads(spec.originalVariances());
                     return json::value(out);
                 };
             }},
        });

    }

}
