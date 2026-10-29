using System;

/// <summary>
/// 严格 JSON 对象校验器。
///
/// Newtonsoft.Json 的 JToken.Parse 比较宽松，会接受：
///   {"a":1,}      尾随逗号
///   {'a':1}       单引号
///   {"a":NaN}     NaN 等非标准常量
/// 这些内容写进配置数据后，独立检查工具/Python json 等严格解析器仍会认为非法。
///
/// 本类只做语法校验，不负责反序列化。规则：
///   - 顶层必须是对象；
///   - 字符串必须使用双引号，禁止未转义控制字符；
///   - 数字必须符合 JSON number 语法，禁止前导 0、.5、1. 等；
///   - 禁止尾随逗号、注释、单引号、NaN/Infinity；
///   - 校验结束后不允许存在多余内容。
/// </summary>
public static class StrictJsonValidator
{
    private const int MaxDepth = 128;

    public static bool TryValidateObject(string json, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(json))
        {
            error = "JSON 为空";
            return false;
        }

        var parser = new Parser(json);
        parser.SkipWhitespace();
        if (parser.IsEnd)
        {
            error = "JSON 为空";
            return false;
        }
        if (parser.Current != '{')
        {
            error = "JSON 顶层必须是对象";
            return false;
        }

        if (!parser.ParseObject(0, out error))
            return false;

        parser.SkipWhitespace();
        if (!parser.IsEnd)
        {
            error = parser.Position("JSON 结束后存在多余内容");
            return false;
        }
        return true;
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text)
        {
            _text = text;
            _index = 0;
        }

        public bool IsEnd => _index >= _text.Length;

        public char Current => _text[_index];

        public string Position(string message)
        {
            int position = Math.Min(_index + 1, Math.Max(1, _text.Length));
            return $"{message}（位置 {position}）";
        }

        public void SkipWhitespace()
        {
            while (!IsEnd)
            {
                char c = _text[_index];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    _index++;
                    continue;
                }
                break;
            }
        }

        public bool ParseObject(int depth, out string error)
        {
            error = null;
            if (depth > MaxDepth)
            {
                error = Position("JSON 嵌套层级过深");
                return false;
            }

            _index++; // {
            SkipWhitespace();
            if (!IsEnd && Current == '}')
            {
                _index++;
                return true;
            }

            while (true)
            {
                SkipWhitespace();
                if (IsEnd)
                {
                    error = Position("对象未闭合");
                    return false;
                }
                if (Current != '"')
                {
                    error = Position("对象键必须使用双引号");
                    return false;
                }

                if (!ParseString(out error))
                    return false;

                SkipWhitespace();
                if (IsEnd || Current != ':')
                {
                    error = Position("对象键后缺少冒号");
                    return false;
                }
                _index++;

                if (!ParseValue(depth + 1, out error))
                    return false;

                SkipWhitespace();
                if (IsEnd)
                {
                    error = Position("对象未闭合");
                    return false;
                }

                if (Current == ',')
                {
                    _index++;
                    SkipWhitespace();
                    if (IsEnd || Current == '}')
                    {
                        error = Position("对象不允许尾随逗号");
                        return false;
                    }
                    continue;
                }

                if (Current == '}')
                {
                    _index++;
                    return true;
                }

                error = Position("对象中缺少逗号或右花括号");
                return false;
            }
        }

        private bool ParseArray(int depth, out string error)
        {
            error = null;
            if (depth > MaxDepth)
            {
                error = Position("JSON 嵌套层级过深");
                return false;
            }

            _index++; // [
            SkipWhitespace();
            if (!IsEnd && Current == ']')
            {
                _index++;
                return true;
            }

            while (true)
            {
                if (!ParseValue(depth + 1, out error))
                    return false;

                SkipWhitespace();
                if (IsEnd)
                {
                    error = Position("数组未闭合");
                    return false;
                }

                if (Current == ',')
                {
                    _index++;
                    SkipWhitespace();
                    if (IsEnd || Current == ']')
                    {
                        error = Position("数组不允许尾随逗号");
                        return false;
                    }
                    continue;
                }

                if (Current == ']')
                {
                    _index++;
                    return true;
                }

                error = Position("数组中缺少逗号或右方括号");
                return false;
            }
        }

        private bool ParseValue(int depth, out string error)
        {
            error = null;
            SkipWhitespace();
            if (IsEnd)
            {
                error = Position("JSON 值缺失");
                return false;
            }

            char c = Current;
            if (c == '{') return ParseObject(depth, out error);
            if (c == '[') return ParseArray(depth, out error);
            if (c == '"') return ParseString(out error);
            if (c == 't') return ParseLiteral("true", out error);
            if (c == 'f') return ParseLiteral("false", out error);
            if (c == 'n') return ParseLiteral("null", out error);
            if (c == '-' || IsDigit09(c)) return ParseNumber(out error);

            error = Position($"非法字符 '{c}'");
            return false;
        }

        private bool ParseString(out string error)
        {
            error = null;
            _index++; // opening quote

            while (!IsEnd)
            {
                char c = _text[_index++];
                if (c == '"')
                    return true;

                if (c < 0x20)
                {
                    error = Position("字符串包含未转义的控制字符");
                    return false;
                }

                if (c != '\\')
                    continue;

                if (IsEnd)
                {
                    error = Position("字符串转义未完成");
                    return false;
                }

                char escaped = _text[_index++];
                if (escaped == '"' || escaped == '\\' || escaped == '/' ||
                    escaped == 'b' || escaped == 'f' || escaped == 'n' ||
                    escaped == 'r' || escaped == 't')
                {
                    continue;
                }

                if (escaped == 'u')
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (IsEnd || !IsHex(_text[_index++]))
                        {
                            error = Position("\\u 转义必须包含 4 位十六进制");
                            return false;
                        }
                    }
                    continue;
                }

                error = Position($"非法转义 \\{escaped}");
                return false;
            }

            error = Position("字符串未闭合");
            return false;
        }

        private bool ParseNumber(out string error)
        {
            error = null;

            if (!IsEnd && Current == '-')
                _index++;

            if (IsEnd)
            {
                error = Position("数字不完整");
                return false;
            }

            if (Current == '0')
            {
                _index++;
                if (!IsEnd && IsDigit09(Current))
                {
                    error = Position("数字不允许前导 0");
                    return false;
                }
            }
            else if (Current >= '1' && Current <= '9')
            {
                _index++;
                while (!IsEnd && IsDigit09(Current))
                    _index++;
            }
            else
            {
                error = Position("数字格式非法");
                return false;
            }

            if (!IsEnd && Current == '.')
            {
                _index++;
                if (IsEnd || !IsDigit09(Current))
                {
                    error = Position("小数点后必须至少有一位数字");
                    return false;
                }
                while (!IsEnd && IsDigit09(Current))
                    _index++;
            }

            if (!IsEnd && (Current == 'e' || Current == 'E'))
            {
                _index++;
                if (!IsEnd && (Current == '+' || Current == '-'))
                    _index++;
                if (IsEnd || !IsDigit09(Current))
                {
                    error = Position("指数部分必须至少有一位数字");
                    return false;
                }
                while (!IsEnd && IsDigit09(Current))
                    _index++;
            }

            return true;
        }

        private bool ParseLiteral(string literal, out string error)
        {
            error = null;
            if (_index + literal.Length > _text.Length ||
                string.Compare(_text, _index, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
            {
                error = Position($"非法字面量，应为 {literal}");
                return false;
            }
            _index += literal.Length;
            return true;
        }

        private static bool IsDigit09(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
        }
    }
}
