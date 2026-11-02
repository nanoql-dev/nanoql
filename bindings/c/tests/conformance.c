/* SPDX-FileCopyrightText: 2026 The nanoql Authors
   SPDX-License-Identifier: Apache-2.0

   The C conformance suite (docs/bindings.md): the C ABI's rules checked on the test build, nanoql-bind's
   --test-exports output, which adds the test types' functions (nqlt.h) and narrows handles' generations to 8 bits.
   Each section is one ctest test: conformance <section>. A section prints its checks and fails on the first count of
   failures above zero; every section ends with no live handle. */
/* glibc declares dladdr and Dl_info only under _GNU_SOURCE, set before the first system header. */
#if !defined(_WIN32) && !defined(_GNU_SOURCE)
#define _GNU_SOURCE
#endif
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "nanoql.h"
#include "nqlt.h"

/* The test build's own fixed functions: this library's handle tag and its generations' width. */
NQL_API int32_t nqlt_handle_tag(void);
NQL_API int32_t nqlt_generation_bits(void);

#if defined(_WIN32)
#include <windows.h>
#else
#include <dlfcn.h>
#include <pthread.h>
#endif

static int checks, failures;

static const char* last_message(void)
{
    const char* m = nql_last_error_message();
    return m ? m : "(none)";
}

#define CHECK(cond, ...) \
    do { \
        checks++; \
        if (!(cond)) { \
            failures++; \
            printf("FAIL %s:%d: ", __FILE__, __LINE__); \
            printf(__VA_ARGS__); \
            printf("\n"); \
        } \
    } while (0)

#define OK(call) CHECK((call) == NQL_OK, "%s: status %d: %s", #call, nql_last_error_code(), last_message())
#define REFUSED(call, code) \
    do { \
        nql_status s_ = (call); \
        CHECK(s_ == (code), "%s: status %d, expected %d: %s", #call, s_, (code), last_message()); \
    } while (0)

static uint64_t bits(double x)
{
    uint64_t u;
    memcpy(&u, &x, sizeof u);
    return u;
}

#define SAME(a, b) CHECK(bits(a) == bits(b), "%s = %.17g (%016llx), %s = %.17g (%016llx)", #a, (a), \
                         (unsigned long long)bits(a), #b, (b), (unsigned long long)bits(b))

static nql_handle get(nql_status (*getter)(nql_handle*))
{
    nql_handle h = NULL;
    OK(getter(&h));
    return h;
}

static void release(nql_handle h)
{
    if (h) OK(nql_release(h));
}

static double npv(nql_handle results)
{
    double v = 0;
    bool has = false;
    OK(nql_InstrumentResults_get_NPV(results, &v, &has));
    CHECK(has, "the results have an NPV");
    return v;
}

static nql_handle set_lookup(nql_status (*lookup)(nql_handle, const char*, nql_handle*), const char* name)
{
    nql_handle h = NULL;
    OK(lookup(NULL, name, &h));
    return h;
}

/* ---- every kind of 2.1 both ways ---- */
static void test_kinds(void)
{
    double d;
    int32_t i;
    int64_t l;
    bool b, has;
    double nan_in = NAN, neg_zero = -0.0;

    OK(nqlt_Echo_Double(3.25, &d));
    SAME(d, 3.25);
    OK(nqlt_Echo_Double(nan_in, &d));
    CHECK(isnan(d), "NaN crosses as NaN");
    OK(nqlt_Echo_Double(neg_zero, &d));
    CHECK(bits(d) == bits(neg_zero), "-0.0 keeps its sign");
    OK(nqlt_Echo_Double(INFINITY, &d));
    CHECK(isinf(d) && d > 0, "infinity crosses");
    OK(nqlt_Echo_Int(-2147483647 - 1, &i));
    CHECK(i == -2147483647 - 1, "int32 minimum");
    OK(nqlt_Echo_Long(INT64_MAX, &l));
    CHECK(l == INT64_MAX, "int64 maximum");
    OK(nqlt_Echo_Bool(true, &b));
    CHECK(b, "true");
    OK(nqlt_Echo_Bool(false, &b));
    CHECK(!b, "false");

    /* Strings: UTF-8 both ways, NULL as null, the caller frees with nql_free. */
    char* s = NULL;
    const char* text = "Z\xc3\xbcrich \xe2\x82\xac \xf0\x9f\x93\x88"; /* Zürich, the euro sign, an emoji: 1-, 2-, 3- and 4-byte UTF-8 */
    OK(nqlt_Echo_String(text, &s));
    CHECK(s && strcmp(s, text) == 0, "a non-ASCII string round-trips");
    nql_free(s);
    OK(nqlt_Echo_StringLength(text, &i));
    CHECK(i == 11, "the string is 11 UTF-16 code units in .NET (the emoji two), got %d", i);
    OK(nqlt_Echo_String(NULL, &s));
    CHECK(s == NULL, "NULL is null");
    OK(nqlt_Echo_StringLength(NULL, &i));
    CHECK(i == -1, "null reached .NET as null");
    OK(nqlt_Echo_String("", &s));
    CHECK(s && s[0] == 0, "an empty string stays empty");
    nql_free(s);

    /* Dates: the serial; 0 is no date, 367 the first valid, 109574 the last (31 December 2199). */
    nql_date dt;
    OK(nqlt_Echo_Date(367, &dt));
    CHECK(dt == 367, "the first date");
    OK(nqlt_Echo_Date(109574, &dt));
    CHECK(dt == 109574, "the last date");
    OK(nqlt_Echo_Date(0, &dt));
    CHECK(dt == 0, "QuantLib's Date()");
    OK(nqlt_Echo_OptDate(0, &dt));
    CHECK(dt == 0, "no date");
    OK(nqlt_Echo_OptDate(45000, &dt));
    CHECK(dt == 45000, "a nullable date");

    nql_period p = {6, NQL_TimeUnit_Months}, pr;
    OK(nqlt_Echo_Period(p, &pr));
    CHECK(pr.length == 6 && pr.units == NQL_TimeUnit_Months, "a period by value");
    OK(nqlt_Echo_OptPeriod(&p, &pr, &has));
    CHECK(has && pr.length == 6, "a nullable period");
    OK(nqlt_Echo_OptPeriod(NULL, &pr, &has));
    CHECK(!has, "no period");

    OK(nqlt_Echo_Enum(NQL_TimeUnit_Years, &i));
    CHECK(i == NQL_TimeUnit_Years, "an enum");
    int32_t e = NQL_TimeUnit_Weeks;
    OK(nqlt_Echo_OptEnum(&e, &i, &has));
    CHECK(has && i == NQL_TimeUnit_Weeks, "a nullable enum");
    OK(nqlt_Echo_OptEnum(NULL, &i, &has));
    CHECK(!has, "no enum");

    nql_complex z = {1.5, -2.25}, zr;
    OK(nqlt_Echo_Complex(z, &zr));
    CHECK(zr.re == 1.5 && zr.im == -2.25, "a complex number");

    double x = 2.5;
    OK(nqlt_Echo_OptDouble(&x, &d, &has));
    CHECK(has && d == 2.5, "a nullable double");
    OK(nqlt_Echo_OptDouble(NULL, &d, &has));
    CHECK(!has, "no double");
    int32_t n = 7;
    OK(nqlt_Echo_OptInt(&n, &i, &has));
    CHECK(has && i == 7, "a nullable int");
    bool t = true;
    OK(nqlt_Echo_OptBool(&t, &b, &has));
    CHECK(has && b, "a nullable bool");
    OK(nqlt_Echo_OptBool(NULL, &b, &has));
    CHECK(!has, "no bool");
    int64_t big = 1234567890123LL, lr = 0;
    OK(nqlt_Echo_OptLong(&big, &lr, &has));
    CHECK(has && lr == big, "a nullable long");
    OK(nqlt_Echo_OptLong(NULL, &lr, &has));
    CHECK(!has, "no long");
    nql_complex zc = {0.5, -1.0}, zcr;
    OK(nqlt_Echo_OptComplex(&zc, &zcr, &has));
    CHECK(has && zcr.re == 0.5 && zcr.im == -1.0, "a nullable complex number");
    OK(nqlt_Echo_OptComplex(NULL, &zcr, &has));
    CHECK(!has, "no complex number");

    /* A nanoql struct behind a handle, in and out, and nullable; a handle of another type where it is expected. */
    nql_handle usd = set_lookup(nql_ConventionSet_Currency, "USD"), money = NULL, echoed = NULL, none = NULL;
    OK(nql_Money_new__Currency(usd, 12.5, &money));
    OK(nqlt_Echo_Money(money, &echoed));
    double amount = 0;
    OK(nql_Money_get_Value(echoed, &amount));
    CHECK(echoed != money && amount == 12.5, "a struct by a new handle to its copy");
    release(echoed);
    OK(nqlt_Echo_OptMoney(money, &echoed));
    OK(nql_Money_get_Value(echoed, &amount));
    CHECK(amount == 12.5, "a nullable struct");
    release(echoed);
    OK(nqlt_Echo_OptMoney(NULL, &none));
    CHECK(none == NULL, "no struct");
    REFUSED(nqlt_Echo_Money(usd, &echoed), NQL_ERROR_TYPE);
    release(money);
    release(usd);
}

/* ---- arrays, collections, matrices ---- */
static void test_arrays(void)
{
    double in[] = {1.0, -0.0, NAN, 4.5};
    double* out = NULL;
    int32_t n = 0, count;
    OK(nqlt_Echo_Doubles(in, 4, &out, &n));
    CHECK(n == 4 && out[0] == 1.0 && bits(out[1]) == bits(-0.0) && isnan(out[2]) && out[3] == 4.5, "doubles in place and out");
    nql_free(out);
    OK(nqlt_Echo_Doubles(in, 0, &out, &n));
    CHECK(n == 0 && out != NULL, "an empty array: a pointer to free and a zero count");
    nql_free(out);
    OK(nqlt_Echo_DoublesCount(NULL, 0, &count));
    CHECK(count == -1, "a NULL array is a null collection");
    OK(nqlt_Echo_DoublesCount(in, 0, &count));
    CHECK(count == 0, "an empty array is an empty collection");

    int32_t ints[] = {3, -1, 2};
    int32_t* iout = NULL;
    OK(nqlt_Echo_Ints(ints, 3, &iout, &n));
    CHECK(n == 3 && iout[0] == 3 && iout[1] == -1 && iout[2] == 2, "ints");
    nql_free(iout);

    bool bs[] = {true, false, true};
    bool* bout = NULL;
    OK(nqlt_Echo_Bools(bs, 3, &bout, &n));
    CHECK(n == 3 && bout[0] && !bout[1] && bout[2], "bools");
    nql_free(bout);

    nql_date dates[] = {45000, 367, 109574};
    nql_date* dout = NULL;
    OK(nqlt_Echo_Dates(dates, 3, &dout, &n));
    CHECK(n == 3 && dout[0] == 45000 && dout[1] == 367 && dout[2] == 109574, "dates");
    nql_free(dout);
    OK(nqlt_Echo_DateSet(dates, 3, &dout, &n));
    CHECK(n == 3 && dout[0] == 367 && dout[1] == 45000 && dout[2] == 109574, "a sorted set, as an array in its order");
    nql_free(dout);

    double ov[] = {1.0, 99.0, 3.0};
    bool oh[] = {true, false, true};
    double* ovo = NULL;
    bool* oho = NULL;
    OK(nqlt_Echo_OptDoubles(ov, oh, 3, &ovo, &oho, &n));
    CHECK(n == 3 && oho[0] && !oho[1] && oho[2] && ovo[0] == 1.0 && ovo[2] == 3.0, "nullable doubles: values and has-values");
    nql_free(ovo);
    nql_free(oho);

    nql_period ps[] = {{1, NQL_TimeUnit_Years}, {3, NQL_TimeUnit_Months}};
    nql_period* pout = NULL;
    OK(nqlt_Echo_Periods(ps, 2, &pout, &n));
    CHECK(n == 2 && pout[0].length == 1 && pout[1].units == NQL_TimeUnit_Months, "periods");
    nql_free(pout);

    int32_t es[] = {NQL_TimeUnit_Days, NQL_TimeUnit_Years};
    int32_t* eout = NULL;
    OK(nqlt_Echo_Enums(es, 2, &eout, &n));
    CHECK(n == 2 && eout[0] == NQL_TimeUnit_Days && eout[1] == NQL_TimeUnit_Years, "enums");
    nql_free(eout);

    const char* strs[] = {"a", "\xc3\xa9t\xc3\xa9", ""};
    char** sout = NULL;
    OK(nqlt_Echo_Strings(strs, 3, &sout, &n));
    CHECK(n == 3 && strcmp(sout[1], "\xc3\xa9t\xc3\xa9") == 0 && sout[2][0] == 0, "strings");
    for (int k = 0; k < n; k++) nql_free(sout[k]);
    nql_free(sout);

    nql_complex cs[] = {{1, 2}, {-3, 4}};
    nql_complex* cout = NULL;
    OK(nqlt_Echo_Complexes(cs, 2, &cout, &n));
    CHECK(n == 2 && cout[1].re == -3 && cout[1].im == 4, "complex numbers");
    nql_free(cout);

    double jv[] = {1, 2, 3, 4, 5, 6};
    int32_t jl[] = {2, 0, 4};
    double* jo = NULL;
    int32_t* jlo = NULL;
    int32_t rows = 0;
    OK(nqlt_Echo_Jagged(jv, 6, jl, 3, &jo, &jlo, &rows));
    CHECK(rows == 3 && jlo[0] == 2 && jlo[1] == 0 && jlo[2] == 4 && jo[5] == 6, "a jagged array: values and row lengths");
    nql_free(jo);
    nql_free(jlo);
    int32_t bad[] = {2, 5};
    REFUSED(nqlt_Echo_Jagged(jv, 6, bad, 2, &jo, &jlo, &rows), NQL_ERROR_ARGUMENT);

    nql_date mk[] = {46000, 45000};
    double mv[] = {0.02, 0.01};
    nql_date* mko = NULL;
    double* mvo = NULL;
    OK(nqlt_Echo_Map(mk, mv, 2, &mko, &mvo, &n));
    CHECK(n == 2 && mko[0] == 45000 && mvo[0] == 0.01 && mko[1] == 46000, "a dictionary: keys and values");
    nql_free(mko);
    nql_free(mvo);
    OK(nqlt_Echo_Nodes(mk, 2, mv, 2, &mko, &mvo, &n));
    CHECK(n == 2 && mko[0] == 46000 && mvo[1] == 0.01, "key-value pairs, a curve's nodes' form");
    nql_free(mko);
    nql_free(mvo);

    int32_t whole;
    double frac;
    OK(nqlt_Echo_Split(3.75, &whole, &frac));
    CHECK(whole == 3 && frac == 0.75, "a tuple: one out-parameter per element");

    /* A matrix by rows: 2x3 in, its 3x2 transpose out. */
    double m[] = {1, 2, 3, 4, 5, 6};
    double* mt = NULL;
    int32_t r = 0, c = 0;
    OK(nqlt_Echo_Transpose(m, 2, 3, &mt, &r, &c));
    CHECK(r == 3 && c == 2 && mt[0] == 1 && mt[1] == 4 && mt[2] == 2 && mt[5] == 6, "a matrix in and out by rows");
    nql_free(mt);
    double corr[] = {1.0, 0.5, 0.5, 1.0};
    OK(nql_PseudoSqrtFunctions_PseudoSqrt(corr, 2, 2, NQL_SalvagingAlgorithmType_None, &mt, &r, &c));
    CHECK(r == 2 && c == 2 && fabs(mt[0] * mt[0] + mt[1] * mt[1] - 1.0) < 1e-15 && fabs(mt[0] * mt[2] + mt[1] * mt[3] - 0.5) < 1e-15,
          "PseudoSqrt's matrix by rows: S S^T is the correlation");
    nql_free(mt);
    double v[] = {1.5, -2};
    double* vo = NULL;
    OK(nqlt_Echo_Twice(v, 2, &vo, &n));
    CHECK(n == 2 && vo[0] == 3.0 && vo[1] == -4.0, "a vector in and out");
    nql_free(vo);

    double span[3] = {0, 0, 0};
    OK(nqlt_Echo_Fill(span, 3, 7.5));
    CHECK(span[0] == 7.5 && span[2] == 7.5, "a writable span, written in place");

    /* A vector the member writes into crosses in and out (R15): a line search's step moves the caller's point. */
    nql_handle box = NULL;
    OK(nql_BoundaryConstraint_new(-1.0, 1.0, &box));
    double point[2] = {0.5, 0.2}, dir[2] = {0.1, -0.3}, step = 0;
    OK(nql_Constraint_Update(box, point, 2, dir, 2, 1.0, &step));
    double* moved = NULL;
    int32_t nm = 0;
    OK(nqlt_References_ConstraintUpdate(&moved, &nm));
    CHECK(step == 1.0 && nm == 2 && memcmp(point, moved, sizeof point) == 0, "the point moved in place, as C# moves it: [%.17g, %.17g]", point[0], point[1]);
    nql_free(moved);
    release(box);

    /* A loss distribution as its values: buckets' edges, widths, densities, cumulative and excess probabilities. */
    nql_handle loss = NULL;
    OK(nql_LossDistBinomial_new(10, 10.0, &loss));
    double *dx0, *dx1, *dens, *cum, *exc, *avg;
    int32_t buckets = 0;
    OK(nql_LossDistBinomial_Evaluate__int(loss, 5, 1.0, 0.3, &dx0, &dx1, &dens, &cum, &exc, &avg, &buckets));
    double total = 0;
    for (int k = 0; k < buckets; k++) total += dens[k] * dx1[k];
    CHECK(buckets == 10 && fabs(total - 1.0) < 1e-12 && exc[0] == 1.0, "a loss distribution's normalized values (%d buckets, mass %.17g)", buckets, total);
    nql_free(dx0); nql_free(dx1); nql_free(dens); nql_free(cum); nql_free(exc); nql_free(avg);
    release(loss);

    /* A tuple: one parameter per element, in its own form; NULL for both nullables is C#'s default. */
    double p_in = 0.5, d_in = 0.1, p_out = 0, d_out = 0;
    bool p_has = false, d_has = true;
    OK(nqlt_Tuples_Single(&p_in, &d_in, &p_out, &p_has, &d_out, &d_has));
    CHECK(p_has && d_has && p_out == 0.5 && d_out == 0.1, "a tuple of nullable doubles both ways");
    OK(nqlt_Tuples_Single(NULL, NULL, &p_out, &p_has, &d_out, &d_has));
    CHECK(!p_has && !d_has, "NULL elements: C#'s default tuple, each null");
    char* text = NULL;
    OK(nqlt_Tuples_Scalars(-3, INT64_MAX, true, &text));
    CHECK(strcmp(text, "-3|9223372036854775807|true") == 0, "an int, a long and a flag: %s", text);
    nql_free(text);
    nql_period year = {1, NQL_TimeUnit_Years};
    OK(nqlt_Tuples_Labels("x\xc3\xa9", 45000, year, &text));
    CHECK(strcmp(text, "x\xc3\xa9|45000|1Y") == 0, "a string, a date and a period: %s", text);
    nql_free(text);
    OK(nqlt_Tuples_Labels(NULL, 45000, year, &text));
    CHECK(strncmp(text, "null|", 5) == 0, "a NULL string element is null: %s", text);
    nql_free(text);
    nql_complex z = {1.5, -2.0};
    OK(nqlt_Tuples_Kinds(NQL_Weekday_Friday, z, &text));
    CHECK(strcmp(text, "6|1.5|-2") == 0, "an enum and a complex number: %s", text);
    nql_free(text);

    /* A collection of tuples: one array per element and one count, in and out; objects by handle. */
    nql_handle o1 = NULL, o2 = NULL;
    OK(nqlt_EchoObject_new(1.0, "one", &o1));
    OK(nqlt_EchoObject_new(2.0, "two", &o2));
    nql_handle items[2] = {o1, o2};
    double weights[2] = {0.25, 0.75};
    nql_handle* items_out = NULL;
    double* weights_out = NULL;
    OK(nqlt_Tuples_Rows(items, weights, 2, &items_out, &weights_out, &n));
    bool same1 = false, same2 = false;
    CHECK(n == 2 && weights_out[0] == 0.25 && weights_out[1] == 0.75, "the weights back in order");
    if (n == 2) {
        OK(nql_equals(items_out[0], o1, &same1));
        OK(nql_equals(items_out[1], o2, &same2));
        release(items_out[0]);
        release(items_out[1]);
    }
    CHECK(same1 && same2, "the objects back, each a new handle to the same object");
    nql_free(items_out);
    nql_free(weights_out);
    OK(nqlt_Tuples_Rows(items, weights, 0, &items_out, &weights_out, &n));
    CHECK(n == 0, "an empty collection of tuples");
    nql_free(items_out);
    nql_free(weights_out);
    release(o1);
    release(o2);

    /* Nullable elements with their flags; NULL first array: a null collection; arrays of no rows: an empty one. */
    double seconds[3] = {2.5, 0, -1.0};
    bool seconds_has[3] = {true, false, true};
    int32_t thirds[3] = {7, 8, 0};
    bool thirds_has[3] = {true, true, false};
    nql_date whens[3] = {45000, 0, 45001};
    OK(nqlt_Tuples_Describe(seconds, seconds_has, thirds, thirds_has, whens, 3, &text));
    CHECK(strcmp(text, "2.5:7:45000;-:8:-;-1:-:45001") == 0, "nullable elements by their flags: %s", text);
    nql_free(text);
    OK(nqlt_Tuples_Describe(NULL, NULL, NULL, NULL, NULL, 0, &text));
    CHECK(strcmp(text, "null") == 0, "a NULL first array is a null collection: %s", text);
    nql_free(text);
    OK(nqlt_Tuples_Describe(seconds, seconds_has, thirds, thirds_has, whens, 0, &text));
    CHECK(strcmp(text, "") == 0, "arrays of no rows are an empty collection: '%s'", text);
    nql_free(text);
    REFUSED(nqlt_Tuples_Describe(seconds, seconds_has, NULL, NULL, whens, 3, &text), NQL_ERROR_ARGUMENT_NULL);
    CHECK(strcmp(nql_last_error_param(), "rows") == 0, "a later element's NULL array is refused, naming the parameter: %s", nql_last_error_param());

    /* Strings and enums, periods with a long and a flag (an element named count takes an underscore), complex numbers
       with dates: each collection back as given. */
    const char* names[2] = {"a", NULL};
    int32_t days[2] = {NQL_Weekday_Monday, NQL_Weekday_Sunday};
    char** names_out = NULL;
    int32_t* days_out = NULL;
    OK(nqlt_Tuples_Days(names, days, 2, &names_out, &days_out, &n));
    CHECK(n == 2 && strcmp(names_out[0], "a") == 0 && names_out[1] == NULL && days_out[0] == NQL_Weekday_Monday && days_out[1] == NQL_Weekday_Sunday,
          "strings, a NULL among them, and enums");
    if (n == 2) {
        nql_free(names_out[0]);
    }
    nql_free(names_out);
    nql_free(days_out);
    nql_period tenors[2] = {{3, NQL_TimeUnit_Months}, {2, NQL_TimeUnit_Years}};
    int64_t counts[2] = {INT64_MIN, 5};
    bool flags[2] = {false, true};
    nql_period* tenors_out = NULL;
    int64_t* counts_out = NULL;
    bool* flags_out = NULL;
    OK(nqlt_Tuples_Terms(tenors, counts, flags, 2, &tenors_out, &counts_out, &flags_out, &n));
    CHECK(n == 2 && tenors_out[1].length == 2 && tenors_out[1].units == NQL_TimeUnit_Years && counts_out[0] == INT64_MIN && !flags_out[0] && flags_out[1],
          "periods, longs and flags");
    nql_free(tenors_out);
    nql_free(counts_out);
    nql_free(flags_out);
    nql_complex zs[2] = {{1.0, 2.0}, {-0.0, NAN}};
    nql_date serials[2] = {45000, 0};
    nql_complex* zs_out = NULL;
    nql_date* dates_out = NULL;
    OK(nqlt_Tuples_Numbers(zs, serials, 2, &zs_out, &dates_out, &n));
    CHECK(n == 2 && zs_out[0].im == 2.0 && bits(zs_out[1].re) == bits(-0.0) && isnan(zs_out[1].im) && dates_out[0] == 45000 && dates_out[1] == 0,
          "complex numbers and dates, a null date as 0");
    nql_free(zs_out);
    nql_free(dates_out);

    /* A returned collection of nullable tuples: one array per element, then the has-value flags, then the count. */
    int32_t lefts[4] = {1, 2, 3, 4}, rights[4] = {10, 20, 30, 40};
    int32_t *lefts_out = NULL, *rights_out = NULL;
    bool* has_out = NULL;
    OK(nqlt_Tuples_Gaps(lefts, rights, 4, 2, &lefts_out, &rights_out, &has_out, &n));
    CHECK(n == 4 && !has_out[0] && has_out[1] && !has_out[2] && has_out[3] && lefts_out[1] == 2 && rights_out[3] == 40,
          "a null tuple by its flag, the others' elements");
    nql_free(lefts_out);
    nql_free(rights_out);
    nql_free(has_out);
}

/* ---- every status code, and the last error's parts ---- */
static void test_codes(void)
{
    double d;
    static const int kinds[][2] = {
        {1, NQL_ERROR}, {2, NQL_ERROR_ARGUMENT}, {3, NQL_ERROR_ARGUMENT_NULL}, {4, NQL_ERROR_ARGUMENT_RANGE},
        {5, NQL_ERROR_INVALID_OPERATION}, {6, NQL_ERROR_INVALID_CAST}, {7, NQL_ERROR_NOT_SUPPORTED},
        {8, NQL_ERROR_KEY_NOT_FOUND}, {11, NQL_ERROR_OUT_OF_MEMORY}, {12, NQL_ERROR_IO}, {13, NQL_ERROR_FORMAT},
        {14, NQL_ERROR_IO}, {15, NQL_ERROR_FORMAT}, {0, NQL_ERROR_INTERNAL},
    };
    for (size_t k = 0; k < sizeof kinds / sizeof kinds[0]; k++) {
        nql_status s = nqlt_Refusals_Throw(kinds[k][0], &d);
        CHECK(s == kinds[k][1] && nql_last_error_code() == kinds[k][1], "kind %d: status %d, expected %d", kinds[k][0], s, kinds[k][1]);
    }

    REFUSED(nqlt_Refusals_Throw(2, &d), NQL_ERROR_ARGUMENT);
    CHECK(strcmp(nql_last_error_param(), "x") == 0, "the parameter .NET names");
    CHECK(strcmp(nql_last_error_type(), "System.ArgumentException") == 0, "the .NET type");
    CHECK(strstr(nql_last_error_message(), "a bad argument") != NULL, "the message");

    /* A documented IndexOutOfRangeException maps by the member's documentation; undocumented, it is a defect's. */
    REFUSED(nqlt_Refusals_DocumentedElement(5, &d), NQL_ERROR_ARGUMENT_RANGE);
    REFUSED(nqlt_Refusals_UndocumentedElement(5, &d), NQL_ERROR_INTERNAL);

    /* A NULL self is refused as the parameter self, never an internal error. */
    double v;
    REFUSED(nqlt_EchoObject_get_Value(NULL, &v), NQL_ERROR_ARGUMENT_NULL);
    CHECK(strcmp(nql_last_error_param(), "self") == 0, "the parameter self");

    /* nanoql's own check of a null argument passes .NET's parameter name through: FlatForward's dayCounter is dc. */
    nql_handle curve = NULL;
    REFUSED(nql_FlatForward_new(45000, 0.03, NULL, NQL_Compounding_Continuous, NQL_Frequency_Annual, &curve), NQL_ERROR_ARGUMENT_NULL);

    /* A conventions file: missing, 12; malformed, 13. */
    nql_handle snap = NULL;
    REFUSED(nql_ConventionsXml_Read__string("no-such-conventions.xml", &snap), NQL_ERROR_IO);
    FILE* f = fopen("malformed-conventions.xml", "wb");
    fputs("<conventions xmlns='https://nanoql.org/schemas/conventions'><unclosed></conventions>", f);
    fclose(f);
    REFUSED(nql_ConventionsXml_Read__string("malformed-conventions.xml", &snap), NQL_ERROR_FORMAT);
    remove("malformed-conventions.xml");

    /* A convention set's unknown name: the set's own check of its argument. */
    nql_handle cal = NULL;
    REFUSED(nql_ConventionSet_Calendar(NULL, "NoSuchCalendar", &cal), NQL_ERROR_ARGUMENT);
    CHECK(strcmp(nql_last_error_param(), "name") == 0, "the lookup's parameter, name");
}

/* ---- handles: ownership, refusals, retirement, a second library's ---- */
typedef nql_status (*echo_new_fn)(double, const char*, nql_handle*);

static char second_error[512]; /* why the second copy didn't load */

static void* load_second_library(const char* copy_name)
{
#if defined(_WIN32)
    char self[MAX_PATH];
    HMODULE first = GetModuleHandleA("nanoql.dll");
    GetModuleFileNameA(first, self, MAX_PATH);
    char* slash = strrchr(self, '\\');
    char copy[MAX_PATH];
    snprintf(copy, sizeof copy, "%.*s\\%s", (int)(slash - self), self, copy_name);
    CopyFileA(self, copy, FALSE);
    HMODULE lib = LoadLibraryA(copy);
    if (!lib) snprintf(second_error, sizeof second_error, "LoadLibrary's error %lu", GetLastError());
    return (void*)lib;
#else
    Dl_info info;
    dladdr((void*)&nql_release, &info);
    char copy[4096];
    const char* slash = strrchr(info.dli_fname, '/');
    snprintf(copy, sizeof copy, "%.*s/%s", (int)(slash - info.dli_fname), info.dli_fname, copy_name);
    char cmd[8300];
    snprintf(cmd, sizeof cmd, "cp '%s' '%s'", info.dli_fname, copy);
    if (system(cmd) != 0) {
        snprintf(second_error, sizeof second_error, "copying the library failed");
        return NULL;
    }
    void* lib = dlopen(copy, RTLD_NOW | RTLD_LOCAL);
    if (!lib) snprintf(second_error, sizeof second_error, "%s", dlerror());
    return lib;
#endif
}

static void* symbol(void* lib, const char* name)
{
#if defined(_WIN32)
    return (void*)GetProcAddress((HMODULE)lib, name);
#else
    return dlsym(lib, name);
#endif
}

static void test_handles(void)
{
    int64_t live0 = -1, live;
    OK(nql_live_handles(&live0));
    nql_handle a = NULL, a2 = NULL, scaled = NULL;
    OK(nqlt_EchoObject_new(2.0, "two", &a));
    OK(nql_retain(a, &a2));
    CHECK(a2 != a, "a second owner is a new handle");
    bool eq = false;
    OK(nql_equals(a, a2, &eq));
    CHECK(eq, "to the same object");
    char* tn = NULL;
    OK(nql_type_name(a, &tn));
    CHECK(strcmp(tn, "NanoQL.Bind.TestTypes.EchoObject") == 0, "the object's .NET type: %s", tn);
    nql_free(tn);
    char* text = NULL;
    OK(nql_to_string(a, &text));
    CHECK(strstr(text, "two") != NULL, "its text");
    nql_free(text);
    int32_t h1, h2;
    OK(nql_hash(a, &h1));
    OK(nql_hash(a2, &h2));
    CHECK(h1 == h2, "equal objects, equal hashes in one process");
    OK(nqlt_EchoObject_Scaled(a, 3.0, &scaled));
    double v;
    OK(nqlt_EchoObject_get_Value(scaled, &v));
    CHECK(v == 6.0, "a new object");
    OK(nqlt_EchoObject_get_Value(a, &v));
    CHECK(v == 2.0, "the first unchanged: immutable");
    OK(nql_live_handles(&live));
    CHECK(live == live0 + 3, "three live handles");

    /* Released, double release, stale: refused, never followed. */
    OK(nql_release(a2));
    REFUSED(nqlt_EchoObject_get_Value(a2, &v), NQL_ERROR_HANDLE);
    REFUSED(nql_release(a2), NQL_ERROR_HANDLE);
    nql_handle reuse = NULL;
    OK(nqlt_EchoObject_new(9.0, "nine", &reuse));
    REFUSED(nqlt_EchoObject_get_Value(a2, &v), NQL_ERROR_HANDLE);
    /* Never issued: another tag. */
    REFUSED(nqlt_EchoObject_get_Value((nql_handle)(uintptr_t)0x0001000000000001ull, &v), NQL_ERROR_HANDLE);
    /* The wrong type. */
    nql_handle cal = set_lookup(nql_ConventionSet_Calendar, "TARGET");
    REFUSED(nqlt_EchoObject_get_Value(cal, &v), NQL_ERROR_TYPE);
    nql_handle objs[] = {a, cal};
    nql_handle* out = NULL;
    int32_t n;
    REFUSED(nqlt_Echo_Objects(objs, 2, &out, &n), NQL_ERROR_TYPE);
    /* An array of objects out: each handle the caller's. */
    nql_handle two[] = {a, scaled};
    OK(nqlt_Echo_Objects(two, 2, &out, &n));
    CHECK(n == 2 && out[0] != a, "an array of new handles");
    for (int k = 0; k < n; k++) release(out[k]);
    nql_free(out);

    /* A slot retired before its generation wraps: the test build's 8 bits, 256 handles to one slot. Slots are reused
       first in, first out, so the free ones are taken first, leaving one free slot, which every turn then reuses. */
    int32_t gen_bits = nqlt_generation_bits();
    CHECK(gen_bits == 8, "the test build's generations are 8 bits wide");
    enum { HELD = 4096 };
    static nql_handle held[HELD];
    for (int k = 0; k < HELD; k++) OK(nqlt_EchoObject_new(0.0, "held", &held[k]));
    nql_handle first = NULL, h = NULL;
    OK(nqlt_EchoObject_new(1.0, "churn", &first));
    uintptr_t slot = (uintptr_t)first & 0xFFFFFF;
    OK(nql_release(first));
    int same_slot = 1;
    for (int k = 1; k < 256; k++) {
        OK(nqlt_EchoObject_new(1.0, "churn", &h));
        same_slot &= ((uintptr_t)h & 0xFFFFFF) == slot;
        OK(nql_release(h));
    }
    CHECK(same_slot, "one free slot is reused every turn");
    OK(nqlt_EchoObject_new(1.0, "after", &h));
    CHECK(((uintptr_t)h & 0xFFFFFF) != slot, "the retired slot isn't reused");
    REFUSED(nqlt_EchoObject_get_Value(first, &v), NQL_ERROR_HANDLE);
    release(h);
    for (int k = 0; k < HELD; k++) release(held[k]);

    /* A second nanoql library in the process: its handle is refused by its tag, never resolved to this library's. glibc
       may refuse a second copy the static TLS a library loaded late draws on; one library per process is the rule
       (BIR9), so that refusal is reported and the foreign checks skipped (R21). */
    void* second = load_second_library("nanoql-second.dll");
    if (!second && strstr(second_error, "static TLS")) {
        printf("the second copy isn't loaded (%s): the foreign checks are skipped\n", second_error);
    } else {
        CHECK(second != NULL, "a second copy of the library loads%s%s", second_error[0] ? ": " : "", second_error);
    }
    if (second) {
        echo_new_fn new2 = (echo_new_fn)symbol(second, "nqlt_EchoObject_new");
        int32_t (*tag2)(void) = (int32_t (*)(void))symbol(second, "nqlt_handle_tag");
        nql_status (*release2)(nql_handle) = (nql_status (*)(nql_handle))symbol(second, "nql_release");
        nql_handle foreign = NULL;
        CHECK(new2(5.0, "foreign", &foreign) == NQL_OK, "the second library issues a handle");
        if (tag2() != nqlt_handle_tag()) {
            REFUSED(nqlt_EchoObject_get_Value(foreign, &v), NQL_ERROR_HANDLE);
        } else {
            printf("the two libraries drew the same tag (1 in 65,535): the foreign check is skipped\n");
        }
        release2(foreign);
    }

    release(a);
    release(scaled);
    release(reuse);
    release(cal);
    OK(nql_live_handles(&live));
    CHECK(live == live0, "every handle released: %lld live, %lld at the start", (long long)live, (long long)live0);
}

/* ---- generators: snapshots drawn in batches ---- */
typedef struct {
    nql_handle snapshot;
    double first[1000];
    nql_status status;
} draw_job;

#if defined(_WIN32)
static DWORD WINAPI draw_thread(LPVOID p)
#else
static void* draw_thread(void* p)
#endif
{
    draw_job* job = (draw_job*)p;
    for (int round = 0; round < 50; round++) {
        double *v = NULL, *w = NULL;
        nql_handle next = NULL;
        job->status |= nql_MersenneTwisterUniformRng_Draws(job->snapshot, 1000, &v, &w, &next);
        if (round == 0 && v) memcpy(job->first, v, sizeof job->first);
        else if (v && memcmp(job->first, v, sizeof job->first) != 0) job->status = -1;
        nql_free(v);
        nql_free(w);
        nql_release(next);
    }
    return 0;
}

static void test_generators(void)
{
    int64_t live0, live;
    OK(nql_live_handles(&live0));
    nql_handle mt = NULL;
    OK(nql_MersenneTwisterUniformRng_new__long(42, &mt));
    char* tn = NULL;
    OK(nql_type_name(mt, &tn));
    CHECK(strcmp(tn, "NanoQL.Numerics.MersenneTwisterUniformRng") == 0, "a snapshot names its generator's type: %s", tn);
    nql_free(tn);

    double *v1 = NULL, *w1 = NULL, *v2 = NULL, *w2 = NULL, *all = NULL, *wall = NULL, *rest = NULL, *wrest = NULL;
    nql_handle n1 = NULL, n2 = NULL, nall = NULL, nrest = NULL;
    OK(nql_MersenneTwisterUniformRng_Draws(mt, 1000, &v1, &w1, &n1));
    OK(nql_MersenneTwisterUniformRng_Draws(mt, 1000, &v2, &w2, &n2));
    CHECK(memcmp(v1, v2, 1000 * sizeof(double)) == 0, "the same snapshot gives the same draws");
    CHECK(w1[0] == 1.0 && w1[999] == 1.0, "weights of 1");
    OK(nql_MersenneTwisterUniformRng_Draws(mt, 2000, &all, &wall, &nall));
    OK(nql_MersenneTwisterUniformRng_Draws(n1, 1000, &rest, &wrest, &nrest));
    CHECK(memcmp(all, v1, 1000 * sizeof(double)) == 0 && memcmp(all + 1000, rest, 1000 * sizeof(double)) == 0,
          "a stream continued from next is one call's draws");
    nql_free(v1); nql_free(w1); nql_free(v2); nql_free(w2); nql_free(all); nql_free(wall); nql_free(rest); nql_free(wrest);
    release(n1); release(n2); release(nall); release(nrest);
    REFUSED(nql_MersenneTwisterUniformRng_Draws(mt, -1, &v1, &w1, &n1), NQL_ERROR_ARGUMENT_RANGE);
    v1 = w1 = v2 = w2 = NULL;
    n1 = n2 = NULL;

    /* Two threads draw from one snapshot at once, with no lock: each gets the first draws, every time. */
    draw_job jobs[2] = {{mt, {0}, 0}, {mt, {0}, 0}};
#if defined(_WIN32)
    HANDLE th[2];
    for (int k = 0; k < 2; k++) th[k] = CreateThread(NULL, 0, draw_thread, &jobs[k], 0, NULL);
    WaitForMultipleObjects(2, th, TRUE, INFINITE);
    for (int k = 0; k < 2; k++) CloseHandle(th[k]);
#else
    pthread_t th[2];
    for (int k = 0; k < 2; k++) pthread_create(&th[k], NULL, draw_thread, &jobs[k]);
    for (int k = 0; k < 2; k++) pthread_join(th[k], NULL);
#endif
    OK(nql_MersenneTwisterUniformRng_Draws(mt, 1000, &v1, &w1, &n1));
    CHECK(jobs[0].status == 0 && jobs[1].status == 0 && memcmp(jobs[0].first, v1, sizeof jobs[0].first) == 0
          && memcmp(jobs[1].first, v1, sizeof jobs[1].first) == 0, "two threads, one snapshot: the same draws");
    nql_free(v1); nql_free(w1); release(n1);

    /* A sequence generator: draws by rows, its dimension out. */
    nql_handle sobol = NULL;
    OK(nql_SobolRsg_new(5, 1, NQL_SobolRsgDirectionIntegers_Jaeckel, true, &sobol));
    int32_t dim = 0;
    OK(nql_SobolRsg_Draws(sobol, 3, &v1, &w1, &dim, &n1));
    CHECK(dim == 5 && v1[0] == 0.5 && v1[4] == 0.5, "Sobol's first point, by rows (dimension %d)", dim);
    nql_free(v1); nql_free(w1); release(n1);

    /* A user's generator, implementing IRng unmarked: kept off handles too, a snapshot drawn in batches. */
    nql_handle counting = NULL;
    OK(nqlt_CountingRng_new(10.0, 0.5, &counting));
    OK(nqlt_CountingRng_Draws(counting, 3, &v1, &w1, &n1));
    CHECK(v1[0] == 10.0 && v1[2] == 11.0, "the user's generator draws from a snapshot");
    OK(nqlt_CountingRng_Draws(n1, 1, &v2, &w2, &n2));
    CHECK(v2[0] == 11.5, "and continues from next");
    double val;
    REFUSED(nqlt_EchoObject_get_Value(counting, &val), NQL_ERROR_TYPE);
    nql_free(v1); nql_free(w1); nql_free(v2); nql_free(w2);
    release(n1); release(n2);

    /* A closed generator construction as a parameter takes a deriving class's snapshot, copied; another is refused. */
    nql_handle uniform = NULL, gaussian = NULL, knuth = NULL, wrong = NULL;
    OK(nql_MersenneTwisterUniformRsg_new__int_long(3, 42, &uniform));
    OK(nql_InvCumulativeMersenneTwisterGaussianRsg_new__RandomSequenceGeneratorMersenneTwisterUniformRng(uniform, &gaussian));
    OK(nql_InvCumulativeMersenneTwisterGaussianRsg_Draws(gaussian, 4, &v1, &w1, &dim, &n1));
    double* expected = NULL;
    int32_t ne = 0;
    OK(nqlt_References_GaussianDraws(&expected, &ne));
    CHECK(dim == 3 && ne == 12 && memcmp(v1, expected, 12 * sizeof(double)) == 0, "the Gaussian draws over the uniform snapshot, as C#'s");
    nql_free(expected); nql_free(v1); nql_free(w1); release(n1);
    OK(nql_KnuthUniformRsg_new__int_long(3, 42, &knuth));
    REFUSED(nql_InvCumulativeMersenneTwisterGaussianRsg_new__RandomSequenceGeneratorMersenneTwisterUniformRng(knuth, &wrong), NQL_ERROR_TYPE);
    release(knuth); release(gaussian); release(uniform);
    release(counting); release(sobol); release(mt);
    OK(nql_live_handles(&live));
    CHECK(live == live0, "every handle released");
}

/* ---- versions, today's date, the deprecated member's mark ---- */
static void test_misc(void)
{
    CHECK(nql_abi_version() == NQL_ABI_VERSION, "the library's ABI version is the header's: %d", nql_abi_version());
    CHECK(strcmp(nql_version(), NQL_VERSION) == 0, "the version: %s", nql_version());
    CHECK(NQL_ABI_VERSION == NQL_VERSION_MAJOR * 1000 + NQL_VERSION_MINOR, "major times 1,000 plus minor");

    nql_date today = 0;
    OK(nql_Date_TodaysDate(&today));
    CHECK(today > 46000 && today < 109574, "today's date from the computer's clock: %d", today);

    FILE* h = fopen(NQLT_HEADER, "rb");
    CHECK(h != NULL, "the test header %s", NQLT_HEADER);
    if (h) {
        static char text[1 << 20];
        size_t n = fread(text, 1, sizeof text - 1, h);
        text[n] = 0;
        fclose(h);
        CHECK(strstr(text, "NQL_DEPRECATED(\"use Echo.Double\") NQL_API nql_status nqlt_Deprecations_Old(") != NULL,
              "a deprecated member is declared NQL_DEPRECATED with its message");
    }

    /* A static stays under its declaring type: no named latent model carries a copy of LatentModel's factory, whose
       own function builds the latent model. */
#if defined(_WIN32)
    void* self = (void*)GetModuleHandleA("nanoql.dll");
#else
    Dl_info info;
    dladdr((void*)&nql_release, &info);
    void* self = dlopen(info.dli_fname, RTLD_NOW | RTLD_NOLOAD);
#endif
    CHECK(self != NULL, "the library itself");
    CHECK(self && symbol(self, "nql_LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits_FromCorrelation") != NULL
              && symbol(self, "nql_GaussianDefProbLM_FromCorrelation") == NULL && symbol(self, "nql_Brent_get_MaxFunctionEvaluations") == NULL,
          "the declaring closing's factory, and no copy under a derived type");
#if !defined(_WIN32)
    if (self) dlclose(self);
#endif
    nql_handle lm = NULL;
    OK(nql_LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits_FromCorrelation(0.3, 2, NULL, &lm));
    char* tn = NULL;
    OK(nql_type_name(lm, &tn));
    CHECK(strncmp(tn, "NanoQL.Experimental.LatentModel`2", 33) == 0, "the factory builds its declaring type: %s", tn);
    nql_free(tn);
    release(lm);
}

/* ---- conventions by name: the set, NULL as the current provider ---- */
static void test_conventions(void)
{
    nql_handle quantlib = get(nql_ConventionSet_get_QuantLib);
    nql_handle a = NULL, b = NULL;
    OK(nql_ConventionSet_DayCounter(quantlib, "Actual365Fixed", &a));
    OK(nql_ConventionSet_DayCounter(NULL, "Actual365Fixed", &b));
    bool eq = false;
    OK(nql_equals(a, b, &eq));
    CHECK(eq, "NULL is the current provider read as a set: QuantLib's");
    int32_t ha = 0, hb = 0;
    OK(nql_hash(a, &ha));
    OK(nql_hash(b, &hb));
    CHECK(a != b && ha == hb, "two handles to equal objects, each looked up apart: equal hashes");
    char* name = NULL;
    OK(nql_DayCounter_get_Name(a, &name));
    CHECK(strcmp(name, "Actual/365 (Fixed)") == 0, "the day counter's name: %s", name);
    nql_free(name);
    release(a);
    release(b);
    release(quantlib);

    /* A user's provider as the default: a NULL set has no names to look up (5); QuantLib's again, and it has. */
    nql_handle user = NULL, ql = get(nql_ConventionProvider_get_QuantLib), c = NULL;
    OK(nqlt_UserProvider_new(&user));
    OK(nql_ConventionProvider_set_Default(user));
    REFUSED(nql_ConventionSet_DayCounter(NULL, "Actual365Fixed", &c), NQL_ERROR_INVALID_OPERATION);
    OK(nql_ConventionProvider_set_Default(ql));
    OK(nql_ConventionSet_DayCounter(NULL, "Actual365Fixed", &c));
    release(c);
    release(user);
    release(ql);

    /* The presets that take arguments, each by its lookup with them; the two lookups by name alone, renamed. */
    nql_handle idx[7] = {NULL};
    nql_handle usd = set_lookup(nql_ConventionSet_Currency, "USD");
    nql_period lag = {3, NQL_TimeUnit_Months};
    OK(nql_ConventionSet_IborIndex__string_int(NULL, "DailyTenorUSDLibor", 2, NULL, &idx[0]));
    OK(nql_ConventionSet_InterestRateIndex(NULL, "BMAIndex", NULL, &idx[1]));
    OK(nql_ConventionSet_ZeroInflationIndex__string_Frequency_bool_ZeroInflationTermStructure(NULL, "AUCPI", NQL_Frequency_Quarterly, false, NULL, &idx[2]));
    OK(nql_ConventionSet_ZeroInflationIndex__string_Frequency_bool_Period(NULL, "GenericCPI", NQL_Frequency_Monthly, true, lag, usd, NULL, &idx[3]));
    OK(nql_ConventionSet_YoYInflationIndex__string_Frequency_bool_YoYInflationTermStructure(NULL, "YYAUCPI", NQL_Frequency_Quarterly, false, NULL, &idx[4]));
    OK(nql_ConventionSet_YoYInflationIndex__string_Frequency_bool_Period(NULL, "YYGenericCPI", NQL_Frequency_Monthly, true, lag, usd, NULL, &idx[5]));
    OK(nql_ConventionSet_ZeroInflationIndex__string_ZeroInflationTermStructure(NULL, "UKRPI", NULL, &idx[6]));
    static const char* types[7] = {"NanoQL.Indexes.DailyTenorUSDLibor", "NanoQL.Indexes.BMAIndex", "NanoQL.Indexes.AUCPI", "NanoQL.Experimental.GenericCPI",
                                   "NanoQL.Indexes.YYAUCPI", "NanoQL.Experimental.YYGenericCPI", "NanoQL.Indexes.UKRPI"};
    for (int k = 0; k < 7; k++) {
        char* tn = NULL;
        OK(nql_type_name(idx[k], &tn));
        CHECK(tn && strcmp(tn, types[k]) == 0, "a preset by its lookup: %s", tn ? tn : "(none)");
        nql_free(tn);
    }
    int32_t days = 0, frequency = 0;
    OK(nql_InterestRateIndex_get_FixingDays(idx[0], &days));
    OK(nql_InflationIndex_get_Frequency(idx[2], &frequency));
    CHECK(days == 2 && frequency == NQL_Frequency_Quarterly, "each built with its arguments");
    nql_handle yy = NULL, wrong = NULL;
    OK(nql_ConventionSet_YoYInflationIndex__string_YoYInflationTermStructure(NULL, "YYUKRPI", NULL, &yy));
    REFUSED(nql_ConventionSet_IborIndex__string_int(NULL, "Euribor6M", 2, NULL, &wrong), NQL_ERROR_ARGUMENT);
    for (int k = 0; k < 7; k++) release(idx[k]);
    release(yy);
    release(usd);
}

/* An optional struct left out: NULL is its default, what C# passes (R16); its implicit constructor crosses too. */
static void test_defaults(void)
{
    nql_handle ctx = get(nqlt_Fixtures_get_Context), curve = NULL, explicit_curve = NULL, i = NULL, bootstrap = NULL;
    nql_handle cal = set_lookup(nql_ConventionSet_Calendar, "TARGET"), dc = set_lookup(nql_ConventionSet_DayCounter, "Actual365Fixed");
    nql_handle* helpers = NULL;
    int32_t n = 0;
    OK(nqlt_Fixtures_get_DepositHelpers(&helpers, &n));
    CHECK(n == 3, "three deposits");
    OK(nql_PiecewiseFlatForward_Create(0, cal, helpers, n, dc, ctx, NULL, 0, NULL, 0, NULL, NULL, &curve));
    double d = 0, reference = 0, e = 0;
    OK(nql_YieldTermStructure_Discount__double(curve, 2.0, false, &d));
    OK(nqlt_References_PiecewiseDiscount(&reference));
    SAME(d, reference);
    OK(nql_BackwardFlat_new(&i));
    OK(nql_IterativeBootstrap_new__void(&bootstrap));
    OK(nql_PiecewiseFlatForward_Create(0, cal, helpers, n, dc, ctx, NULL, 0, NULL, 0, i, bootstrap, &explicit_curve));
    OK(nql_YieldTermStructure_Discount__double(explicit_curve, 2.0, false, &e));
    SAME(e, reference);

    /* A required struct stays refused when NULL. */
    nql_handle none = NULL;
    REFUSED(nqlt_Echo_Money(NULL, &none), NQL_ERROR_ARGUMENT_NULL);
    for (int32_t k = 0; k < n; k++) release(helpers[k]);
    nql_free(helpers);
    release(ctx);
    release(curve);
    release(explicit_curve);
    release(i);
    release(bootstrap);
    release(cal);
    release(dc);
}

/* ---- bound engines ---- */
static void test_engines(void)
{
    int64_t live0, live;
    OK(nql_live_handles(&live0));
    nql_handle ctx = get(nqlt_Fixtures_get_Context);
    nql_handle rf = get(nqlt_Fixtures_get_RiskFree);

    /* The swap engine bound with its curve; MakeVanillaSwap picks its swap calculation and solves its fixed rate. */
    nql_handle swap_engine = NULL, builder = get(nqlt_Fixtures_get_SwapBuilder), with = NULL, swap = NULL, results = NULL;
    OK(nql_DiscountingSwapEngine_Bind(rf, NULL, 0, 0, &swap_engine));
    OK(nql_MakeVanillaSwap_WithPricingEngine(builder, swap_engine, &with));
    OK(nql_MakeVanillaSwap_ToVanillaSwap(with, ctx, &swap));
    double rate = 0, reference = 0;
    OK(nql_FixedVsFloatingSwap_get_FixedRate(swap, &rate));
    OK(nqlt_References_SwapFixedRate(&reference));
    SAME(rate, reference);
    OK(nql_DiscountingSwapEngine_Calculate__FixedVsFloatingSwap(swap, ctx, rf, NULL, 0, 0, &results));
    CHECK(fabs(npv(results)) < 1e-6, "the solved swap prices at par through the direct engine");
    release(results);

    /* A bound engine of the wrong shape: the Heston engine where a swap's calculation is needed. */
    nql_handle heston = get(nqlt_Fixtures_get_Heston), heston_engine = NULL, refused = NULL;
    OK(nql_AnalyticHestonEngine_Bind__HestonModel_int(NULL, NULL, &heston_engine));
    REFUSED(nql_MakeVanillaSwap_WithPricingEngine(builder, heston_engine, &refused), NQL_ERROR_TYPE);

    /* The Heston helper on the bound analytic engine, the model left open: the trial model goes into it. */
    nql_handle helper = get(nqlt_Fixtures_get_HestonHelper), priced = NULL;
    double value = 0;
    OK(nql_HestonModelHelper_SetPricingEngine(helper, heston_engine, &priced));
    OK(nql_HestonModelHelper_ModelValue(priced, heston, ctx, &value));
    OK(nqlt_References_HestonModelValue(&reference));
    SAME(value, reference);
    release(priced);

    /* The model both bound and supplied: refused when the member takes the engine. */
    nql_handle both = NULL;
    OK(nql_AnalyticHestonEngine_Bind__HestonModel_int(heston, NULL, &both));
    REFUSED(nql_HestonModelHelper_SetPricingEngine(helper, both, &priced), NQL_ERROR_TYPE);
    release(both);
    /* An engine without the member's calculation: the swap engine on the Heston helper. */
    REFUSED(nql_HestonModelHelper_SetPricingEngine(helper, swap_engine, &priced), NQL_ERROR_TYPE);

    /* The Heston–Hull-White engines: the trial model by the roles table's name, the Hull-White model bound. */
    nql_handle hw = get(nqlt_Fixtures_get_HullWhite), h1hw = NULL, hhw = NULL, open = NULL;
    double rho = 0.6;
    OK(nql_AnalyticH1HWEngine_Bind__HestonModel_HullWhite_double_int(NULL, hw, &rho, NULL, &h1hw));
    OK(nql_HestonModelHelper_SetPricingEngine(helper, h1hw, &priced));
    OK(nql_HestonModelHelper_ModelValue(priced, heston, ctx, &value));
    OK(nqlt_References_H1HWModelValue(&reference));
    SAME(value, reference);
    release(priced);
    OK(nql_AnalyticHestonHullWhiteEngine_Bind__HestonModel_HullWhite_int(NULL, hw, NULL, &hhw));
    OK(nql_HestonModelHelper_SetPricingEngine(helper, hhw, &priced));
    OK(nql_HestonModelHelper_ModelValue(priced, heston, ctx, &value));
    OK(nqlt_References_HestonHullWhiteModelValue(&reference));
    SAME(value, reference);
    release(priced);
    /* The Hull-White model left open: neither bound nor supplied. */
    OK(nql_AnalyticH1HWEngine_Bind__HestonModel_HullWhite_double_int(NULL, NULL, &rho, NULL, &open));
    REFUSED(nql_HestonModelHelper_SetPricingEngine(helper, open, &priced), NQL_ERROR_TYPE);
    release(open);

    /* The quanto engine supplies the process to the bound European engine, its process left open. */
    nql_handle european = NULL, quanto = get(nqlt_Fixtures_get_Quanto), bsm = get(nqlt_Fixtures_get_BlackScholes);
    nql_handle foreign = get(nqlt_Fixtures_get_Foreign), fxvol = get(nqlt_Fixtures_get_ExchangeVolatility);
    OK(nql_AnalyticEuropeanEngine_Bind__GeneralizedBlackScholesProcess(NULL, &european));
    OK(nql_QuantoEngine_Calculate__QuantoVanillaOption(quanto, ctx, bsm, foreign, fxvol, 0.3, european, &results));
    OK(nqlt_References_QuantoNpv(&reference));
    SAME(npv(results), reference);
    release(results);

    /* A Monte Carlo-shaped engine: its nullable inputs neither bound nor supplied are null, the shape left open by a
       NULL first array takes its default, and a required input left open is refused. */
    nql_handle mc = NULL, mc_shaped = NULL, mc_open = NULL;
    int32_t mc_steps = 4, samples = 2000;
    int64_t seed = 42;
    double times[2] = {0.5, 1.0}, shape_weights[2] = {1.5, 0.5};
    OK(nqlt_McShapedEngine_Bind(NULL, &mc_steps, NULL, &samples, NULL, &seed, NULL, NULL, 0, &mc));
    OK(nql_QuantoEngine_Calculate__QuantoVanillaOption(quanto, ctx, bsm, foreign, fxvol, 0.3, mc, &results));
    OK(nqlt_References_McShapedQuantoNpv(false, &reference));
    SAME(npv(results), reference);
    release(results);
    OK(nqlt_McShapedEngine_Bind(NULL, &mc_steps, NULL, &samples, NULL, &seed, times, shape_weights, 2, &mc_shaped));
    OK(nql_QuantoEngine_Calculate__QuantoVanillaOption(quanto, ctx, bsm, foreign, fxvol, 0.3, mc_shaped, &results));
    OK(nqlt_References_McShapedQuantoNpv(true, &reference));
    SAME(npv(results), reference);
    release(results);
    OK(nqlt_McShapedEngine_Bind(NULL, &mc_steps, NULL, &samples, NULL, NULL, NULL, NULL, 0, &mc_open));
    REFUSED(nql_QuantoEngine_Calculate__QuantoVanillaOption(quanto, ctx, bsm, foreign, fxvol, 0.3, mc_open, &results), NQL_ERROR_TYPE);
    CHECK(strstr(last_message(), "'seed' is neither bound nor supplied") != NULL, "the required seed open: %s", last_message());
    release(mc);
    release(mc_shaped);
    release(mc_open);

    /* The YoY optionlet helper's stripper supplies its trial surface, placed after the bound index. */
    nql_handle market = get(nqlt_YoYMarket_get_Instance), yctx = NULL, index = NULL, nominal = NULL, surface = NULL, yoy = NULL;
    OK(nqlt_YoYMarket_get_Context(market, &yctx));
    OK(nqlt_YoYMarket_get_Index(market, &index));
    OK(nqlt_YoYMarket_get_Nominal(market, &nominal));
    OK(nqlt_YoYMarket_get_Surface(market, &surface));
    OK(nql_YoYInflationUnitDisplacedBlackCapFloorEngine_Bind(index, NULL, nominal, &yoy));
    nql_handle dc = set_lookup(nql_ConventionSet_DayCounter, "Actual365Fixed"), target = set_lookup(nql_ConventionSet_Calendar, "TARGET");
    nql_handle yhelper = NULL, ytrial = NULL;
    nql_period lag = {3, NQL_TimeUnit_Months};
    OK(nql_YoYOptionletHelper_new(1.0, 10000.0, NQL_YoYInflationCapFloorType_Cap, lag, dc, target, 0, index, NQL_CPIInterpolationType_Flat, 0.03, 3, yoy, yctx, &yhelper));
    OK(nql_YoYOptionletHelper_SetTermStructure(yhelper, surface, &ytrial));
    OK(nql_YoYOptionletHelper_ImpliedQuote(ytrial, yctx, &value));
    OK(nqlt_References_YoYImpliedQuote(&reference));
    SAME(value, reference);

    /* The OAS members: the spread goes into the tree engine's parameter named by the role. */
    nql_handle bond = get(nqlt_Fixtures_get_CallableBond), bctx = get(nqlt_Fixtures_get_BondContext), bcurve = get(nqlt_Fixtures_get_BondCurve);
    nql_handle bmodel = get(nqlt_Fixtures_get_BondModel), tree = NULL, black = NULL, black_curve = NULL;
    int32_t steps = 40;
    OK(nql_TreeCallableFixedRateBondEngine_Bind__ShortRateModel_int(bmodel, &steps, NULL, NULL, &tree));
    double price = 0, oas = 0;
    OK(nqlt_References_OasPrice(&price));
    OK(nql_CallableBond_OAS(bond, price, bcurve, dc, NQL_Compounding_Compounded, NQL_Frequency_Semiannual, tree, bctx, 0, 1e-10, 100, 0.0, &oas));
    OK(nqlt_References_Oas(&reference));
    SAME(oas, reference);
    OK(nql_CallableBond_CleanPriceOAS(bond, 0.01, bcurve, dc, NQL_Compounding_Compounded, NQL_Frequency_Semiannual, tree, bctx, 0, &value));
    OK(nqlt_References_CleanPriceOas(&reference));
    SAME(value, reference);
    OK(nql_CallableBond_EffectiveDuration(bond, 0.01, bcurve, dc, NQL_Compounding_Compounded, NQL_Frequency_Semiannual, tree, bctx, 0.0002, &value));
    OK(nqlt_References_EffectiveDuration(&reference));
    SAME(value, reference);
    /* The Black engine has no spread parameter: refused, bound with its volatility and curve or with the curve alone. */
    double vol = 0.2;
    OK(nql_BlackCallableFixedRateBondEngine_Bind__double(&vol, bcurve, &black));
    REFUSED(nql_CallableBond_OAS(bond, price, bcurve, dc, NQL_Compounding_Compounded, NQL_Frequency_Semiannual, black, bctx, 0, 1e-10, 100, 0.0, &oas), NQL_ERROR_TYPE);
    OK(nql_BlackCallableFixedRateBondEngine_Bind__double(NULL, bcurve, &black_curve));
    REFUSED(nql_CallableBond_OAS(bond, price, bcurve, dc, NQL_Compounding_Compounded, NQL_Frequency_Semiannual, black_curve, bctx, 0, 1e-10, 100, 0.0, &oas), NQL_ERROR_TYPE);

    /* The Vanna–Volga engine supplies the process by type and the series by its role's name. */
    nql_handle barrier = NULL, dbo = get(nqlt_Fixtures_get_DoubleBarrier), atm = get(nqlt_Fixtures_get_AtmQuote);
    nql_handle put25 = get(nqlt_Fixtures_get_Put25), call25 = get(nqlt_Fixtures_get_Call25), fx = get(nqlt_Fixtures_get_FxCurve);
    OK(nql_AnalyticDoubleBarrierEngine_Bind(NULL, NULL, &barrier));
    OK(nql_VannaVolgaDoubleBarrierEngine_Calculate(dbo, ctx, barrier, atm, put25, call25, 100.0, fx, fx, false, 0.0, 5, &results));
    OK(nqlt_References_VannaVolgaNpv(&reference));
    SAME(npv(results), reference);
    release(results);

    nql_handle all[] = {
        swap_engine, builder, with, swap, heston, heston_engine, helper, hw, h1hw, hhw, european, quanto, bsm, foreign, fxvol,
        market, yctx, index, nominal, surface, yoy, dc, target, yhelper, ytrial, bond, bctx, bcurve, bmodel, tree, black, black_curve,
        barrier, dbo, atm, put25, call25, fx, ctx, rf,
    };
    for (size_t k = 0; k < sizeof all / sizeof all[0]; k++) release(all[k]);
    OK(nql_live_handles(&live));
    CHECK(live == live0, "every handle released: %lld live", (long long)live - live0);
}

/* ---- calibrations through bound engines: GSR's and Hull-White's, a model the engine can't take ---- */
static void test_calibrations(void)
{
    int64_t live0, live;
    OK(nql_live_handles(&live0));

    /* Andreasen–Huge from a calibration set read back as tuples, by its default optimizer and by BFGS(), whose line
       search the C function leaves at its default. */
    nql_handle* options = NULL;
    double* vols = NULL;
    int32_t nset = 0;
    OK(nqlt_Fixtures_get_CalibrationSet(&options, &vols, &nset));
    CHECK(nset == 9, "nine options with their volatilities: %d", nset);
    nql_handle rf = get(nqlt_Fixtures_get_RiskFree), dividend = get(nqlt_Fixtures_get_Dividend), bfgs = NULL, ah = NULL, ahb = NULL;
    OK(nql_BFGS_new(&bfgs));
    OK(nql_AndreasenHugeVolatilityInterpl_new(options, vols, nset, 100.0, rf, dividend, NQL_AndreasenHugeVolatilityInterplInterpolationType_CubicSpline,
                                             NQL_AndreasenHugeVolatilityInterplCalibrationType_Call, 100, NULL, NULL, NULL, NULL, &ah));
    OK(nql_AndreasenHugeVolatilityInterpl_new(options, vols, nset, 100.0, rf, dividend, NQL_AndreasenHugeVolatilityInterplInterpolationType_CubicSpline,
                                             NQL_AndreasenHugeVolatilityInterplCalibrationType_Call, 100, NULL, NULL, bfgs, NULL, &ahb));
    double lv = 0, lv_ref = 0;
    OK(nql_AndreasenHugeVolatilityInterpl_LocalVol(ah, 1.0, 110.0, &lv));
    OK(nqlt_References_AndreasenHugeLocalVol(false, &lv_ref));
    SAME(lv, lv_ref);
    OK(nql_AndreasenHugeVolatilityInterpl_LocalVol(ahb, 1.0, 110.0, &lv));
    OK(nqlt_References_AndreasenHugeLocalVol(true, &lv_ref));
    SAME(lv, lv_ref);
    for (int k = 0; k < nset; k++) release(options[k]);
    nql_free(options);
    nql_free(vols);
    release(ah); release(ahb); release(bfgs); release(rf); release(dividend);

    nql_handle ctx = get(nqlt_Fixtures_get_CalibrationContext), gsr = get(nqlt_Fixtures_get_Gsr), cal_hw = get(nqlt_Fixtures_get_CalibrationHullWhite);
    nql_handle g1d = NULL, cap_engine = NULL;
    OK(nql_Gaussian1dSwaptionEngine_Bind(NULL, NULL, NULL, NULL, NULL, NULL, NULL, &g1d));
    OK(nql_AnalyticCapFloorEngine_Bind(NULL, NULL, &cap_engine));

    nql_handle* swaptions = NULL;
    int32_t n = 0;
    OK(nqlt_Fixtures_get_Swaptions(&swaptions, &n));
    nql_handle priced[3] = {0};
    for (int k = 0; k < n && k < 3; k++) OK(nql_SwaptionHelper_SetPricingEngine(swaptions[k], g1d, &priced[k]));
    double value = 0;
    double reference = 0;
    OK(nql_SwaptionHelper_ModelValue(priced[1], gsr, ctx, &value));
    OK(nqlt_References_GsrSwaptionModelValue(&reference));
    SAME(value, reference);
    /* A model the engine can't take: the cast's InvalidCastException, code 6, as C#'s helper throws. */
    REFUSED(nql_SwaptionHelper_ModelValue(priced[0], cal_hw, ctx, &value), NQL_ERROR_INVALID_CAST);

    nql_handle* caps = NULL;
    int32_t m = 0;
    OK(nqlt_Fixtures_get_Caps(&caps, &m));
    nql_handle capped[3] = {0};
    for (int k = 0; k < m && k < 3; k++) OK(nql_CapHelper_SetPricingEngine(caps[k], cap_engine, &capped[k]));
    OK(nql_CapHelper_ModelValue(capped[1], cal_hw, ctx, &value));
    OK(nqlt_References_HullWhiteCapModelValue(&reference));
    SAME(value, reference);
    REFUSED(nql_CapHelper_ModelValue(capped[0], gsr, ctx, &value), NQL_ERROR_INVALID_CAST);

    /* The calibrations themselves, to C#'s parameters bit for bit (R14): GSR's iterative volatility calibration on the
       priced swaptions, and Hull-White's on the priced caps through the extension method closed at its constraint. */
    nql_handle method = get(nqlt_Fixtures_get_Method), criteria = get(nqlt_Fixtures_get_Criteria), results = NULL, model = NULL;
    double *got = NULL, *want = NULL;
    int32_t ng = 0, nw = 0;
    OK(nql_Gsr_CalibrateVolatilitiesIterative(gsr, priced, 3, method, criteria, ctx, NULL, NULL, 0, &results));
    OK(nql_CalibrationResults_Gsr_get_Model(results, &model));
    OK(nql_Gsr_Params(model, &got, &ng));
    OK(nqlt_References_GsrVolatilities(&want, &nw));
    CHECK(ng == nw && ng > 0 && memcmp(got, want, sizeof(double) * (size_t)ng) == 0, "GSR's calibrated parameters are C#'s, bit for bit (%d)", ng);
    nql_free(got);
    nql_free(want);
    release(model);
    release(results);
    OK(nql_CalibratedModelExtensions_Calibrate(cal_hw, capped, 3, method, criteria, ctx, NULL, NULL, 0, NULL, 0, &results));
    OK(nql_CalibrationResults_ICalibratedModel_get_Model(results, &model));
    OK(nql_CalibratedModel_Params(model, &got, &ng));
    OK(nqlt_References_HullWhiteCaps(&want, &nw));
    CHECK(ng == nw && ng > 0 && memcmp(got, want, sizeof(double) * (size_t)ng) == 0, "Hull-White's calibrated parameters are C#'s, bit for bit (%d)", ng);
    nql_free(got);
    nql_free(want);
    release(model);
    release(results);
    release(method);
    release(criteria);

    for (int k = 0; k < 3; k++) {
        release(priced[k]);
        release(capped[k]);
    }

    for (int k = 0; k < n; k++) release(swaptions[k]);
    for (int k = 0; k < m; k++) release(caps[k]);
    nql_free(swaptions);
    nql_free(caps);
    release(g1d);
    release(cap_engine);
    release(ctx);
    release(gsr);
    release(cal_hw);
    OK(nql_live_handles(&live));
    CHECK(live == live0, "every handle released");
}

int main(int argc, char** argv)
{
    static const struct {
        const char* name;
        void (*run)(void);
    } sections[] = {
        {"kinds", test_kinds}, {"arrays", test_arrays}, {"codes", test_codes}, {"handles", test_handles},
        {"generators", test_generators}, {"misc", test_misc}, {"conventions", test_conventions}, {"defaults", test_defaults}, {"engines", test_engines},
        {"calibrations", test_calibrations},
    };
    setvbuf(stdout, NULL, _IONBF, 0);
    int ran = 0;
    for (size_t k = 0; k < sizeof sections / sizeof sections[0]; k++) {
        if (argc > 1 && strcmp(argv[1], sections[k].name) != 0) continue;
        int before = failures;
        sections[k].run();
        /* Every section ends with no live handle. */
        int64_t live = -1;
        OK(nql_live_handles(&live));
        CHECK(live == 0, "%s ends with no live handle: %lld live", sections[k].name, (long long)live);
        printf("%s: %s\n", sections[k].name, failures == before ? "passed" : "FAILED");
        ran++;
    }

    printf("%d checks, %d failed, %d sections\n", checks, failures, ran);
    return ran == 0 ? 2 : failures == 0 ? 0 : 1;
}
