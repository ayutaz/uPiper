/* Minimal stub for us_text functions required by Flite */
/* This avoids encoding issues with the original us_text.c */

#include "cst_val.h"
#include "cst_features.h"
#include "cst_item.h"
#include "cst_relation.h"
#include "cst_utterance.h"

/* Avoid including us_text.h - define what we need */
typedef struct cst_utterance_struct cst_utterance;

/* Required function stubs */
cst_utterance *us_textanalysis(cst_utterance *u)
{
    /* Minimal implementation - just pass through */
    return u;
}

void us_text_init()
{
    /* No initialization needed for stub */
}

void us_text_deinit()
{
    /* No cleanup needed for stub */
}