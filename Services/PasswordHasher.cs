namespace MyAIAgent.Services
{
    /// <summary>
    /// BCrypt password hashing with transparent migration of legacy plaintext
    /// rows: <see cref="Verify"/> accepts a stored value that is either a real
    /// BCrypt hash or (for accounts created before hashing existed) the plaintext
    /// password, and tells the caller whether the row still needs upgrading.
    /// </summary>
    public static class PasswordHasher
    {
        public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        private static bool LooksHashed(string stored) =>
            stored.StartsWith("$2a$") || stored.StartsWith("$2b$") || stored.StartsWith("$2y$");

        /// <param name="needsRehash">
        /// true when the stored value was legacy plaintext and matched — the
        /// caller should replace it with <see cref="Hash"/>(password) and save.
        /// </param>
        public static bool Verify(string password, string stored, out bool needsRehash)
        {
            if (LooksHashed(stored))
            {
                needsRehash = false;
                return BCrypt.Net.BCrypt.Verify(password, stored);
            }

            // Legacy plaintext row.
            needsRehash = stored == password;
            return needsRehash;
        }
    }
}
