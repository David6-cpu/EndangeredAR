namespace EndangeredAR.AI
{
    public enum GreetingProductAnswerMode
    {
        Other,
        SocialChat
    }

    public enum GreetingProductScopeReasonCode
    {
        Eligible,
        NotGreeting,
        AnswerModeNotSocialChat,
        AuthorityNotNone,
        FinalSourceNotOnDeviceLlm,
        ResponseValidationFailed,
        StaleCompletion,
        InvalidAnimal,
        InactiveInteractionPage,
        ExistingEatCandidate,
        ExistingTauntCandidate,
        ExistingActionCandidate
    }

    public readonly struct GreetingProductScopeInput
    {
        public GreetingProductScopeInput(
            GreetingIntentResult rawIntent,
            GreetingProductAnswerMode answerMode,
            ContentAuthority contentAuthority,
            AIFinalSource finalSource,
            bool responseValidationPassed,
            bool requestTicketCurrent,
            bool currentAnimalValid,
            bool activeInteractionPage,
            AIAction existingActionCandidate,
            bool hasOtherAcceptedActionCandidate)
        {
            RawIntent = rawIntent;
            AnswerMode = answerMode;
            ContentAuthority = contentAuthority;
            FinalSource = finalSource;
            ResponseValidationPassed = responseValidationPassed;
            RequestTicketCurrent = requestTicketCurrent;
            CurrentAnimalValid = currentAnimalValid;
            ActiveInteractionPage = activeInteractionPage;
            ExistingActionCandidate = existingActionCandidate;
            HasOtherAcceptedActionCandidate = hasOtherAcceptedActionCandidate;
        }

        public GreetingIntentResult RawIntent { get; }
        public GreetingProductAnswerMode AnswerMode { get; }
        public ContentAuthority ContentAuthority { get; }
        public AIFinalSource FinalSource { get; }
        public bool ResponseValidationPassed { get; }
        public bool RequestTicketCurrent { get; }
        public bool CurrentAnimalValid { get; }
        public bool ActiveInteractionPage { get; }
        public AIAction ExistingActionCandidate { get; }
        public bool HasOtherAcceptedActionCandidate { get; }
    }

    public readonly struct GreetingProductScopeResult
    {
        public GreetingProductScopeResult(
            bool isEligible,
            GreetingProductScopeReasonCode reasonCode,
            string policyVersion)
        {
            IsEligible = isEligible;
            ReasonCode = reasonCode;
            PolicyVersion = policyVersion ?? string.Empty;
        }

        public bool IsEligible { get; }
        public GreetingProductScopeReasonCode ReasonCode { get; }
        public string PolicyVersion { get; }
    }

    public static class GreetingProductScopeGate
    {
        public const string PolicyVersion = "r3.4a5-greeting-scope-v1";

        public static GreetingProductScopeResult Evaluate(GreetingProductScopeInput input)
        {
            if (!input.RawIntent.IsGreeting)
            {
                return Reject(GreetingProductScopeReasonCode.NotGreeting);
            }

            if (input.AnswerMode != GreetingProductAnswerMode.SocialChat)
            {
                return Reject(GreetingProductScopeReasonCode.AnswerModeNotSocialChat);
            }

            if (input.ContentAuthority != ContentAuthority.None)
            {
                return Reject(GreetingProductScopeReasonCode.AuthorityNotNone);
            }

            if (input.FinalSource != AIFinalSource.OnDeviceLlm)
            {
                return Reject(GreetingProductScopeReasonCode.FinalSourceNotOnDeviceLlm);
            }

            if (!input.ResponseValidationPassed)
            {
                return Reject(GreetingProductScopeReasonCode.ResponseValidationFailed);
            }

            if (!input.RequestTicketCurrent)
            {
                return Reject(GreetingProductScopeReasonCode.StaleCompletion);
            }

            if (!input.CurrentAnimalValid)
            {
                return Reject(GreetingProductScopeReasonCode.InvalidAnimal);
            }

            if (!input.ActiveInteractionPage)
            {
                return Reject(GreetingProductScopeReasonCode.InactiveInteractionPage);
            }

            if (input.ExistingActionCandidate == AIAction.Eat)
            {
                return Reject(GreetingProductScopeReasonCode.ExistingEatCandidate);
            }

            if (input.ExistingActionCandidate == AIAction.Taunt)
            {
                return Reject(GreetingProductScopeReasonCode.ExistingTauntCandidate);
            }

            if (input.ExistingActionCandidate != AIAction.None || input.HasOtherAcceptedActionCandidate)
            {
                return Reject(GreetingProductScopeReasonCode.ExistingActionCandidate);
            }

            return new GreetingProductScopeResult(
                true,
                GreetingProductScopeReasonCode.Eligible,
                PolicyVersion);
        }

        private static GreetingProductScopeResult Reject(GreetingProductScopeReasonCode reasonCode)
        {
            return new GreetingProductScopeResult(false, reasonCode, PolicyVersion);
        }
    }
}
