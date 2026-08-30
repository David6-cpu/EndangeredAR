using System;
using System.Collections.Generic;
using EndangeredAR.AI.OnDevice;
using EndangeredAR.AI.Prompt;

namespace EndangeredAR.AI.Validation
{
    public static class StrictRepairPromptBuilder
    {
        public static IReadOnlyList<OnDeviceChatMessage> Build(
            IReadOnlyList<OnDeviceChatMessage> original,
            IOnDeviceTokenCounter tokenCounter,
            OnDevicePromptBudget budget,
            string validationCode,
            string exactRepairResponse)
        {
            if (original == null || original.Count < 2 || tokenCounter == null || budget == null ||
                original[0]?.Role != "system" || original[original.Count - 1]?.Role != "user")
            {
                throw new ArgumentException("A structured trusted prompt is required.");
            }

            var code = SanitizeCode(validationCode);
            var messages = Copy(original);
            var authorityRepair = BuildAuthorityRepair(code, exactRepairResponse);
            messages[0] = new OnDeviceChatMessage(
                "system",
                messages[0].Content +
                "\n\n<STRICT RESPONSE REPAIR>\n" +
                "上一次输出未通过应用校验。只根据同一可信上下文重新回答；不得新增事实、数字、名称、时间、聊天历史或动作。" +
                "校验类别：" + code +
                authorityRepair +
                "\n</STRICT RESPONSE REPAIR>");
            if (IsCurrentProgressValidation(code) || IsMemoryValidation(code) ||
                IsSystemPolicyValidation(code))
            {
                messages[messages.Count - 1] = new OnDeviceChatMessage(
                    "user",
                    "这是一次格式修复。不要回答之前的问题。" +
                    "逐字复制以下唯一允许的回复，不得改写、解释或添加内容：\n" +
                    exactRepairResponse.Trim());
            }

            while (true)
            {
                var count = tokenCounter.CountTokens(messages);
                if (count >= 0 && count <= budget.MaximumPromptTokens)
                {
                    return messages.AsReadOnly();
                }

                if (messages.Count <= 2)
                {
                    throw new OnDevicePromptBudgetExceededException();
                }

                messages.RemoveAt(1);
                if (messages.Count > 2 && messages[1].Role == "assistant")
                {
                    messages.RemoveAt(1);
                }
            }
        }

        private static List<OnDeviceChatMessage> Copy(IReadOnlyList<OnDeviceChatMessage> values)
        {
            var result = new List<OnDeviceChatMessage>(values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                result.Add(values[index]?.Copy() ?? throw new ArgumentException("Prompt message is null."));
            }

            return result;
        }

        private static string SanitizeCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return "validation_failed";
            }

            foreach (var character in value)
            {
                if (!(character == '_' || character >= 'a' && character <= 'z' ||
                      character >= '0' && character <= '9'))
                {
                    return "validation_failed";
                }
            }

            return value;
        }

        private static string BuildAuthorityRepair(string validationCode, string exactRepairResponse)
        {
            if (validationCode == "chat_history_claim_not_authorized" ||
                validationCode == "history_boundary_missing")
            {
                return "\n修复要求：只输出下面这一句，不得添加其他内容：" +
                       "不会长期保存完整聊天内容，所以无法准确回答你以前问过什么。";
            }

            if (!IsCurrentProgressValidation(validationCode) &&
                !IsMemoryValidation(validationCode))
            {
                if (!IsSystemPolicyValidation(validationCode))
                {
                    return string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(exactRepairResponse) || exactRepairResponse.Length > 320 ||
                exactRepairResponse.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("A bounded exact repair response is required.");
            }

            return "\n修复要求：只输出下面这一句，不得添加其他内容：" +
                   exactRepairResponse.Trim();
        }

        private static bool IsCurrentProgressValidation(string validationCode)
        {
            return validationCode == "current_task_missing" ||
                   validationCode == "current_task_state_missing" ||
                   validationCode == "current_task_state_conflict" ||
                   validationCode == "current_task_guidance_not_authorized" ||
                   validationCode == "current_state_count_conflict";
        }

        private static bool IsSystemPolicyValidation(string validationCode)
        {
            return validationCode == "system_policy_response_not_exact";
        }

        private static bool IsMemoryValidation(string validationCode)
        {
            return validationCode == "memory_claim_not_authorized" ||
                   validationCode == "memory_anchor_missing" ||
                   validationCode == "memory_empty_claim_missing" ||
                   validationCode == "memory_unavailable_claim_missing";
        }
    }
}
