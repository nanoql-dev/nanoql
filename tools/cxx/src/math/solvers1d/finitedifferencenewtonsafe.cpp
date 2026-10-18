// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/solvers1d/finitedifferencenewtonsafe.hpp, which the wheel doesn't expose: FiniteDifferenceNewtonSafe's two
// solve forms (Solver1D's, ql/math/solver1d.hpp), within a bracket and from a step, on a named function, counting its
// calls. The solver is {"maxEvaluations", "lowerBound", "upperBound"}, each null for its default; the result is
// {"root", "calls", "lastArgument"}, the function's calls counted by the function itself (the solver's own count skips
// one), and the argument of its last call, null before any. The functions, written the same way in
// tests/NanoQL.Tests/Vectors/Cxx/Numerics/Solvers1D/FiniteDifferenceNewtonSafeCxxVectorTests.cs, so that the
// arithmetic is the same:
//   p1 x x - 1;  p2 1 - x x;  p3 x x x - 2 x - 5;  p4 (x - 0.3)(x + 2)(x - 4);  p5 x x x x - 10;
//   p6 0.5 x x x - x x + 0.25;  cube (x - 1)(x - 1)(x - 1);  reciprocal 1 / x - 2;  root sqrt(x) - 1.5;
//   lorentz x / (1 + x x) - 0.3;  quintic x x x x x - 3 x + 1;  flat 0.001 (x - 7) + (x - 7)(x - 7)(x - 7)

#include "convert.hpp"

#include <ql/math/solvers1d/finitedifferencenewtonsafe.hpp>
#include <ql/utilities/null.hpp>

#include <cmath>
#include <optional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SolverSpec {
            std::optional<Size> maxEvaluations;
            std::optional<Real> lowerBound, upperBound;
        };

        SolverSpec solver(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a solver spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "maxEvaluations" && kv.key() != "lowerBound" && kv.key() != "upperBound")
                    throw InvalidParams("a solver takes no key " + std::string(kv.key()));
            }
            SolverSpec s;
            auto get = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a solver needs ") + name);
                return *x;
            };
            if (!get("maxEvaluations").is_null()) {
                Integer n = integer(get("maxEvaluations"));
                if (n == Null<Integer>() || n < 0)
                    throw InvalidParams("expected a Size, got " + json::serialize(get("maxEvaluations")));
                s.maxEvaluations = static_cast<Size>(n);
            }
            if (!get("lowerBound").is_null())
                s.lowerBound = real(get("lowerBound"));
            if (!get("upperBound").is_null())
                s.upperBound = real(get("upperBound"));
            return s;
        }

        Real evaluate(const std::string& name, Real x) {
            if (name == "p1")
                return x * x - 1.0;
            if (name == "p2")
                return 1.0 - x * x;
            if (name == "p3")
                return x * x * x - 2.0 * x - 5.0;
            if (name == "p4")
                return (x - 0.3) * (x + 2.0) * (x - 4.0);
            if (name == "p5")
                return x * x * x * x - 10.0;
            if (name == "p6")
                return 0.5 * x * x * x - x * x + 0.25;
            if (name == "cube")
                return (x - 1.0) * (x - 1.0) * (x - 1.0);
            if (name == "reciprocal")
                return 1.0 / x - 2.0;
            if (name == "root")
                return std::sqrt(x) - 1.5;
            if (name == "lorentz")
                return x / (1.0 + x * x) - 0.3;
            if (name == "quintic")
                return x * x * x * x * x - 3.0 * x + 1.0;
            if (name == "flat")
                return 0.001 * (x - 7.0) + (x - 7.0) * (x - 7.0) * (x - 7.0);
            throw InvalidParams("the function " + name + " was not checked");
        }

        std::string function(const json::value& v) {
            std::string name = text(v);
            for (const char* known : {"p1", "p2", "p3", "p4", "p5", "p6", "cube", "reciprocal", "root", "lorentz",
                                      "quintic", "flat"}) {
                if (name == known)
                    return name;
            }
            throw InvalidParams("unknown function " + name);
        }

        // The named function, counting its calls and keeping its last argument.
        struct Counted {
            std::string name;
            int* calls;
            std::optional<Real>* last;
            Real operator()(Real x) const {
                ++*calls;
                *last = x;
                return evaluate(name, x);
            }
        };

        FiniteDifferenceNewtonSafe configured(const SolverSpec& s) {
            FiniteDifferenceNewtonSafe solver;
            if (s.maxEvaluations)
                solver.setMaxEvaluations(*s.maxEvaluations);
            if (s.lowerBound)
                solver.setLowerBound(*s.lowerBound);
            if (s.upperBound)
                solver.setUpperBound(*s.upperBound);
            return solver;
        }

        template <class F>
        json::value counted(const std::string& name, F solve) {
            int calls = 0;
            std::optional<Real> last;
            Real root = solve(Counted{name, &calls, &last});
            json::object out;
            out["root"] = root;
            out["calls"] = calls;
            out["lastArgument"] = last ? json::value(*last) : json::value(nullptr);
            return out;
        }

        const Register unit({
            // solve(solver, function, accuracy, guess, xMin, xMax)
            {"FiniteDifferenceNewtonSafe::solve(f,Real,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 SolverSpec s = solver(a[0]);
                 std::string f = function(a[1]);
                 Real accuracy = real(a[2]), guess = real(a[3]), xMin = real(a[4]), xMax = real(a[5]);
                 return [=] {
                     return counted(f, [&](const Counted& g) {
                         return configured(s).solve(g, accuracy, guess, xMin, xMax);
                     });
                 };
             }},
            // solve(solver, function, accuracy, guess, step)
            {"FiniteDifferenceNewtonSafe::solve(f,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 SolverSpec s = solver(a[0]);
                 std::string f = function(a[1]);
                 Real accuracy = real(a[2]), guess = real(a[3]), step = real(a[4]);
                 return [=] {
                     return counted(f, [&](const Counted& g) { return configured(s).solve(g, accuracy, guess, step); });
                 };
             }},
        });

    }

}
