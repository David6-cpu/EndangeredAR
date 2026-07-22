using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.API;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public class ApiSecurityTests
    {
        private const string ChatClientPath = "Assets/Scripts/API/ChatApiClient.cs";

        [Test]
        public void ApiConfig_DoesNotExposeProviderKeyOrDirectModeFields()
        {
            var memberNames = typeof(ApiConfig)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .ToArray();

            var prohibitedNames = new[]
            {
                "use" + "DirectLlm",
                "moon" + "shotBaseUrl",
                "moon" + "shotModel",
                "moon" + "shotApiKey",
                "direct" + "LlmSystemPrompt",
                "Effective" + "DirectLlmSystemPrompt"
            };

            foreach (var prohibitedName in prohibitedNames)
            {
                Assert.That(memberNames, Does.Not.Contain(prohibitedName),
                    $"ApiConfig must not expose the client-side provider member '{prohibitedName}'.");
            }
        }

        [Test]
        public void ChatApiClient_UsesOnlyBackendChatEndpoint()
        {
            var source = File.ReadAllText(Path.GetFullPath(ChatClientPath));

            StringAssert.Contains("{config.baseUrl.TrimEnd('/')}/chat", source);
            StringAssert.DoesNotContain("SendDirect" + "MoonshotMessage", source);
            StringAssert.DoesNotContain("Moon" + "shot", source);
            StringAssert.DoesNotContain("Author" + "ization", source);
            StringAssert.DoesNotContain("Bear" + "er", source);
            StringAssert.DoesNotContain("chat/completions", source);
        }
    }
}
