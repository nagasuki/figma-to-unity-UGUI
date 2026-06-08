using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FigmaToUGUI.Editor
{
    internal static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return new Parser(json).ParseValue();
        }

        private sealed class Parser
        {
            private readonly string json;
            private int index;

            public Parser(string json)
            {
                this.json = json;
            }

            public object ParseValue()
            {
                EatWhitespace();
                if (index >= json.Length)
                {
                    return null;
                }

                char c = json[index];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == '-' || char.IsDigit(c)) return ParseNumber();
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;

                throw new FormatException("Unexpected JSON token at " + index + ".");
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> table = new Dictionary<string, object>();
                index++;

                while (true)
                {
                    EatWhitespace();
                    if (Peek('}'))
                    {
                        index++;
                        return table;
                    }

                    string key = ParseString();
                    EatWhitespace();
                    Expect(':');
                    table[key] = ParseValue();
                    EatWhitespace();

                    if (Peek(','))
                    {
                        index++;
                        continue;
                    }

                    Expect('}');
                    return table;
                }
            }

            private List<object> ParseArray()
            {
                List<object> list = new List<object>();
                index++;

                while (true)
                {
                    EatWhitespace();
                    if (Peek(']'))
                    {
                        index++;
                        return list;
                    }

                    list.Add(ParseValue());
                    EatWhitespace();

                    if (Peek(','))
                    {
                        index++;
                        continue;
                    }

                    Expect(']');
                    return list;
                }
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new StringBuilder();

                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (index >= json.Length)
                    {
                        break;
                    }

                    char escaped = json[index++];
                    if (escaped == '"' || escaped == '\\' || escaped == '/')
                    {
                        builder.Append(escaped);
                    }
                    else if (escaped == 'b')
                    {
                        builder.Append('\b');
                    }
                    else if (escaped == 'f')
                    {
                        builder.Append('\f');
                    }
                    else if (escaped == 'n')
                    {
                        builder.Append('\n');
                    }
                    else if (escaped == 'r')
                    {
                        builder.Append('\r');
                    }
                    else if (escaped == 't')
                    {
                        builder.Append('\t');
                    }
                    else if (escaped == 'u')
                    {
                        string hex = json.Substring(index, 4);
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                        index += 4;
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private object ParseNumber()
            {
                int start = index;
                if (Peek('-'))
                {
                    index++;
                }

                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }

                if (Peek('.'))
                {
                    index++;
                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }

                if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
                {
                    index++;
                    if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    {
                        index++;
                    }

                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }

                string number = json.Substring(start, index - start);
                double parsed;
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    throw new FormatException("Invalid JSON number: " + number);
                }

                return parsed;
            }

            private bool Match(string value)
            {
                if (index + value.Length > json.Length)
                {
                    return false;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    if (json[index + i] != value[i])
                    {
                        return false;
                    }
                }

                index += value.Length;
                return true;
            }

            private void EatWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }

            private bool Peek(char c)
            {
                return index < json.Length && json[index] == c;
            }

            private void Expect(char c)
            {
                if (!Peek(c))
                {
                    throw new FormatException("Expected '" + c + "' at " + index + ".");
                }

                index++;
            }
        }
    }
}
