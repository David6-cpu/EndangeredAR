namespace EndangeredAR.Memory
{
    public static class CharacterMemoryIdValidator
    {
        public const int MaximumLength = 96;

        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumLength || !IsAsciiLetterOrDigit(value[0]))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAsciiLetterOrDigit(character) &&
                    character != '.' &&
                    character != '_' &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return value >= 'a' && value <= 'z' || value >= '0' && value <= '9';
        }
    }
}
