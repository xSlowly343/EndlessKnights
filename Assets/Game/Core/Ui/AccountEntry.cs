using System.Text;

namespace Game.Core.Ui
{
    public enum AccountField
    {
        None,
        Email,
        Password
    }

    public sealed class AccountEntry
    {
        public const int MaxLength = 64;
        public const int MinPassword = 8;
        private const char Mask = '•';

        private readonly StringBuilder _email = new StringBuilder(MaxLength);
        private readonly StringBuilder _password = new StringBuilder(MaxLength);

        public AccountField Focus { get; private set; }
        public string Email => _email.ToString();
        public string Password => _password.ToString();
        public string MaskedPassword => new string(Mask, _password.Length);
        public string LinkedEmail { get; private set; } = string.Empty;
        public bool SignedIn => LinkedEmail.Length > 0;

        public bool EmailValid
        {
            get
            {
                var text = Email;
                var at = text.IndexOf('@');
                var dot = text.LastIndexOf('.');
                return at > 0 && at == text.LastIndexOf('@') && dot > at + 1 && dot < text.Length - 1 && text.IndexOf(' ') < 0;
            }
        }

        public bool PasswordValid => _password.Length >= MinPassword;
        public bool Ready => EmailValid && PasswordValid;

        public void FocusOn(AccountField field) => Focus = field;

        public bool Type(char symbol)
        {
            var target = Focus == AccountField.Email ? _email : Focus == AccountField.Password ? _password : null;
            if (target == null)
                return false;
            if (symbol == CouponEntry.Backspace || symbol == CouponEntry.Delete)
            {
                if (target.Length == 0)
                    return false;
                target.Length--;
                return true;
            }
            if (symbol < ' ' || target.Length >= MaxLength || (Focus == AccountField.Email && symbol == ' '))
                return false;
            target.Append(Focus == AccountField.Email ? char.ToLowerInvariant(symbol) : symbol);
            return true;
        }

        public bool Paste(string text)
        {
            var changed = false;
            foreach (var symbol in text.Trim())
                changed |= Type(symbol);
            return changed;
        }

        public void SignedInAs(string email)
        {
            LinkedEmail = email;
            _password.Clear();
            Focus = AccountField.None;
        }

        public void SignOut()
        {
            LinkedEmail = string.Empty;
            _password.Clear();
        }

        public void ForgetPassword() => _password.Clear();
    }
}
