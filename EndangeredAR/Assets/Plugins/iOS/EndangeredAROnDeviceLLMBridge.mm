#import <Foundation/Foundation.h>
#import <mach/mach.h>

#include <llama/llama.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#include "EndangeredAROnDeviceLLMBridge.h"

namespace
{
    enum NativeState
    {
        Uninitialized = 0,
        Loading = 1,
        Ready = 2,
        Generating = 3,
        Completed = 4,
        Cancelled = 5,
        Error = 6
    };

    struct Metrics
    {
        int64_t model_load_ms = 0;
        int64_t first_token_ms = 0;
        int64_t total_ms = 0;
        int generated_tokens = 0;
        float tokens_per_second = 0.0f;
        uint64_t peak_memory_bytes = 0;
        std::string thermal_before = "unknown";
        std::string thermal_after = "unknown";
        bool metal_enabled = false;
    };

    struct Runtime
    {
        std::mutex mutex;
        dispatch_queue_t worker = nullptr;
        std::atomic_bool cancel_requested{false};
        NativeState state = Uninitialized;
        llama_model * model = nullptr;
        llama_context * context = nullptr;
        const llama_vocab * vocab = nullptr;
        bool backend_initialized = false;
        std::string output;
        std::string error;
        Metrics metrics;
    };

    Runtime & runtime()
    {
        static Runtime value;
        return value;
    }

    dispatch_queue_t worker_queue(Runtime & value)
    {
        std::lock_guard<std::mutex> lock(value.mutex);
        if (value.worker == nullptr)
        {
            value.worker = dispatch_queue_create(
                "org.endangeredar.on-device-llm",
                DISPATCH_QUEUE_SERIAL);
        }

        return value.worker;
    }

    int64_t elapsed_milliseconds(
        const std::chrono::steady_clock::time_point & start,
        const std::chrono::steady_clock::time_point & end)
    {
        return std::chrono::duration_cast<std::chrono::milliseconds>(end - start).count();
    }

    uint64_t current_memory_bytes()
    {
        task_vm_info_data_t info{};
        mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
        if (task_info(
                mach_task_self(),
                TASK_VM_INFO,
                reinterpret_cast<task_info_t>(&info),
                &count) != KERN_SUCCESS)
        {
            return 0;
        }

        return static_cast<uint64_t>(info.phys_footprint);
    }

    std::string thermal_state()
    {
        switch ([NSProcessInfo processInfo].thermalState)
        {
            case NSProcessInfoThermalStateNominal:
                return "nominal";
            case NSProcessInfoThermalStateFair:
                return "fair";
            case NSProcessInfoThermalStateSerious:
                return "serious";
            case NSProcessInfoThermalStateCritical:
                return "critical";
        }

        return "unknown";
    }

    void sample_memory(Metrics & metrics)
    {
        metrics.peak_memory_bytes = std::max(metrics.peak_memory_bytes, current_memory_bytes());
    }

    bool load_progress(float, void * user_data)
    {
        auto * cancelled = static_cast<std::atomic_bool *>(user_data);
        return cancelled == nullptr || !cancelled->load();
    }

    bool abort_compute(void * user_data)
    {
        auto * cancelled = static_cast<std::atomic_bool *>(user_data);
        return cancelled != nullptr && cancelled->load();
    }

    void set_error(Runtime & value, const char * code)
    {
        std::lock_guard<std::mutex> lock(value.mutex);
        value.error = code == nullptr ? "on_device_llm_error" : code;
        value.output.clear();
        value.state = Error;
    }

    void set_cancelled(Runtime & value)
    {
        std::lock_guard<std::mutex> lock(value.mutex);
        value.error.clear();
        value.output.clear();
        value.state = Cancelled;
    }

    void free_model(Runtime & value)
    {
        if (value.context != nullptr)
        {
            llama_free(value.context);
            value.context = nullptr;
        }

        if (value.model != nullptr)
        {
            llama_model_free(value.model);
            value.model = nullptr;
        }

        value.vocab = nullptr;
        if (value.backend_initialized)
        {
            llama_backend_free();
            value.backend_initialized = false;
        }
    }

    bool append_token_piece(
        const llama_vocab * vocab,
        llama_token token,
        std::string & output)
    {
        char stack_buffer[32];
        int32_t length = llama_token_to_piece(
            vocab,
            token,
            stack_buffer,
            static_cast<int32_t>(sizeof(stack_buffer)),
            0,
            false);
        if (length >= 0)
        {
            output.append(stack_buffer, static_cast<size_t>(length));
            return true;
        }

        std::vector<char> buffer(static_cast<size_t>(-length));
        length = llama_token_to_piece(
            vocab,
            token,
            buffer.data(),
            static_cast<int32_t>(buffer.size()),
            0,
            false);
        if (length < 0)
        {
            return false;
        }

        output.append(buffer.data(), static_cast<size_t>(length));
        return true;
    }

    bool build_chat_prompt(llama_model * model, const std::string & user_prompt, std::string & prompt)
    {
        const char * chat_template = llama_model_chat_template(model, nullptr);
        if (chat_template == nullptr)
        {
            return false;
        }

        const llama_chat_message messages[] =
        {
            { "system", "你是森森，请用简短、友好的中文回答。" },
            { "user", user_prompt.c_str() }
        };
        int32_t required = llama_chat_apply_template(
            chat_template,
            messages,
            2,
            true,
            nullptr,
            0);
        if (required <= 0)
        {
            return false;
        }

        std::vector<char> buffer(static_cast<size_t>(required) + 1, '\0');
        const int32_t written = llama_chat_apply_template(
            chat_template,
            messages,
            2,
            true,
            buffer.data(),
            static_cast<int32_t>(buffer.size()));
        if (written <= 0 || written > required)
        {
            return false;
        }

        prompt.assign(buffer.data(), static_cast<size_t>(written));
        return true;
    }

    bool tokenize_prompt(
        const llama_vocab * vocab,
        const std::string & prompt,
        std::vector<llama_token> & tokens)
    {
        const int32_t required = llama_tokenize(
            vocab,
            prompt.c_str(),
            static_cast<int32_t>(prompt.size()),
            nullptr,
            0,
            true,
            true);
        if (required >= 0 || required == INT32_MIN)
        {
            return false;
        }

        tokens.resize(static_cast<size_t>(-required));
        const int32_t count = llama_tokenize(
            vocab,
            prompt.c_str(),
            static_cast<int32_t>(prompt.size()),
            tokens.data(),
            static_cast<int32_t>(tokens.size()),
            true,
            true);
        if (count <= 0)
        {
            tokens.clear();
            return false;
        }

        tokens.resize(static_cast<size_t>(count));
        return true;
    }

    int copy_string(const std::string & source, char * buffer, int capacity)
    {
        if (buffer == nullptr || capacity <= 0)
        {
            return 0;
        }

        const int length = std::min(static_cast<int>(source.size()), capacity - 1);
        if (length > 0)
        {
            std::memcpy(buffer, source.data(), static_cast<size_t>(length));
        }

        buffer[length] = '\0';
        return length;
    }

    std::string metrics_json(const Metrics & metrics)
    {
        NSString * json = [NSString stringWithFormat:
            @"{\"modelLoadMs\":%lld,\"firstTokenMs\":%lld,\"totalMs\":%lld,"
             "\"generatedTokens\":%d,\"tokensPerSecond\":%.4f,\"peakMemoryBytes\":%llu,"
             "\"thermalBefore\":\"%s\",\"thermalAfter\":\"%s\",\"metalEnabled\":%s}",
            metrics.model_load_ms,
            metrics.first_token_ms,
            metrics.total_ms,
            metrics.generated_tokens,
            metrics.tokens_per_second,
            metrics.peak_memory_bytes,
            metrics.thermal_before.c_str(),
            metrics.thermal_after.c_str(),
            metrics.metal_enabled ? "true" : "false"];
        return std::string([json UTF8String]);
    }
}

int endar_llm_start_load(const char * model_path, int n_ctx, int n_threads)
{
    if (model_path == nullptr || model_path[0] == '\0' || n_ctx < 256 || n_threads < 1)
    {
        return 0;
    }

    Runtime & value = runtime();
    {
        std::lock_guard<std::mutex> lock(value.mutex);
        if (value.state != Uninitialized)
        {
            return 0;
        }

        value.state = Loading;
        value.error.clear();
        value.output.clear();
        value.metrics = Metrics{};
        value.metrics.thermal_before = thermal_state();
        value.cancel_requested.store(false);
    }

    const std::string model_path_copy(model_path);
    dispatch_async(worker_queue(value), ^{
        @autoreleasepool
        {
            const auto started = std::chrono::steady_clock::now();
            llama_backend_init();
            value.backend_initialized = true;

            llama_model_params model_params = llama_model_default_params();
            model_params.n_gpu_layers = -1;
            model_params.progress_callback = load_progress;
            model_params.progress_callback_user_data = &value.cancel_requested;
            value.model = llama_model_load_from_file(model_path_copy.c_str(), model_params);
            if (value.cancel_requested.load())
            {
                free_model(value);
                set_cancelled(value);
                return;
            }

            if (value.model == nullptr)
            {
                free_model(value);
                set_error(value, "model_load_failed");
                return;
            }

            llama_context_params context_params = llama_context_default_params();
            context_params.n_ctx = static_cast<uint32_t>(n_ctx);
            context_params.n_batch = 256;
            context_params.n_ubatch = 256;
            context_params.n_threads = n_threads;
            context_params.n_threads_batch = n_threads;
            value.context = llama_init_from_model(value.model, context_params);
            if (value.context == nullptr)
            {
                free_model(value);
                set_error(value, "context_create_failed");
                return;
            }

            value.vocab = llama_model_get_vocab(value.model);
            llama_set_abort_callback(value.context, abort_compute, &value.cancel_requested);
            const auto ended = std::chrono::steady_clock::now();

            std::lock_guard<std::mutex> lock(value.mutex);
            value.metrics.model_load_ms = elapsed_milliseconds(started, ended);
            value.metrics.metal_enabled = llama_supports_gpu_offload();
            sample_memory(value.metrics);
            value.metrics.thermal_after = thermal_state();
            value.state = Ready;
        }
    });
    return 1;
}

int endar_llm_start_generate(const char * prompt_utf8, int max_tokens)
{
    if (prompt_utf8 == nullptr || prompt_utf8[0] == '\0' || max_tokens < 1 || max_tokens > 256)
    {
        return 0;
    }

    Runtime & value = runtime();
    {
        std::lock_guard<std::mutex> lock(value.mutex);
        if ((value.state != Ready && value.state != Completed && value.state != Cancelled) ||
            value.model == nullptr || value.context == nullptr || value.vocab == nullptr)
        {
            return 0;
        }

        value.state = Generating;
        value.error.clear();
        value.output.clear();
        value.metrics.first_token_ms = 0;
        value.metrics.total_ms = 0;
        value.metrics.generated_tokens = 0;
        value.metrics.tokens_per_second = 0.0f;
        value.metrics.thermal_before = thermal_state();
        value.cancel_requested.store(false);
    }

    const std::string prompt_copy(prompt_utf8);
    dispatch_async(worker_queue(value), ^{
        @autoreleasepool
        {
            const auto started = std::chrono::steady_clock::now();
            std::string formatted_prompt;
            std::vector<llama_token> prompt_tokens;
            if (!build_chat_prompt(value.model, prompt_copy, formatted_prompt) ||
                !tokenize_prompt(value.vocab, formatted_prompt, prompt_tokens))
            {
                set_error(value, "prompt_prepare_failed");
                return;
            }

            if (prompt_tokens.size() + static_cast<size_t>(max_tokens) > llama_n_ctx(value.context))
            {
                set_error(value, "context_budget_exceeded");
                return;
            }

            llama_memory_clear(llama_get_memory(value.context), true);
            llama_batch prompt_batch = llama_batch_get_one(
                prompt_tokens.data(),
                static_cast<int32_t>(prompt_tokens.size()));
            if (llama_decode(value.context, prompt_batch) != 0)
            {
                if (value.cancel_requested.load())
                {
                    set_cancelled(value);
                }
                else
                {
                    set_error(value, "prompt_decode_failed");
                }
                return;
            }

            llama_sampler * sampler = llama_sampler_chain_init(llama_sampler_chain_default_params());
            llama_sampler_chain_add(sampler, llama_sampler_init_top_k(20));
            llama_sampler_chain_add(sampler, llama_sampler_init_top_p(0.8f, 1));
            llama_sampler_chain_add(sampler, llama_sampler_init_temp(0.7f));
            llama_sampler_chain_add(sampler, llama_sampler_init_dist(0xC0DEC0DEu));

            std::string output;
            int generated = 0;
            auto first_token_at = started;
            for (int index = 0; index < max_tokens; ++index)
            {
                if (value.cancel_requested.load())
                {
                    llama_sampler_free(sampler);
                    set_cancelled(value);
                    return;
                }

                const llama_token token = llama_sampler_sample(sampler, value.context, -1);
                if (llama_vocab_is_eog(value.vocab, token))
                {
                    break;
                }

                if (!append_token_piece(value.vocab, token, output))
                {
                    llama_sampler_free(sampler);
                    set_error(value, "token_decode_failed");
                    return;
                }

                ++generated;
                if (generated == 1)
                {
                    first_token_at = std::chrono::steady_clock::now();
                }

                llama_token next_token = token;
                llama_batch next_batch = llama_batch_get_one(&next_token, 1);
                if (llama_decode(value.context, next_batch) != 0)
                {
                    llama_sampler_free(sampler);
                    if (value.cancel_requested.load())
                    {
                        set_cancelled(value);
                    }
                    else
                    {
                        set_error(value, "generation_decode_failed");
                    }
                    return;
                }

                if ((generated % 8) == 0)
                {
                    std::lock_guard<std::mutex> lock(value.mutex);
                    sample_memory(value.metrics);
                }
            }

            llama_sampler_free(sampler);
            const auto ended = std::chrono::steady_clock::now();
            const int64_t total_ms = std::max<int64_t>(1, elapsed_milliseconds(started, ended));
            const int64_t generation_ms = generated > 0
                ? std::max<int64_t>(1, elapsed_milliseconds(first_token_at, ended))
                : total_ms;

            std::lock_guard<std::mutex> lock(value.mutex);
            value.output = output;
            value.metrics.first_token_ms = generated > 0
                ? elapsed_milliseconds(started, first_token_at)
                : 0;
            value.metrics.total_ms = total_ms;
            value.metrics.generated_tokens = generated;
            value.metrics.tokens_per_second = generated > 0
                ? static_cast<float>(generated) * 1000.0f / static_cast<float>(generation_ms)
                : 0.0f;
            sample_memory(value.metrics);
            value.metrics.thermal_after = thermal_state();
            value.state = Completed;
        }
    });
    return 1;
}

int endar_llm_get_state(void)
{
    Runtime & value = runtime();
    std::lock_guard<std::mutex> lock(value.mutex);
    return static_cast<int>(value.state);
}

int endar_llm_copy_output(char * buffer, int capacity)
{
    Runtime & value = runtime();
    std::lock_guard<std::mutex> lock(value.mutex);
    return copy_string(value.output, buffer, capacity);
}

int endar_llm_copy_error(char * buffer, int capacity)
{
    Runtime & value = runtime();
    std::lock_guard<std::mutex> lock(value.mutex);
    return copy_string(value.error, buffer, capacity);
}

int endar_llm_copy_metrics_json(char * buffer, int capacity)
{
    Runtime & value = runtime();
    std::lock_guard<std::mutex> lock(value.mutex);
    return copy_string(metrics_json(value.metrics), buffer, capacity);
}

void endar_llm_cancel(void)
{
    runtime().cancel_requested.store(true);
}

void endar_llm_release(void)
{
    Runtime & value = runtime();
    value.cancel_requested.store(true);
    dispatch_async(worker_queue(value), ^{
        @autoreleasepool
        {
            free_model(value);
            std::lock_guard<std::mutex> lock(value.mutex);
            value.output.clear();
            value.error.clear();
            value.metrics = Metrics{};
            value.state = Uninitialized;
        }
    });
}
