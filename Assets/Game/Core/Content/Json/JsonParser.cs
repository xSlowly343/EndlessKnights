using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core.Content.Json
{
    public sealed class JsonParser
    {
        private readonly string _text;
        private readonly string _fileName;
        private int _position;

        private JsonParser(string text, string fileName)
        {
            _text = text;
            _fileName = fileName;
        }

        public static JsonValue Parse(string text, string fileName)
        {
            var parser = new JsonParser(text, fileName);
            parser.SkipWhitespace();
            var value = parser.ReadValue();
            parser.SkipWhitespace();
            if (parser._position < text.Length)
                throw parser.Error("unexpected text after the end of the document");
            return value;
        }

        private JsonValue ReadValue()
        {
            if (_position >= _text.Length)
                throw Error("unexpected end of file");

            switch (_text[_position])
            {
                case '{':
                    return ReadObject();
                case '[':
                    return ReadArray();
                case '"':
                    return JsonValue.Of(ReadString());
                case 't':
                    ExpectWord("true");
                    return JsonValue.Of(true);
                case 'f':
                    ExpectWord("false");
                    return JsonValue.Of(false);
                case 'n':
                    ExpectWord("null");
                    return JsonValue.Null();
                default:
                    return JsonValue.Of(ReadNumber());
            }
        }

        private JsonValue ReadObject()
        {
            _position++;
            var fields = new Dictionary<string, JsonValue>();
            SkipWhitespace();
            if (TryConsume('}'))
                return JsonValue.Object(fields);

            while (true)
            {
                SkipWhitespace();
                if (_position >= _text.Length)
                    throw Error("unexpected end of file");
                if (_text[_position] != '"')
                    throw Error("expected a field name in quotes");
                var name = ReadString();
                if (fields.ContainsKey(name))
                    throw Error($"field '{name}' is repeated");
                SkipWhitespace();
                if (!TryConsume(':'))
                    throw Error($"expected ':' after field '{name}'");
                SkipWhitespace();
                fields[name] = ReadValue();
                SkipWhitespace();
                if (TryConsume('}'))
                    return JsonValue.Object(fields);
                if (!TryConsume(','))
                    throw Error("expected ',' or '}' in object");
            }
        }

        private JsonValue ReadArray()
        {
            _position++;
            var items = new List<JsonValue>();
            SkipWhitespace();
            if (TryConsume(']'))
                return JsonValue.Array(items);

            while (true)
            {
                SkipWhitespace();
                items.Add(ReadValue());
                SkipWhitespace();
                if (TryConsume(']'))
                    return JsonValue.Array(items);
                if (!TryConsume(','))
                    throw Error("expected ',' or ']' in array");
            }
        }

        private string ReadString()
        {
            _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var c = _text[_position++];
                if (c == '"')
                    return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }
                if (_position >= _text.Length)
                    break;
                var escape = _text[_position++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (_position + 4 > _text.Length || !int.TryParse(_text.Substring(_position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            throw Error("bad \\u escape in string");
                        builder.Append((char)code);
                        _position += 4;
                        break;
                    default:
                        throw Error($"unknown escape '\\{escape}' in string");
                }
            }
            throw Error("string is not closed");
        }

        private double ReadNumber()
        {
            var start = _position;
            while (_position < _text.Length && "+-0123456789.eE".IndexOf(_text[_position]) >= 0)
                _position++;
            var token = _text.Substring(start, _position - start);
            if (token.Length == 0)
            {
                _position = start;
                throw Error($"unexpected character '{_text[start]}'");
            }
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                _position = start;
                throw Error($"'{token}' is not a number");
            }
            return number;
        }

        private void ExpectWord(string word)
        {
            if (string.CompareOrdinal(_text, _position, word, 0, word.Length) != 0)
                throw Error($"unexpected character '{_text[_position]}'");
            _position += word.Length;
        }

        private bool TryConsume(char c)
        {
            if (_position < _text.Length && _text[_position] == c)
            {
                _position++;
                return true;
            }
            return false;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }

        private ContentException Error(string problem)
        {
            var line = 1;
            var column = 1;
            for (var i = 0; i < _position && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            return new ContentException(_fileName, string.Empty, $"line {line}, column {column}: {problem}");
        }
    }
}
