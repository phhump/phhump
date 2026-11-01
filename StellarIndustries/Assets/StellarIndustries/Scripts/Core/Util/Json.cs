using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StellarIndustries.Core.Util
{
    /// <summary>
    /// Minimal, dependency-free JSON reader/writer. Produces Dictionary&lt;string, object&gt;,
    /// List&lt;object&gt;, double, string, bool and null. Engine-independent so the
    /// simulation can be tested outside Unity.
    /// </summary>
    public static class Json
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder(4096);
            Write(sb, value);
            return sb.ToString();
        }

        public static object Deserialize(string json)
        {
            if (json == null) throw new FormatException("JSON input is null");
            var p = new Parser(json);
            p.SkipWs();
            object result = p.ParseValue();
            p.SkipWs();
            if (!p.End) throw new FormatException("Unexpected trailing characters at " + p.Pos);
            return result;
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            switch (v)
            {
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: WriteDouble(sb, d); return;
                case float f: WriteDouble(sb, f); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case uint ui: sb.Append(ui.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary dict:
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry e in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                        sb.Append(':');
                        Write(sb, e.Value);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable list:
                    sb.Append('[');
                    bool f2 = true;
                    foreach (var item in list)
                    {
                        if (!f2) sb.Append(',');
                        f2 = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    return;
                default:
                    if (v is IConvertible) { WriteDouble(sb, Convert.ToDouble(v, CultureInfo.InvariantCulture)); return; }
                    throw new NotSupportedException("Json.Serialize: unsupported type " + v.GetType());
            }
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append('0'); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string s;
            public int Pos;
            public Parser(string s) { this.s = s; }
            public bool End => Pos >= s.Length;

            public void SkipWs()
            {
                while (Pos < s.Length && char.IsWhiteSpace(s[Pos])) Pos++;
            }

            char Peek() { if (End) throw new FormatException("Unexpected end of JSON"); return s[Pos]; }

            public object ParseValue()
            {
                SkipWs();
                char c = Peek();
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, Pos, word, 0, word.Length) != 0) throw new FormatException("Expected '" + word + "' at " + Pos);
                Pos += word.Length;
            }

            Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>();
                Pos++; SkipWs();
                if (Peek() == '}') { Pos++; return d; }
                while (true)
                {
                    SkipWs();
                    if (Peek() != '"') throw new FormatException("Expected key at " + Pos);
                    string key = ParseString();
                    SkipWs();
                    if (Peek() != ':') throw new FormatException("Expected ':' at " + Pos);
                    Pos++;
                    d[key] = ParseValue();
                    SkipWs();
                    char c = Peek(); Pos++;
                    if (c == '}') return d;
                    if (c != ',') throw new FormatException("Expected ',' or '}' at " + (Pos - 1));
                }
            }

            List<object> ParseArray()
            {
                var l = new List<object>();
                Pos++; SkipWs();
                if (Peek() == ']') { Pos++; return l; }
                while (true)
                {
                    l.Add(ParseValue());
                    SkipWs();
                    char c = Peek(); Pos++;
                    if (c == ']') return l;
                    if (c != ',') throw new FormatException("Expected ',' or ']' at " + (Pos - 1));
                }
            }

            string ParseString()
            {
                var sb = new StringBuilder();
                Pos++;
                while (true)
                {
                    char c = Peek(); Pos++;
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        char e = Peek(); Pos++;
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (Pos + 4 > s.Length) throw new FormatException("Bad unicode escape");
                                sb.Append((char)int.Parse(s.Substring(Pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                Pos += 4; break;
                            default: throw new FormatException("Bad escape at " + Pos);
                        }
                    }
                    else sb.Append(c);
                }
            }

            object ParseNumber()
            {
                int start = Pos;
                while (Pos < s.Length && "+-0123456789.eE".IndexOf(s[Pos]) >= 0) Pos++;
                if (start == Pos) throw new FormatException("Unexpected character '" + s[Pos] + "' at " + Pos);
                return double.Parse(s.Substring(start, Pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
