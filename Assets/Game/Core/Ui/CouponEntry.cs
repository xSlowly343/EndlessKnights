using System.Text;
using Game.Core.Mail;

namespace Game.Core.Ui
{
    public sealed class CouponEntry
    {
        public const char Backspace = '\b';
        public const char Delete = (char)127;
        public const char PasteKey = (char)22;
        private const int GroupSize = 4;

        private readonly StringBuilder _text = new StringBuilder(Coupon.MaxLength);

        public string Text => _text.ToString();
        public int Length => _text.Length;
        public bool Ready => _text.Length >= Coupon.MinLength;

        public string Display
        {
            get
            {
                var shown = new StringBuilder(_text.Length + _text.Length / GroupSize);
                for (var i = 0; i < _text.Length; i++)
                {
                    if (i > 0 && i % GroupSize == 0)
                        shown.Append('-');
                    shown.Append(_text[i]);
                }
                return shown.ToString();
            }
        }

        public static bool IsSubmit(char symbol) => symbol == '\r' || symbol == '\n';

        public bool Type(char symbol)
        {
            if (symbol == Backspace || symbol == Delete)
            {
                if (_text.Length == 0)
                    return false;
                _text.Length--;
                return true;
            }
            var upper = char.ToUpperInvariant(symbol);
            if ((upper < 'A' || upper > 'Z') && (upper < '0' || upper > '9'))
                return false;
            if (_text.Length >= Coupon.MaxLength)
                return false;
            _text.Append(upper);
            return true;
        }

        public bool Paste(string? raw)
        {
            var code = Coupon.Normalize(raw);
            if (code.Length == 0)
                return false;
            _text.Clear();
            _text.Append(code);
            return true;
        }

        public bool Clear()
        {
            if (_text.Length == 0)
                return false;
            _text.Clear();
            return true;
        }
    }
}
