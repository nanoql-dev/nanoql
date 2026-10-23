// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/shortrate/onefactormodels/markovfunctional.hpp, .cpp: the Markov-functional model's members the wheel
// doesn't bind, its numeraire date and time, its arbitrage indices and its model outputs, on a model built inside the
// call, which calibrates at the first of them under the evaluation date the case's settings set. A model is {"curve"
// (a flat forward spec of indexes/indexspec.hpp), "reversion", "volstepdates" (serials), "volatilities", "kind",
// "settings"}, the kind "swaption" with "swaptionVol" (termstructures/volatility/swaption/swaptionvolspec.hpp),
// "expiries" (serials), "tenors" ([length, units] each) and "swapIndexTenor", the swap index a EuriborSwapIsdaFixA of
// that tenor on QuantLib's empty handle; or "caplet" with "capletVol" (termstructures/volatility/optionlet/
// optionletvolspec.hpp), "expiries" and "iborIndex" (an Ibor index spec of indexes/indexspec.hpp). The settings are
// {"yGridPoints", "yStdDevs", "gaussHermitePoints", "digitalGap", "marketRateAccuracy", "lowerRateBound",
// "upperRateBound", "adjustments", "smileMoneynessCheckpoints"}, the adjustments ModelSettings' integer. An optional
// "forcedArbitrageIndices", [[left, right], ...], is forced on the model once built (forceArbitrageIndices).
//
//     MarkovFunctional::numeraireDate, numeraireTime  (spec)
//     MarkovFunctional::numeraire                     (spec, [[t, y], ...]): [numeraire(t, y), ...]
//     MarkovFunctional::zerobond                      (spec, [[T, t, y], ...]): [zerobond(T, t, y), ...]
//     MarkovFunctional::arbitrageIndices              (spec): [[left, right], ...], null for a custom smile's pair
//     MarkovFunctional::modelOutputs                  (spec): {"expiries", "tenors", "atm", "annuity",
//                                                     "adjustmentFactors", "digitalsAdjustmentFactors", "messages",
//                                                     "smileStrikes", "marketRawCallPremium", "marketRawPutPremium",
//                                                     "marketCallPremium", "marketPutPremium", "modelCallPremium",
//                                                     "modelPutPremium", "marketVega", "marketZerorate",
//                                                     "modelZerorate"}

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolspec.hpp"

#include <ql/indexes/swap/euriborswap.hpp>
#include <ql/models/shortrate/onefactormodels/markovfunctional.hpp>

#include <cstdint>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct MfSpec {
            indexes::CurveSpec curve;
            Real reversion = 0.0;
            std::vector<DateSpec> volstepdates;
            std::vector<Real> volatilities;
            bool caplet = false;
            termstructures::SwaptionVolSpec swaptionVol;
            termstructures::OptionletVolSpec capletVol;
            std::vector<DateSpec> expiries;
            std::vector<termstructures::PeriodSpec> tenors;
            termstructures::PeriodSpec swapIndexTenor{1, Years};
            indexes::IndexSpec iborIndex;
            Size yGridPoints = 64, gaussHermitePoints = 32;
            Real yStdDevs = 7.0, digitalGap = 1E-5, marketRateAccuracy = 1E-7, lowerRateBound = 0.0,
                 upperRateBound = 2.0;
            int adjustments = 0;
            std::vector<Real> checkpoints;
            std::vector<std::pair<Size, Size>> forced;
        };

        MfSpec mfSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Markov-functional model spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            MfSpec s;
            s.curve = indexes::curve(o.at("curve"));
            s.reversion = real(o.at("reversion"));
            for (const auto& d : o.at("volstepdates").as_array())
                s.volstepdates.push_back(date(d));
            s.volatilities = reals(o.at("volatilities"));
            for (const auto& d : o.at("expiries").as_array())
                s.expiries.push_back(date(d));
            std::string kind = text(o.at("kind"));
            if (kind == "swaption") {
                s.swaptionVol = termstructures::swaptionVolatility(o.at("swaptionVol"));
                for (const auto& t : o.at("tenors").as_array())
                    s.tenors.push_back(termstructures::tenor(t));
                s.swapIndexTenor = termstructures::tenor(o.at("swapIndexTenor"));
            } else if (kind == "caplet") {
                s.caplet = true;
                s.capletVol = termstructures::optionletVolatility(o.at("capletVol"));
                s.iborIndex = indexes::iborIndex(o.at("iborIndex"));
            } else {
                throw InvalidParams("a Markov-functional model's kind is swaption or caplet, not " + kind);
            }
            const json::object& st = o.at("settings").as_object();
            s.yGridPoints = static_cast<Size>(integer(st.at("yGridPoints")));
            s.yStdDevs = real(st.at("yStdDevs"));
            s.gaussHermitePoints = static_cast<Size>(integer(st.at("gaussHermitePoints")));
            s.digitalGap = real(st.at("digitalGap"));
            s.marketRateAccuracy = real(st.at("marketRateAccuracy"));
            s.lowerRateBound = real(st.at("lowerRateBound"));
            s.upperRateBound = real(st.at("upperRateBound"));
            s.adjustments = integer(st.at("adjustments"));
            s.checkpoints = reals(st.at("smileMoneynessCheckpoints"));
            if (const json::value* f = o.if_contains("forcedArbitrageIndices")) {
                for (const auto& p : f->as_array()) {
                    const json::array& pair = p.as_array();
                    s.forced.emplace_back(static_cast<Size>(integer(pair.at(0))), static_cast<Size>(integer(pair.at(1))));
                }
            }
            return s;
        }

        ext::shared_ptr<MarkovFunctional> build(const MfSpec& s) {
            MarkovFunctional::ModelSettings settings(s.yGridPoints, s.yStdDevs, s.gaussHermitePoints, s.digitalGap,
                                                     s.marketRateAccuracy, s.lowerRateBound, s.upperRateBound,
                                                     s.adjustments, s.checkpoints);
            std::vector<Date> steps, expiries;
            for (const auto& d : s.volstepdates)
                steps.push_back(makeDate(d));
            for (const auto& d : s.expiries)
                expiries.push_back(makeDate(d));
            if (s.caplet)
                return ext::make_shared<MarkovFunctional>(
                    indexes::makeCurve(s.curve), s.reversion, steps, s.volatilities,
                    Handle<OptionletVolatilityStructure>(termstructures::makeOptionletVolatility(s.capletVol)),
                    expiries, indexes::makeIborIndex(s.iborIndex), settings);
            std::vector<Period> tenors;
            for (const auto& t : s.tenors)
                tenors.push_back(termstructures::makePeriod(t));
            return ext::make_shared<MarkovFunctional>(
                indexes::makeCurve(s.curve), s.reversion, steps, s.volatilities,
                Handle<SwaptionVolatilityStructure>(termstructures::makeSwaptionVolatility(s.swaptionVol)), expiries,
                tenors, ext::make_shared<EuriborSwapIsdaFixA>(termstructures::makePeriod(s.swapIndexTenor)), settings);
        }

        ext::shared_ptr<MarkovFunctional> make(const MfSpec& s) {
            ext::shared_ptr<MarkovFunctional> m = build(s);
            if (!s.forced.empty())
                m->forceArbitrageIndices(s.forced);
            return m;
        }

        // A member read at each of a list of points, each point a list of reals.
        Method atPoints(std::function<Real(const MarkovFunctional&, const std::vector<Real>&)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 2);
                MfSpec s = mfSpec(a[0]);
                std::vector<std::vector<Real>> points;
                for (const auto& p : a[1].as_array())
                    points.push_back(reals(p));
                return [=] {
                    ext::shared_ptr<MarkovFunctional> m = make(s);
                    json::array out;
                    for (const auto& p : points)
                        out.emplace_back(f(*m, p));
                    return json::value(std::move(out));
                };
            };
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        json::value matrix(const std::vector<std::vector<Real>>& m) {
            json::array out;
            for (const auto& row : m)
                out.push_back(realArray(row));
            return json::value(std::move(out));
        }

        Method member(std::function<json::value(const MarkovFunctional&)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 1);
                MfSpec s = mfSpec(a[0]);
                return [=] { return f(*make(s)); };
            };
        }

        const Register unit({
            {"MarkovFunctional::numeraireDate",
             member([](const MarkovFunctional& m) { return result(m.numeraireDate()); })},
            {"MarkovFunctional::numeraireTime",
             member([](const MarkovFunctional& m) { return json::value(m.numeraireTime()); })},
            {"MarkovFunctional::numeraire",
             atPoints([](const MarkovFunctional& m, const std::vector<Real>& p) { return m.numeraire(p.at(0), p.at(1)); })},
            {"MarkovFunctional::zerobond",
             atPoints([](const MarkovFunctional& m, const std::vector<Real>& p) {
                 return m.zerobond(p.at(0), p.at(1), p.at(2));
             })},
            {"MarkovFunctional::arbitrageIndices",
             member([](const MarkovFunctional& m) {
                 json::array out;
                 for (const auto& p : m.arbitrageIndices()) {
                     if (p.first == Null<Size>())
                         out.emplace_back(nullptr);
                     else
                         out.push_back(json::array{static_cast<std::int64_t>(p.first),
                                                   static_cast<std::int64_t>(p.second)});
                 }
                 return json::value(std::move(out));
             })},
            {"MarkovFunctional::modelOutputs",
             member([](const MarkovFunctional& m) {
                 const MarkovFunctional::ModelOutputs& o = m.modelOutputs();
                 json::object out;
                 json::array expiries, tenors, messages;
                 for (const Date& d : o.expiries_)
                     expiries.push_back(result(d));
                 for (const Period& p : o.tenors_)
                     tenors.push_back(json::array{p.length(), static_cast<int>(p.units())});
                 for (const std::string& s : o.messages_)
                     messages.emplace_back(s);
                 out["expiries"] = std::move(expiries);
                 out["tenors"] = std::move(tenors);
                 out["atm"] = realArray(o.atm_);
                 out["annuity"] = realArray(o.annuity_);
                 out["adjustmentFactors"] = realArray(o.adjustmentFactors_);
                 out["digitalsAdjustmentFactors"] = realArray(o.digitalsAdjustmentFactors_);
                 out["messages"] = std::move(messages);
                 out["smileStrikes"] = matrix(o.smileStrikes_);
                 out["marketRawCallPremium"] = matrix(o.marketRawCallPremium_);
                 out["marketRawPutPremium"] = matrix(o.marketRawPutPremium_);
                 out["marketCallPremium"] = matrix(o.marketCallPremium_);
                 out["marketPutPremium"] = matrix(o.marketPutPremium_);
                 out["modelCallPremium"] = matrix(o.modelCallPremium_);
                 out["modelPutPremium"] = matrix(o.modelPutPremium_);
                 out["marketVega"] = matrix(o.marketVega_);
                 out["marketZerorate"] = realArray(o.marketZerorate_);
                 out["modelZerorate"] = realArray(o.modelZerorate_);
                 return json::value(std::move(out));
             })},
        });

    }

}
