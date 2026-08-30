using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using EndangeredAR.AI.Knowledge;
using EndangeredAR.AI.Prompt;
using EndangeredAR.AI.Validation;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceAIResponseComposer : IAIProvider
    {
        private const string SystemRole =
            "你是珍稀及受保护野生动物科普角色森森。只用应用提供的可信上下文组织简洁、自然、适合青少年的中文回答。" +
            "不得补造科学事实、业务状态、长期记忆、聊天历史或动作权限；没有提供的事实必须明确说不知道。";
        private const string HistoryBoundaryResponseContract =
            "\n回答要求：必须明确说明不会长期保存完整聊天内容，只回答一句自然中文；" +
            "不得声称记得、忘记或讨论过任何旧聊天；不得猜测旧话题，也不得描述其他记忆机制。";
        private const string SystemPolicyRepairResponse =
            "这条请求超出可信动物科普或应用权限；我不会编造事实或修改任何状态。";
        private const int StrictRepairMaxTokens = 64;

        private readonly IOnDeviceLLMProvider provider;
        private readonly OnDevicePromptBudget promptBudget;

        public OnDeviceAIResponseComposer(
            IOnDeviceLLMProvider provider,
            OnDevicePromptBudget promptBudget)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.promptBudget = promptBudget ?? throw new ArgumentNullException(nameof(promptBudget));
        }

        public string ProviderId => OnDeviceLLMProvider.OnDeviceGeneratorId;

        public IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.animalId) ||
                string.IsNullOrWhiteSpace(request.message))
            {
                onError?.Invoke(Error("on_device_request_invalid"));
                yield break;
            }

            var ready = false;
            OnDeviceLLMError prepareError = null;
            var prepare = provider.Prepare(
                timeoutSeconds,
                () => ready = true,
                error => prepareError = error);
            while (prepare != null && prepare.MoveNext())
            {
                yield return null;
            }

            if (!ready)
            {
                onError?.Invoke(ConvertError(prepareError, "on_device_model_unavailable"));
                yield break;
            }

            CanonicalEvidencePackage evidence = null;
            TrustedChatPrompt prompt;
            try
            {
                var authorityText = BuildAuthorityText(request, out evidence);
                prompt = TrustedChatPromptBuilder.Build(
                    new TrustedChatPromptInput(
                        SystemRole,
                        request.message,
                        BuildTrustedHistory(request),
                        request.ContentAuthority,
                        request.ContentAuthority == ContentAuthority.CurrentProgress ? authorityText : string.Empty,
                        request.ContentAuthority == ContentAuthority.CharacterMemory ? authorityText : string.Empty,
                        request.ContentAuthority == ContentAuthority.CanonicalKnowledge ? authorityText : string.Empty,
                        request.ContentAuthority == ContentAuthority.SystemPolicy ? authorityText : string.Empty),
                    promptBudget,
                    provider);
            }
            catch (OnDevicePromptBudgetExceededException)
            {
                onError?.Invoke(Error("on_device_prompt_budget_exceeded"));
                yield break;
            }
            catch (ArgumentException)
            {
                onError?.Invoke(Error("on_device_authority_invalid"));
                yield break;
            }

            IReadOnlyList<OnDeviceChatMessage> messages = prompt.Messages;
            long totalGenerationMs = 0L;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                OnDeviceLLMResult generated = null;
                OnDeviceLLMError generationError = null;
                OnDeviceLLMRequest nativeRequest;
                try
                {
                    var isDeterministicRepair = attempt > 0 &&
                                                (request.ContentAuthority == ContentAuthority.CurrentProgress ||
                                                 request.ContentAuthority == ContentAuthority.CharacterMemory ||
                                                 request.ContentAuthority == ContentAuthority.SystemPolicy);
                    var isShortRepair = isDeterministicRepair &&
                                        request.ContentAuthority != ContentAuthority.CharacterMemory;
                    nativeRequest = new OnDeviceLLMRequest(
                        SafeGenerationId(request.requestId + (attempt == 0 ? string.Empty : "_repair")),
                        messages,
                        isShortRepair
                            ? Math.Min(promptBudget.ReservedGenerationTokens, StrictRepairMaxTokens)
                            : promptBudget.ReservedGenerationTokens,
                        isDeterministicRepair ? 0f : 0.7f,
                        isDeterministicRepair ? 1f : 0.8f,
                        1.05f,
                        7u);
                }
                catch (ArgumentException)
                {
                    onError?.Invoke(Error("on_device_request_invalid"));
                    yield break;
                }

                var generation = provider.Send(
                    nativeRequest,
                    timeoutSeconds,
                    result => generated = result,
                    error => generationError = error);
                while (generation != null && generation.MoveNext())
                {
                    yield return null;
                }

                if (generated == null || string.IsNullOrWhiteSpace(generated.Text))
                {
                    onError?.Invoke(ConvertError(generationError, "on_device_generation_failed"));
                    yield break;
                }

                totalGenerationMs += Math.Max(0L, generated.Metrics?.totalMs ?? 0L);
                var validation = AuthorityAwareResponseValidator.Validate(request, evidence, generated.Text);
                if (validation.IsValid)
                {
                    onSuccess?.Invoke(ComposeResponse(request, evidence, generated, totalGenerationMs));
                    yield break;
                }

                if (attempt > 0)
                {
                    onError?.Invoke(Error("on_device_response_validation_failed"));
                    yield break;
                }

                try
                {
                    messages = StrictRepairPromptBuilder.Build(
                        prompt.Messages,
                        provider,
                        promptBudget,
                        validation.ErrorCode,
                        BuildExactRepairResponse(request));
                }
                catch (OnDevicePromptBudgetExceededException)
                {
                    onError?.Invoke(Error("on_device_response_validation_failed"));
                    yield break;
                }
                catch (ArgumentException)
                {
                    onError?.Invoke(Error("on_device_response_validation_failed"));
                    yield break;
                }
            }
        }

        private static AIResponse ComposeResponse(
            AIRequest request,
            CanonicalEvidencePackage evidence,
            OnDeviceLLMResult generated,
            long totalGenerationMs)
        {
            var response = new AIResponse
            {
                animalId = request.animalId,
                reply = generated.Text.Trim(),
                source = OnDeviceLLMProvider.OnDeviceGeneratorId,
                routeReason = "on_device_only",
                suggestedQuestions = Array.Empty<string>(),
                answerMode = ResolveAnswerMode(request, evidence),
                evidenceStatus = evidence?.EvidenceStatus ?? "not_required",
                GroundingTopic = evidence?.GroundingTopic ?? GroundingTopic.None,
                GroundedFactIds = evidence == null
                    ? Array.Empty<string>()
                    : Copy(evidence.GroundedFactIds),
                ActionSuggestion = AIActionPolicy.SelectDeterministicIntent(
                    request.message,
                    request.animalId),
                citations = evidence == null
                    ? Array.Empty<AICitation>()
                    : ConvertCitations(evidence.Citations)
            };
            response.ContentAuthority = request.ContentAuthority;
            response.LanguageGenerator = LanguageGenerator.OnDeviceLlm;
            response.ProviderAttempts = new[] { OnDeviceLLMProvider.OnDeviceGeneratorId };
            response.FallbackUsed = false;
            response.FallbackReasonCode = string.Empty;
            response.ElapsedMilliseconds = Math.Max(0L, totalGenerationMs);
            response.ProvenanceErrorCode = string.Empty;
            return response;
        }

        private static string BuildAuthorityText(
            AIRequest request,
            out CanonicalEvidencePackage evidence)
        {
            evidence = null;
            switch (request.ContentAuthority)
            {
                case ContentAuthority.CanonicalKnowledge:
                    evidence = CanonicalKnowledgeRetriever.Retrieve(
                        request.animalId,
                        request.knowledgeProfile,
                        request.message);
                    return FormatCanonicalEvidence(evidence);
                case ContentAuthority.CurrentProgress:
                    return FormatCurrentContext(request.Context);
                case ContentAuthority.CharacterMemory:
                    return CharacterMemoryAnswerBuilder.BuildExplicitRecall(request.MemoryContext);
                case ContentAuthority.SystemPolicy:
                    return request.MemoryUseMode == MemoryUseMode.HistoryBoundary
                        ? CharacterMemoryAnswerBuilder.BuildConversationHistoryBoundary() +
                          HistoryBoundaryResponseContract
                        : "唯一允许的回复：" + SystemPolicyRepairResponse +
                          "只能逐字输出该句，不得改写、解释或添加内容。";
                case ContentAuthority.None:
                default:
                    return string.Empty;
            }
        }

        private static string FormatCanonicalEvidence(CanonicalEvidencePackage evidence)
        {
            if (evidence == null || evidence.AnswerMode != "grounded_fact" ||
                string.IsNullOrWhiteSpace(evidence.ApprovedAnswerConstraint))
            {
                throw new ArgumentException("Canonical evidence is unavailable.");
            }

            var builder = new StringBuilder()
                .Append("证据状态：").Append(evidence.EvidenceStatus).Append('\n')
                .Append("回答事实边界：").Append(evidence.ApprovedAnswerConstraint.Trim());
            foreach (var fact in evidence.Facts)
            {
                if (!string.IsNullOrWhiteSpace(fact.Claim))
                {
                    builder.Append("\n审核事实：").Append(fact.Claim.Trim());
                }
            }

            builder.Append("\n只能重述以上事实；引用由应用层显示，不得自行添加来源。");
            return builder.ToString();
        }

        private static string FormatCurrentContext(ReadOnlyCharacterContext context)
        {
            context ??= ReadOnlyCharacterContext.Empty;
            var character = context.Character;
            var task = context.Task;
            var builder = new StringBuilder()
                .Append("当前动物：").Append(character.AnimalId).Append('\n')
                .Append("当前已解锁：").Append(character.Unlocked ? "是" : "否").Append('\n')
                .Append("当前已学习知识数：").Append(character.LearnedKnowledgeCount).Append('\n')
                .Append("当前已获得徽章数：").Append(character.EarnedBadgeCount).Append('\n')
                .Append("当前任务：").Append(string.IsNullOrEmpty(task.TaskTitle) ? "无" : task.TaskTitle).Append('\n')
                .Append("当前任务完成：").Append(task.Completed ? "是" : "否").Append('\n');

            builder.Append("可回答结论：").Append(BuildCurrentProgressConclusion(context)).Append('\n');

            return builder
                .Append("只可自然重述可回答结论；不得提供任务步骤、可选物品、奖励、重玩建议或新任务。\n")
                .Append("这些是当前只读状态，不是长期记忆；不得声称修改了任何状态。")
                .ToString();
        }

        private static string BuildExactRepairResponse(AIRequest request)
        {
            if (request.ContentAuthority == ContentAuthority.CurrentProgress)
            {
                return BuildCurrentProgressConclusion(request.Context);
            }

            if (request.ContentAuthority == ContentAuthority.CharacterMemory)
            {
                return request.MemoryUseMode == MemoryUseMode.Reunion
                    ? CharacterMemoryAnswerBuilder.BuildReunion(
                        request.MemoryContext,
                        SafeReunionTailGuard.FallbackTail)
                    : CharacterMemoryAnswerBuilder.BuildExplicitRecall(request.MemoryContext);
            }

            return request.ContentAuthority == ContentAuthority.SystemPolicy &&
                   request.MemoryUseMode != MemoryUseMode.HistoryBoundary
                ? SystemPolicyRepairResponse
                : string.Empty;
        }

        private static string BuildCurrentProgressConclusion(ReadOnlyCharacterContext context)
        {
            var task = (context ?? ReadOnlyCharacterContext.Empty).Task;
            if (string.IsNullOrEmpty(task.TaskTitle))
            {
                return "当前状态没有提供任务。";
            }

            return task.Completed
                ? task.TaskTitle + "已完成；当前状态没有提供新的任务。"
                : "当前任务是" + task.TaskTitle + "，尚未完成。";
        }

        private static OnDeviceChatMessage[] BuildHistory(EndangeredAR.API.ChatMessage[] history)
        {
            var result = new List<OnDeviceChatMessage>();
            foreach (var message in history ?? Array.Empty<EndangeredAR.API.ChatMessage>())
            {
                if (message == null || (message.role != "user" && message.role != "assistant") ||
                    string.IsNullOrWhiteSpace(message.content))
                {
                    continue;
                }

                result.Add(new OnDeviceChatMessage(message.role, message.content.Trim()));
            }

            return result.ToArray();
        }

        private static OnDeviceChatMessage[] BuildTrustedHistory(AIRequest request)
        {
            // Authority-bound replies must not inherit lower-priority session claims.
            return request.ContentAuthority == ContentAuthority.None
                ? BuildHistory(request.history)
                : Array.Empty<OnDeviceChatMessage>();
        }

        private static string ResolveAnswerMode(
            AIRequest request,
            CanonicalEvidencePackage evidence)
        {
            if (evidence != null)
            {
                return evidence.AnswerMode;
            }

            if (request.MemoryUseMode == MemoryUseMode.ExplicitRecall ||
                request.MemoryUseMode == MemoryUseMode.HistoryBoundary)
            {
                return CharacterMemoryAnswerBuilder.MemoryRecallAnswerMode;
            }

            return request.ContentAuthority == ContentAuthority.SystemPolicy
                ? "off_domain"
                : "social_chat";
        }

        private static AICitation[] ConvertCitations(
            IReadOnlyList<CanonicalEvidenceCitation> citations)
        {
            var result = new AICitation[citations?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                var citation = citations[index];
                result[index] = new AICitation
                {
                    sourceId = citation.SourceId,
                    title = citation.Title,
                    organization = citation.Organization,
                    url = citation.Url
                };
            }

            return result;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            var result = new string[values?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = values[index];
            }

            return result;
        }

        private static string SafeGenerationId(string value)
        {
            if (!string.IsNullOrEmpty(value) && value.Length <= 64)
            {
                foreach (var character in value)
                {
                    if (!(character == '_' || character == '-' ||
                          character >= 'a' && character <= 'z' ||
                          character >= 'A' && character <= 'Z' ||
                          character >= '0' && character <= '9'))
                    {
                        return Guid.NewGuid().ToString("N");
                    }
                }

                return value;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static AIProviderError ConvertError(OnDeviceLLMError error, string fallbackCode)
        {
            return new AIProviderError(
                error?.Code ?? fallbackCode,
                "On-device AI generation is unavailable.",
                error?.IsTimeout == true);
        }

        private static AIProviderError Error(string code)
        {
            return new AIProviderError(code, "On-device AI request failed.", false);
        }
    }
}
