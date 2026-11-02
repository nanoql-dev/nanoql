/* SPDX-FileCopyrightText: 2026 The nanoql Authors
   SPDX-License-Identifier: Apache-2.0

   The C replayer's runtime (docs/bindings.md, "The vector suite through each binding"): a trace line's JSON, the
   objects the trace names by id, readers that make each argument in its C form, and checkers that compare each result
   with the recorded one: doubles by their bits, NaN as NaN, objects by their ids, arrays element by element, refusals
   by their codes. nanoql-bind's generated dispatch.c calls them, one handler per C function. */
#ifndef NANOQL_REPLAY_H
#define NANOQL_REPLAY_H

#include <stdbool.h>
#include <stdint.h>

#include "nanoql.h"

typedef enum { JV_NULL, JV_BOOL, JV_NUM, JV_STR, JV_ARR, JV_OBJ } jv_type;

/* A JSON value: a number keeps its text; an array's or object's children follow it (an object's as key, value). */
typedef struct jv {
    jv_type type;
    const char* text;
    int len;
    int count;
    struct jv** items;
} jv;

typedef struct rp rp;

/* A handler: the call's arguments, its recorded result (NULL when it was refused) and its recorded status. It returns
   0 when the replay of the fact stops (an argument it can't make), 1 otherwise. */
typedef int (*replay_handler)(rp* r, const jv* args, const jv* result, int status);

typedef struct {
    const char* name;
    replay_handler handler;
} replay_entry;

extern const replay_entry replay_table[];
extern const int replay_table_count;

/* Whether the fact's replay stops: an argument read can't be made. */
int r_stop(rp* r);

/* Readers: argument i of args, in its C form. An argument the replay can't make (an object the trace marks opaque, or
   an id it doesn't hold) stops the fact; arrays are freed after the call. */
double rd_double(rp* r, const jv* a, int i);
int32_t rd_int(rp* r, const jv* a, int i);
int64_t rd_long(rp* r, const jv* a, int i);
bool rd_bool(rp* r, const jv* a, int i);
const char* rd_str(rp* r, const jv* a, int i);
nql_date rd_date(rp* r, const jv* a, int i);
nql_period rd_period(rp* r, const jv* a, int i);
nql_complex rd_complex(rp* r, const jv* a, int i);
nql_handle rd_obj(rp* r, const jv* a, int i);
const double* rd_optdouble(rp* r, const jv* a, int i, double* store);
const int32_t* rd_optint(rp* r, const jv* a, int i, int32_t* store);
const int64_t* rd_optlong(rp* r, const jv* a, int i, int64_t* store);
const bool* rd_optbool(rp* r, const jv* a, int i, bool* store);
const nql_period* rd_optperiod(rp* r, const jv* a, int i, nql_period* store);
const nql_complex* rd_optcomplex(rp* r, const jv* a, int i, nql_complex* store);

/* Element kinds for arrays and maps. */
enum { K_DOUBLE, K_INT, K_LONG, K_BOOL, K_DATE, K_PERIOD, K_STR, K_OBJ, K_COMPLEX, K_OPTDOUBLE, K_OPTINT, K_OPTDATE };

/* An array of elements of a kind: NULL for null; nullable elements' flags in *has. */
const void* rd_array(rp* r, const jv* a, int i, int kind, const bool** has, int32_t* count);
double* rd_span(rp* r, const jv* a, int i, int kind, int32_t* count);
const double* rd_matrix(rp* r, const jv* a, int i, int32_t* rows, int32_t* columns);
const double* rd_jagged(rp* r, const jv* a, int i, int32_t* count, const int32_t** lengths, int32_t* rows);
void rd_map(rp* r, const jv* a, int i, int kk, int vk, const void** keys, const void** values, const bool** has, int32_t* count);
const jv* rd_tuple(rp* r, const jv* a, int i);
void rd_tuples(rp* r, const jv* a, int i, int n, const int* kinds, const void** items, const bool** has, int32_t* count);

/* The status: 1 when the call succeeded as recorded and its result is to be checked, 0 otherwise (a refusal matched,
   or a mismatch counted). */
int st(rp* r, nql_status s, int recorded);

/* Checkers: the replayed result against the recorded one. Strings and arrays nanoql returned are freed. */
void ck_double(rp* r, const jv* res, double v);
void ck_int(rp* r, const jv* res, int64_t v);
void ck_bool(rp* r, const jv* res, bool v);
void ck_str(rp* r, const jv* res, char* v);
void ck_period(rp* r, const jv* res, nql_period v);
void ck_complex(rp* r, const jv* res, nql_complex v);
void ck_opt(rp* r, const jv* res, bool has, int kind, const void* v);
void ck_obj(rp* r, const jv* res, nql_handle h);
void ck_array(rp* r, const jv* res, int kind, void* p, bool* has, int32_t n);
void ck_matrix(rp* r, const jv* res, double* p, int32_t rows, int32_t columns);
void ck_jagged(rp* r, const jv* res, double* p, int32_t* lengths, int32_t rows);
void ck_map(rp* r, const jv* res, int kk, int vk, void* keys, void* values, bool* has, int32_t n);
void ck_distribution(rp* r, const jv* res, double* x, double* dx, double* density, double* cumulative, double* excess, double* average, int32_t n);
void ck_tuples(rp* r, const jv* res, int n, const int* kinds, void** items, bool* has, int32_t count);
void ck_span(rp* r, int k, double* p, int32_t n);
void ck_span_ints(rp* r, int k, int32_t* p, int32_t n);
const jv* jv_at(const jv* v, int i);

#endif
