#pragma once

#ifdef __cplusplus
extern "C" {
#endif

int endar_llm_start_load(const char * model_path, int n_ctx, int n_threads);
int endar_llm_start_generate(const char * prompt_utf8, int max_tokens);
int endar_llm_get_state(void);
int endar_llm_copy_output(char * buffer, int capacity);
int endar_llm_copy_error(char * buffer, int capacity);
int endar_llm_copy_metrics_json(char * buffer, int capacity);
void endar_llm_cancel(void);
void endar_llm_release(void);

#ifdef __cplusplus
}
#endif
