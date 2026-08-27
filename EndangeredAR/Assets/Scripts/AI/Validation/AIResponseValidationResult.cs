namespace EndangeredAR.AI.Validation
{
    public sealed class AIResponseValidationResult
    {
        private AIResponseValidationResult(bool isValid, string errorCode)
        {
            IsValid = isValid;
            ErrorCode = errorCode ?? string.Empty;
        }

        public bool IsValid { get; }
        public string ErrorCode { get; }

        public static AIResponseValidationResult Valid { get; } =
            new AIResponseValidationResult(true, string.Empty);

        public static AIResponseValidationResult Reject(string errorCode)
        {
            return new AIResponseValidationResult(false, errorCode);
        }
    }
}
