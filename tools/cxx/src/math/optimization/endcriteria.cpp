// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/endcriteria.hpp, .cpp: EndCriteria's constructor and inspectors, each check and operator(),
// succeeded and the stream text of EndCriteria::Type. End criteria are a spec (math/optimization/specs.hpp). A check
// starts from ecType None and returns {"result", "ecType"}, with "statStateIterations" for the checks that count
// stationary iterations: QuantLib's return value and what it leaves in its Size& and Type& arguments. A count is a
// non-negative integer.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/endcriteria.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        json::value typeResult(EndCriteria::Type t) { return json::value(static_cast<std::int64_t>(t)); }

        json::value checked(bool result, EndCriteria::Type t) {
            return json::object{{"result", result}, {"ecType", typeResult(t)}};
        }

        json::value counted(bool result, Size statState, EndCriteria::Type t) {
            return json::object{
                {"result", result}, {"statStateIterations", static_cast<std::int64_t>(statState)}, {"ecType", typeResult(t)}};
        }

        // A check counting stationary iterations: end criteria, two reals and a count.
        Call stationary(const Args& a, bool (EndCriteria::*check)(Real, Real, Size&, EndCriteria::Type&) const) {
            arity(a, 4);
            EndCriteriaSpec s = endCriteria(a[0]);
            Real xOld = real(a[1]), xNew = real(a[2]);
            Size count = size(a[3]);
            return [=] {
                const EndCriteria e = makeEndCriteria(s);
                Size statState = count;
                EndCriteria::Type t = EndCriteria::None;
                bool result = (e.*check)(xOld, xNew, statState, t);
                return counted(result, statState, t);
            };
        }

        const Register unit({
            {"EndCriteria",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 EndCriteriaSpec s = endCriteria(a[0]);
                 return [=] {
                     const EndCriteria e = makeEndCriteria(s);
                     return json::value(
                         json::object{{"maxIterations", static_cast<std::int64_t>(e.maxIterations())},
                                      {"maxStationaryStateIterations",
                                       static_cast<std::int64_t>(e.maxStationaryStateIterations())},
                                      {"rootEpsilon", e.rootEpsilon()},
                                      {"functionEpsilon", e.functionEpsilon()},
                                      {"gradientNormEpsilon", e.gradientNormEpsilon()}});
                 };
             }},
            {"EndCriteria::checkMaxIterations",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 EndCriteriaSpec s = endCriteria(a[0]);
                 Size iteration = size(a[1]);
                 return [=] {
                     EndCriteria::Type t = EndCriteria::None;
                     bool result = makeEndCriteria(s).checkMaxIterations(iteration, t);
                     return checked(result, t);
                 };
             }},
            {"EndCriteria::checkStationaryPoint",
             [](const Args& a) { return stationary(a, &EndCriteria::checkStationaryPoint); }},
            {"EndCriteria::checkStationaryFunctionValue",
             [](const Args& a) { return stationary(a, &EndCriteria::checkStationaryFunctionValue); }},
            {"EndCriteria::checkStationaryFunctionAccuracy",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 EndCriteriaSpec s = endCriteria(a[0]);
                 Real f = real(a[1]);
                 bool positive = boolean(a[2]);
                 return [=] {
                     EndCriteria::Type t = EndCriteria::None;
                     bool result = makeEndCriteria(s).checkStationaryFunctionAccuracy(f, positive, t);
                     return checked(result, t);
                 };
             }},
            {"EndCriteria::checkZeroGradientNorm",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 EndCriteriaSpec s = endCriteria(a[0]);
                 Real gNorm = real(a[1]);
                 return [=] {
                     EndCriteria::Type t = EndCriteria::None;
                     bool result = makeEndCriteria(s).checkZeroGradientNorm(gNorm, t);
                     return checked(result, t);
                 };
             }},
            {"EndCriteria::operator()",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 EndCriteriaSpec s = endCriteria(a[0]);
                 Size iteration = size(a[1]), count = size(a[2]);
                 bool positive = boolean(a[3]);
                 Real fold = real(a[4]), normgold = real(a[5]), fnew = real(a[6]), normgnew = real(a[7]);
                 return [=] {
                     Size statState = count;
                     EndCriteria::Type t = EndCriteria::None;
                     bool result =
                         makeEndCriteria(s)(iteration, statState, positive, fold, normgold, fnew, normgnew, t);
                     return counted(result, statState, t);
                 };
             }},
            {"EndCriteria::succeeded",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto t = enumeration<EndCriteria::Type>(a[0]);
                 return [=] { return json::value(EndCriteria::succeeded(t)); };
             }},
            {"operator<<(EndCriteria::Type)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto t = enumeration<EndCriteria::Type>(a[0]);
                 return [=] { return json::value(streamed(t)); };
             }},
        });

    }

}
