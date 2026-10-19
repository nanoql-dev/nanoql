// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp and swaptionvolcube.hpp: what the wheel doesn't
// reach of the swaption volatility cubes, on a cube spec built inside the call (tools/vectors/gen/termstructures/
// volatility/swaption/swaptionvolcube.py's specs):
//
//     XabrSwaptionVolatilityCube::marketVolCube(Size)   (cube, i): the i-th strike spread's market volatilities, a
//                                                       list of rows, after the cube's calculation, which QuantLib's
//                                                       marketVolCube(i) doesn't run; an i past the strike spreads is
//                                                       the harness's refusal (QuantLib reads past its matrices)
//     XabrSwaptionVolatilityCube::sparseSabrParameters  (cube): the calculation's sparse parameters, a list of rows,
//                                                       the route of the construction's and the calibration's
//                                                       refusals
//     SwaptionVolatilityCube::maxSwapTenor              (cube): [length, units]
//     SwaptionVolatilityCube::atmStrike(Period,Period)  (cube, optionTenor, swapTenor)
//
// A cube is {"type" ("InterpolatedSwaptionVolatilityCube" or "SabrSwaptionVolatilityCube"), "atm", "optionTenors",
// "swapTenors", "strikeSpreads", "volSpreads", "swapIndex", "shortSwapIndex", "vegaWeightedSmileFit",
// "enableExtrapolation"}, and for a SABR cube "parametersGuess", "isParameterFixed", "isAtmCalibrated", "endCriteria",
// "maxErrorTolerance", "optMethod", "errorAccept", "useMaxError", "maxGuesses", "backwardFlat", "cutoffStrike" and
// "singlePassCalibration". Its at-the-money structure is a matrix spec of swaptionvolmatrixspec.hpp (with its "form")
// or a structure spec of swaptionvolspec.hpp; a swap index {"type": "EuriborSwapIsdaFixA", "tenor", "forwarding"},
// with "discounting" for exogenous discounting, each curve a spec of indexes/indexspec.hpp; the volatility spreads and
// the guesses a list of rows; the end criteria null or a spec of math/optimization/specs.hpp; the method null,
// {"type": "Simplex", "lambda"} or {"type": "LevenbergMarquardt", "epsfcn", "xtol", "gtol",
// "useCostFunctionsJacobian"}; the tolerance and errorAccept null for Null<Real>(). The cube's rows must be one per
// option and swap tenor pair and its guesses and flags four, which QuantLib reads unchecked: the harness refuses
// others.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "math/optimization/specs.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolmatrixspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolspec.hpp"

#include <ql/indexes/swap/euriborswap.hpp>
#include <ql/math/matrix.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/simplex.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.hpp>
#include <ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp>

#include <cstdint>
#include <initializer_list>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makePeriod;
        using termstructures::PeriodSpec;
        using termstructures::tenor;

        struct SwapIndexSpec {
            PeriodSpec tenor{0, Years};
            indexes::CurveSpec forwarding;
            bool exogenous = false;
            indexes::CurveSpec discounting;
        };

        struct MethodSpec {
            std::string type;  // empty for none, Simplex or LevenbergMarquardt
            Real lambda = 0.0, epsfcn = 0.0, xtol = 0.0, gtol = 0.0;
            bool useCostFunctionsJacobian = false;
        };

        struct CubeSpec {
            std::string type;
            bool matrix = true;
            termstructures::SwaptionVolMatrixSpec atmMatrix;
            termstructures::SwaptionVolSpec atmStructure;
            std::vector<PeriodSpec> optionTenors, swapTenors;
            std::vector<Real> strikeSpreads;
            std::vector<std::vector<Real>> volSpreads;
            SwapIndexSpec swapIndex, shortSwapIndex;
            bool vegaWeightedSmileFit = false;
            bool enableExtrapolation = false;
            std::vector<std::vector<Real>> parametersGuess;
            std::vector<bool> isParameterFixed;
            bool isAtmCalibrated = false;
            std::optional<optimization::EndCriteriaSpec> endCriteria;
            Real maxErrorTolerance = Null<Real>();
            MethodSpec optMethod;
            Real errorAccept = Null<Real>();
            bool useMaxError = false;
            Size maxGuesses = 50;
            bool backwardFlat = false;
            Real cutoffStrike = 0.0001;
            bool singlePassCalibration = false;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, const std::vector<std::string>& keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const std::string& k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        const json::array& array(const json::value& v, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_array();
        }

        std::vector<std::vector<Real>> rows(const json::value& v, const std::string& what) {
            std::vector<std::vector<Real>> out;
            for (const json::value& row : array(v, what))
                out.push_back(reals(row));
            return out;
        }

        std::vector<PeriodSpec> tenors(const json::value& v) {
            std::vector<PeriodSpec> out;
            for (const json::value& p : array(v, "a list of tenors"))
                out.push_back(tenor(p));
            return out;
        }

        SwapIndexSpec swapIndex(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a swap index spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const std::string what = "a swap index";
            if (text(key(o, "type", what)) != "EuriborSwapIsdaFixA")
                throw InvalidParams("unknown swap index " + json::serialize(key(o, "type", what)));
            onlyKeys(o, {"type", "tenor", "forwarding", "discounting"}, what);
            SwapIndexSpec s;
            s.tenor = tenor(key(o, "tenor", what));
            s.forwarding = indexes::curve(key(o, "forwarding", what));
            const json::value* d = o.if_contains("discounting");
            s.exogenous = d != nullptr;
            if (s.exogenous)
                s.discounting = indexes::curve(*d);
            return s;
        }

        MethodSpec method(const json::value& v) {
            MethodSpec s;
            if (v.is_null())
                return s;
            if (!v.is_object())
                throw InvalidParams("expected a method spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            s.type = text(key(o, "type", "a method"));
            if (s.type == "Simplex") {
                onlyKeys(o, {"type", "lambda"}, "a Simplex method");
                s.lambda = real(key(o, "lambda", "a Simplex method"));
            } else if (s.type == "LevenbergMarquardt") {
                const std::string what = "a LevenbergMarquardt method";
                onlyKeys(o, {"type", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"}, what);
                s.epsfcn = real(key(o, "epsfcn", what));
                s.xtol = real(key(o, "xtol", what));
                s.gtol = real(key(o, "gtol", what));
                s.useCostFunctionsJacobian = boolean(key(o, "useCostFunctionsJacobian", what));
            } else {
                throw InvalidParams("unknown method " + s.type);
            }
            return s;
        }

        CubeSpec cube(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a cube spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            CubeSpec s;
            s.type = text(key(o, "type", "a cube"));
            const std::string& what = s.type;
            std::vector<std::string> keys = {"type", "atm", "optionTenors", "swapTenors", "strikeSpreads",
                                             "volSpreads", "swapIndex", "shortSwapIndex", "vegaWeightedSmileFit",
                                             "enableExtrapolation"};
            bool sabr = s.type == "SabrSwaptionVolatilityCube";
            if (sabr) {
                for (const char* k : {"parametersGuess", "isParameterFixed", "isAtmCalibrated", "endCriteria",
                                      "maxErrorTolerance", "optMethod", "errorAccept", "useMaxError", "maxGuesses",
                                      "backwardFlat", "cutoffStrike", "singlePassCalibration"})
                    keys.emplace_back(k);
            } else if (s.type != "InterpolatedSwaptionVolatilityCube") {
                throw InvalidParams("unknown cube " + s.type);
            }
            onlyKeys(o, keys, what);
            const json::value& atm = key(o, "atm", what);
            s.matrix = atm.is_object() && atm.get_object().if_contains("form") != nullptr;
            if (s.matrix)
                s.atmMatrix = termstructures::swaptionVolMatrix(atm);
            else
                s.atmStructure = termstructures::swaptionVolatility(atm);
            s.optionTenors = tenors(key(o, "optionTenors", what));
            s.swapTenors = tenors(key(o, "swapTenors", what));
            s.strikeSpreads = reals(key(o, "strikeSpreads", what));
            s.volSpreads = rows(key(o, "volSpreads", what), "the volatility spreads' rows");
            s.swapIndex = swapIndex(key(o, "swapIndex", what));
            s.shortSwapIndex = swapIndex(key(o, "shortSwapIndex", what));
            s.vegaWeightedSmileFit = boolean(key(o, "vegaWeightedSmileFit", what));
            s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
            if (sabr) {
                s.parametersGuess = rows(key(o, "parametersGuess", what), "the guesses' rows");
                for (const json::value& b : array(key(o, "isParameterFixed", what), "the fixed flags"))
                    s.isParameterFixed.push_back(boolean(b));
                s.isAtmCalibrated = boolean(key(o, "isAtmCalibrated", what));
                const json::value& criteria = key(o, "endCriteria", what);
                if (!criteria.is_null())
                    s.endCriteria = optimization::endCriteria(criteria);
                s.maxErrorTolerance = real(key(o, "maxErrorTolerance", what));
                s.optMethod = method(key(o, "optMethod", what));
                s.errorAccept = real(key(o, "errorAccept", what));
                s.useMaxError = boolean(key(o, "useMaxError", what));
                s.maxGuesses = optimization::size(key(o, "maxGuesses", what));
                s.backwardFlat = boolean(key(o, "backwardFlat", what));
                s.cutoffStrike = real(key(o, "cutoffStrike", what));
                s.singlePassCalibration = boolean(key(o, "singlePassCalibration", what));
                // QuantLib reads the guesses' first four entries of every pair's row, and the first four flags,
                // unchecked.
                if (s.parametersGuess.size() < s.optionTenors.size() * s.swapTenors.size())
                    throw InvalidParams("a SABR cube spec needs a guess row per option and swap tenor pair");
                for (const auto& row : s.parametersGuess)
                    if (row.size() < 4)
                        throw InvalidParams("a SABR cube spec's guess rows need four guesses");
                if (s.isParameterFixed.size() < 4)
                    throw InvalidParams("a SABR cube spec needs four fixed flags");
            }
            return s;
        }

        std::vector<Period> periods(const std::vector<PeriodSpec>& p) {
            std::vector<Period> out;
            for (const PeriodSpec& x : p)
                out.push_back(makePeriod(x));
            return out;
        }

        std::vector<std::vector<Handle<Quote>>> quotes(const std::vector<std::vector<Real>>& r) {
            std::vector<std::vector<Handle<Quote>>> out;
            for (const auto& row : r) {
                std::vector<Handle<Quote>> q;
                for (Real x : row)
                    q.emplace_back(ext::make_shared<SimpleQuote>(x));
                out.push_back(q);
            }
            return out;
        }

        ext::shared_ptr<SwapIndex> makeSwapIndex(const SwapIndexSpec& s) {
            if (s.exogenous)
                return ext::make_shared<EuriborSwapIsdaFixA>(makePeriod(s.tenor), indexes::makeCurve(s.forwarding),
                                                             indexes::makeCurve(s.discounting));
            return ext::make_shared<EuriborSwapIsdaFixA>(makePeriod(s.tenor), indexes::makeCurve(s.forwarding));
        }

        ext::shared_ptr<OptimizationMethod> makeMethod(const MethodSpec& s) {
            if (s.type == "Simplex")
                return ext::make_shared<Simplex>(s.lambda);
            if (s.type == "LevenbergMarquardt")
                return ext::make_shared<LevenbergMarquardt>(s.epsfcn, s.xtol, s.gtol, s.useCostFunctionsJacobian);
            return {};
        }

        ext::shared_ptr<SwaptionVolatilityCube> makeCube(const CubeSpec& s) {
            ext::shared_ptr<SwaptionVolatilityStructure> atm =
                s.matrix ? ext::shared_ptr<SwaptionVolatilityStructure>(termstructures::makeSwaptionVolMatrix(s.atmMatrix))
                         : termstructures::makeSwaptionVolatility(s.atmStructure);
            Handle<SwaptionVolatilityStructure> h(atm);
            ext::shared_ptr<SwaptionVolatilityCube> out;
            if (s.type == "InterpolatedSwaptionVolatilityCube") {
                out = ext::make_shared<InterpolatedSwaptionVolatilityCube>(
                    h, periods(s.optionTenors), periods(s.swapTenors), s.strikeSpreads, quotes(s.volSpreads),
                    makeSwapIndex(s.swapIndex), makeSwapIndex(s.shortSwapIndex), s.vegaWeightedSmileFit);
            } else {
                ext::shared_ptr<EndCriteria> criteria;
                if (s.endCriteria)
                    criteria = ext::make_shared<EndCriteria>(optimization::makeEndCriteria(*s.endCriteria));
                out = ext::make_shared<SabrSwaptionVolatilityCube>(
                    h, periods(s.optionTenors), periods(s.swapTenors), s.strikeSpreads, quotes(s.volSpreads),
                    makeSwapIndex(s.swapIndex), makeSwapIndex(s.shortSwapIndex), s.vegaWeightedSmileFit,
                    quotes(s.parametersGuess), s.isParameterFixed, s.isAtmCalibrated, criteria, s.maxErrorTolerance,
                    makeMethod(s.optMethod), s.errorAccept, s.useMaxError, s.maxGuesses, s.backwardFlat,
                    s.cutoffStrike, s.singlePassCalibration);
            }
            if (s.enableExtrapolation)
                out->enableExtrapolation();
            return out;
        }

        json::value matrixRows(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i) {
                json::array row;
                for (Size j = 0; j < m.columns(); ++j)
                    row.emplace_back(m[i][j]);
                out.emplace_back(std::move(row));
            }
            return json::value(std::move(out));
        }

        CubeSpec sabrCube(const json::value& v) {
            CubeSpec s = cube(v);
            if (s.type != "SabrSwaptionVolatilityCube")
                throw InvalidParams("expected a SABR cube spec, got a " + s.type);
            return s;
        }

        const Register unit({
            {"XabrSwaptionVolatilityCube::marketVolCube(Size)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 CubeSpec s = sabrCube(a[0]);
                 Size i = optimization::size(a[1]);
                 if (i >= s.strikeSpreads.size())
                     throw InvalidParams("an index past the cube's strike spreads");
                 return [=] {
                     auto c = ext::dynamic_pointer_cast<SabrSwaptionVolatilityCube>(makeCube(s));
                     c->sparseSabrParameters();
                     return matrixRows(c->marketVolCube(i));
                 };
             }},
            {"XabrSwaptionVolatilityCube::sparseSabrParameters",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CubeSpec s = sabrCube(a[0]);
                 return [=] {
                     auto c = ext::dynamic_pointer_cast<SabrSwaptionVolatilityCube>(makeCube(s));
                     return matrixRows(c->sparseSabrParameters());
                 };
             }},
            {"SwaptionVolatilityCube::maxSwapTenor",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CubeSpec s = cube(a[0]);
                 return [=] {
                     const Period& p = makeCube(s)->maxSwapTenor();
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(p.length()));
                     out.emplace_back(static_cast<std::int64_t>(p.units()));
                     return json::value(std::move(out));
                 };
             }},
            {"SwaptionVolatilityCube::atmStrike(Period,Period)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CubeSpec s = cube(a[0]);
                 PeriodSpec option = tenor(a[1]);
                 PeriodSpec swap = tenor(a[2]);
                 return [=] { return json::value(makeCube(s)->atmStrike(makePeriod(option), makePeriod(swap))); };
             }},
        });

    }

}
