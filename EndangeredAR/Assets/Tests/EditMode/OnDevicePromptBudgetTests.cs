using EndangeredAR.AI.Prompt;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDevicePromptBudgetTests
    {
        [Test]
        public void Budget_ReservesGenerationAndSafetyTokens()
        {
            var budget = new OnDevicePromptBudget(2048, 128, 64);

            Assert.That(budget.ContextTokens, Is.EqualTo(2048));
            Assert.That(budget.ReservedGenerationTokens, Is.EqualTo(128));
            Assert.That(budget.SafetyMarginTokens, Is.EqualTo(64));
            Assert.That(budget.MaximumPromptTokens, Is.EqualTo(1856));
        }

        [TestCase(255, 64, 32)]
        [TestCase(2048, 0, 32)]
        [TestCase(2048, 64, 0)]
        [TestCase(256, 200, 100)]
        public void Budget_RejectsInvalidOrEmptyPromptCapacity(
            int context,
            int generation,
            int safety)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new OnDevicePromptBudget(context, generation, safety));
        }
    }
}
