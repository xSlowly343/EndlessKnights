using System.Threading;
using System.Threading.Tasks;
using Game.Core.Ui;

namespace Game.Net.Nakama
{
    public enum AccountOutcome
    {
        SignedUp,
        SignedIn,
        Wrong,
        Offline
    }

    public static class AccountSignIn
    {
        public const int AlreadyLinked = 409;
        public const int Unauthorized = 401;
        public const int NotFound = 404;

        public static string Problem(AccountEntry entry) =>
            !entry.EmailValid ? "account.badEmail" : !entry.PasswordValid ? "account.shortPassword" : string.Empty;

        public static async Task<(AccountOutcome Outcome, NakamaSession? Session)> RunAsync(NakamaClient client, NakamaSession? session, string email, string password, CancellationToken cancel = default)
        {
            try
            {
                if (session != null)
                {
                    try
                    {
                        await client.LinkEmailAsync(session, email, password, cancel).ConfigureAwait(false);
                        return (AccountOutcome.SignedUp, session);
                    }
                    catch (NakamaException error) when (error.Status == AlreadyLinked)
                    {
                    }
                }
                var signed = await client.AuthenticateEmailAsync(email, password, session == null, cancel).ConfigureAwait(false);
                return (session == null ? AccountOutcome.SignedUp : AccountOutcome.SignedIn, signed);
            }
            catch (NakamaException error) when (error.Status == Unauthorized || error.Status == NotFound || error.Status == AlreadyLinked)
            {
                return (AccountOutcome.Wrong, null);
            }
            catch (NakamaException)
            {
                return (AccountOutcome.Offline, null);
            }
        }

        public static string Text(AccountOutcome outcome) => outcome switch
        {
            AccountOutcome.SignedUp => "account.signedUp",
            AccountOutcome.SignedIn => "account.signedIn",
            AccountOutcome.Wrong => "account.wrong",
            _ => "account.offline"
        };
    }
}
