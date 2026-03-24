using System;

namespace FourthDevs.Email.Knowledge
{
    /// <summary>
    /// Simple mutex: locks the knowledge base to a single account during
    /// draft sessions to enforce cross-account isolation.
    /// </summary>
    public static class AccessLock
    {
        private static string _lockedAccount;

        public static void LockKnowledgeToAccount(string account)
        {
            if (_lockedAccount != null)
            {
                throw new InvalidOperationException(
                    $"Knowledge base is already locked to \"{_lockedAccount}\". " +
                    $"Unlock before locking to \"{account}\".");
            }
            _lockedAccount = account;
        }

        public static void UnlockKnowledge()
        {
            _lockedAccount = null;
        }

        public static string GetLockedAccount()
        {
            return _lockedAccount;
        }

        public static void AssertAccountAccess(string requestedAccount)
        {
            if (_lockedAccount != null && requestedAccount != _lockedAccount)
            {
                throw new InvalidOperationException(
                    $"ACCESS_DENIED: Knowledge base is locked to \"{_lockedAccount}\". " +
                    $"Cannot access data for \"{requestedAccount}\".");
            }
        }
    }
}
