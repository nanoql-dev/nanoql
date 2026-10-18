// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/globalbootstrap.hpp: what the wheel's GlobalBootstrap can't take, on a piecewise curve built inside
// the call from specs of termstructures/yield/ratehelperspec.hpp under the settings already set: instrument weights;
// additional dates from a functor of the evaluation date; a penalty of the harness's own, the gradient penalty of
// QuantLib's testGlobalBootstrapInitialGuessFn (test-suite/piecewiseyieldcurve.cpp:1567-1582), on additional helpers;
// an initial-guess function of its own, which counts its calls; and a traits of the harness's own, QuantLib's Discount
// without globalGuess, whose curve takes the per-pillar guess (globalbootstrap.hpp:412-427), as
// tests/NanoQL.Tests/Vectors/Cxx/TermStructures/GlobalBootstrapCxxVectorTests.cs defines them.
//
// The curve is {"traits", "interpolator", "referenceDate", "dayCounter"}: the traits "Discount", "ZeroYield",
// "ForwardRate", "SimpleZeroYield" or "DiscountWithoutGlobalGuess", the interpolator "LogLinear", "Linear" or
// "BackwardFlat" (the pairs the method instantiates), a fixed reference date and a day counter of convert.hpp. The
// bootstrap is {"accuracy", "weights", "additionalHelpers", "dates", "penalty", "guess"}: the accuracy, null for
// Null<Real>(); the instrument weights, empty for none; the additional helpers' specs; the dates functor, null or
// {"offsets": [days]}, the evaluation date plus each offset in days; the penalty, null or {"weight", "helpers"}, the
// additional helpers' quote errors first when "helpers" is set, then the weight times each change of the simple
// forward rates between nodes over its time step; the guess function, null or {"a", "b", "extra"}, a value a + b t at
// each node's time t after the first, then "extra" more values at the last node's time. The curve uses QuantLib's
// default optimizer and end criteria.
//
// The result is {"dates", "data", "maxDate", "quoteErrors", "additionalQuoteErrors", "guessCalls"}: the curve's nodes,
// its maximum date, each helper's and each additional helper's quote error, null for one whose pillar isn't after the
// reference date (QuantLib binds only the alive ones), and the number of the guess function's calls. A refusal of
// QuantLib's, the bootstrap's or the curve's, is the call's.

#include "convert.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/globalbootstrap.hpp>
#include <ql/termstructures/yield/bootstraptraits.hpp>
#include <ql/termstructures/yield/piecewiseyieldcurve.hpp>

#include <functional>
#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        // QuantLib's Discount traits without globalGuess: the global bootstrap takes each node's guess in turn.
        struct DiscountWithoutGlobalGuess {
            template <class Interpolator>
            struct curve {
                typedef InterpolatedDiscountCurve<Interpolator> type;
            };
            typedef BootstrapHelper<YieldTermStructure> helper;

            static Date initialDate(const YieldTermStructure* c) { return Discount::initialDate(c); }
            static Real initialValue(const YieldTermStructure* c) { return Discount::initialValue(c); }
            template <class C>
            static Real guess(Size i, const C* c, bool validData, Size firstAliveHelper) {
                return Discount::guess(i, c, validData, firstAliveHelper);
            }
            template <class C>
            static Real minValueAfter(Size i, const C* c, bool validData, Size firstAliveHelper) {
                return Discount::minValueAfter(i, c, validData, firstAliveHelper);
            }
            template <class C>
            static Real maxValueAfter(Size i, const C* c, bool validData, Size firstAliveHelper) {
                return Discount::maxValueAfter(i, c, validData, firstAliveHelper);
            }
            template <class C>
            static Real transformDirect(Real x, Size i, const C* c) {
                return Discount::transformDirect(x, i, c);
            }
            template <class C>
            static Real transformInverse(Real x, Size i, const C* c) {
                return Discount::transformInverse(x, i, c);
            }
            static void updateGuess(std::vector<Real>& data, Real discount, Size i) {
                Discount::updateGuess(data, discount, i);
            }
            static Size maxIterations() { return Discount::maxIterations(); }
        };

        using termstructures::RateHelperSpec;

        struct CurveSpec {
            std::string traits, interpolator;
            DateSpec referenceDate;
            DayCounterSpec dayCounter;
        };

        struct BootstrapSpec {
            Real accuracy = Null<Real>();
            std::vector<Real> weights;
            std::vector<RateHelperSpec> additionalHelpers;
            bool dates = false;
            std::vector<Integer> offsets;
            bool penalty = false;
            Real penaltyWeight = 0.0;
            bool penaltyHelpers = false;
            bool guess = false;
            Real guessA = 0.0, guessB = 0.0;
            Integer guessExtra = 0;
        };

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        const char* const pairs[][2] = {{"Discount", "LogLinear"},
                                        {"Discount", "Linear"},
                                        {"ZeroYield", "Linear"},
                                        {"ForwardRate", "BackwardFlat"},
                                        {"SimpleZeroYield", "Linear"},
                                        {"DiscountWithoutGlobalGuess", "LogLinear"}};

        CurveSpec curve(const json::value& v) {
            const json::object& o = object(v, "a curve");
            onlyKeys(o, {"traits", "interpolator", "referenceDate", "dayCounter"}, "a curve");
            CurveSpec s;
            s.traits = text(key(o, "traits", "a curve"));
            s.interpolator = text(key(o, "interpolator", "a curve"));
            bool known = false;
            for (const auto& p : pairs)
                known = known || (s.traits == p[0] && s.interpolator == p[1]);
            if (!known)
                throw InvalidParams("no curve on " + s.traits + " and " + s.interpolator);
            s.referenceDate = date(key(o, "referenceDate", "a curve"));
            s.dayCounter = dayCounter(key(o, "dayCounter", "a curve"));
            return s;
        }

        std::vector<RateHelperSpec> helpers(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of rate helpers, got " + json::serialize(v));
            std::vector<RateHelperSpec> out;
            for (const json::value& h : v.get_array())
                out.push_back(termstructures::rateHelper(h));
            return out;
        }

        BootstrapSpec bootstrap(const json::value& v) {
            const json::object& o = object(v, "a bootstrap");
            onlyKeys(o, {"accuracy", "weights", "additionalHelpers", "dates", "penalty", "guess"}, "a bootstrap");
            BootstrapSpec s;
            s.accuracy = real(key(o, "accuracy", "a bootstrap"));
            s.weights = reals(key(o, "weights", "a bootstrap"));
            s.additionalHelpers = helpers(key(o, "additionalHelpers", "a bootstrap"));
            const json::value& d = key(o, "dates", "a bootstrap");
            if (!d.is_null()) {
                const json::object& od = object(d, "a dates functor");
                onlyKeys(od, {"offsets"}, "a dates functor");
                s.dates = true;
                s.offsets = integers(key(od, "offsets", "a dates functor"));
            }
            const json::value& p = key(o, "penalty", "a bootstrap");
            if (!p.is_null()) {
                const json::object& op = object(p, "a penalty");
                onlyKeys(op, {"weight", "helpers"}, "a penalty");
                s.penalty = true;
                s.penaltyWeight = real(key(op, "weight", "a penalty"));
                s.penaltyHelpers = boolean(key(op, "helpers", "a penalty"));
            }
            const json::value& g = key(o, "guess", "a bootstrap");
            if (!g.is_null()) {
                const json::object& og = object(g, "a guess function");
                onlyKeys(og, {"a", "b", "extra"}, "a guess function");
                s.guess = true;
                s.guessA = real(key(og, "a", "a guess function"));
                s.guessB = real(key(og, "b", "a guess function"));
                s.guessExtra = integer(key(og, "extra", "a guess function"));
                if (s.guessExtra == Null<Integer>() || s.guessExtra < 0)
                    throw InvalidParams("a guess function's extra values can't be fewer than none");
            }
            return s;
        }

        json::value quoteErrors(const std::vector<ext::shared_ptr<RateHelper>>& hs, const Date& referenceDate) {
            json::array out;
            for (const auto& h : hs)
                out.push_back(h->pillarDate() > referenceDate ? json::value(h->quoteError()) : json::value(nullptr));
            return out;
        }

        template <class Traits, class Interpolator>
        json::value run(const CurveSpec& c, const std::vector<RateHelperSpec>& h, const BootstrapSpec& b) {
            typedef PiecewiseYieldCurve<Traits, Interpolator, GlobalBootstrap> Curve;
            std::vector<ext::shared_ptr<RateHelper>> instruments, additional;
            for (const RateHelperSpec& s : h)
                instruments.push_back(termstructures::makeRateHelper(s));
            for (const RateHelperSpec& s : b.additionalHelpers)
                additional.push_back(termstructures::makeRateHelper(s));

            std::function<std::vector<Date>()> dates;
            if (b.dates) {
                std::vector<Integer> offsets = b.offsets;
                dates = [offsets]() {
                    Date today = Settings::instance().evaluationDate();
                    std::vector<Date> out;
                    for (Integer d : offsets)
                        out.push_back(today + d);
                    return out;
                };
            }

            std::function<Array(const std::vector<Time>&, const std::vector<Real>&)> penalties;
            if (b.penalty) {
                Real w = b.penaltyWeight;
                bool withHelpers = b.penaltyHelpers;
                penalties = [additional, w, withHelpers](const std::vector<Time>& times,
                                                         const std::vector<Real>& data) {
                    const Size nInst = withHelpers ? additional.size() : 0;
                    Array errors(nInst + times.size() - 2);
                    // instruments errors
                    for (Size i = 0; i < nInst; ++i)
                        errors[i] = additional[i]->quoteError();
                    // gradient penalties
                    Array rates(data.size() - 1);
                    for (Size i = 0; i < times.size() - 1; ++i)
                        rates[i] = (data[i] / data[i + 1] - 1.0) / (times[i + 1] - times[i]);
                    for (Size i = 0; i < times.size() - 2; ++i)
                        errors[nInst + i] = w * (rates[i + 1] - rates[i]) / (times[i + 1] - times[i]);
                    return errors;
                };
            }

            int guessCalls = 0;
            std::function<Array(const std::vector<Time>&, const std::vector<Real>&)> guessFn;
            if (b.guess) {
                Real a = b.guessA, slope = b.guessB;
                Size extra = static_cast<Size>(b.guessExtra);
                guessFn = [&guessCalls, a, slope, extra](const std::vector<Time>& times, const std::vector<Real>&) {
                    ++guessCalls;
                    const Size pillars = times.size() - 1;
                    Array guess(pillars + extra);
                    for (Size i = 0; i < guess.size(); ++i)
                        guess[i] = a + slope * times[std::min(i + 1, pillars)];
                    return guess;
                };
            }

            // named through the curve, which is instantiated first, as QuantLib's own tests name it
            typename Curve::bootstrap_type globalBootstrap(additional, dates, penalties, b.accuracy, nullptr, nullptr,
                                                           nullptr, b.weights, guessFn);
            Date referenceDate = makeDate(c.referenceDate);
            auto curve = ext::make_shared<Curve>(referenceDate, instruments, makeDayCounter(c.dayCounter),
                                                 Interpolator(), globalBootstrap);
            json::array nodeDates, nodeData;
            for (const auto& n : curve->nodes()) {
                nodeDates.push_back(result(n.first));
                nodeData.push_back(json::value(n.second));
            }
            json::object out;
            out["dates"] = nodeDates;
            out["data"] = nodeData;
            out["maxDate"] = result(curve->maxDate());
            out["quoteErrors"] = quoteErrors(instruments, referenceDate);
            out["additionalQuoteErrors"] = quoteErrors(additional, referenceDate);
            out["guessCalls"] = guessCalls;
            return out;
        }

        json::value dispatch(const CurveSpec& c, const std::vector<RateHelperSpec>& h, const BootstrapSpec& b) {
            if (c.traits == "Discount" && c.interpolator == "LogLinear")
                return run<Discount, LogLinear>(c, h, b);
            if (c.traits == "Discount" && c.interpolator == "Linear")
                return run<Discount, Linear>(c, h, b);
            if (c.traits == "ZeroYield" && c.interpolator == "Linear")
                return run<ZeroYield, Linear>(c, h, b);
            if (c.traits == "ForwardRate" && c.interpolator == "BackwardFlat")
                return run<ForwardRate, BackwardFlat>(c, h, b);
            if (c.traits == "SimpleZeroYield" && c.interpolator == "Linear")
                return run<SimpleZeroYield, Linear>(c, h, b);
            if (c.traits == "DiscountWithoutGlobalGuess" && c.interpolator == "LogLinear")
                return run<DiscountWithoutGlobalGuess, LogLinear>(c, h, b);
            throw InvalidParams("the curve on " + c.traits + " and " + c.interpolator + " was not checked");
        }

        const Register unit({
            {"GlobalBootstrap::curve",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CurveSpec c = curve(a[0]);
                 std::vector<RateHelperSpec> h = helpers(a[1]);
                 BootstrapSpec b = bootstrap(a[2]);
                 return [=] { return dispatch(c, h, b); };
             }},
        });

    }

}
