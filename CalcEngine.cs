using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniCalc
{
    /// <summary>
    /// Immediate-execution arithmetic (each new operator folds the pending one).
    /// </summary>
    sealed class CalcEngine
    {
        const int MaxDigits = 12;

        enum Phase
        {
            Editing,
            AwaitOperand,
            Result
        }

        string _display = "0";
        double _acc;
        string _op;
        Phase _phase = Phase.Result;
        bool _error;
        bool _repeat;
        string _lastOp;
        double _lastRight;
        string _lastRecord;

        public string Expression { get; private set; }

        public bool HasError
        {
            get { return _error; }
        }

        public string ActiveOperator
        {
            get { return _phase == Phase.AwaitOperand ? _op : null; }
        }

        public string DisplayText
        {
            get { return _error ? _display : Pretty(_display); }
        }

        public void Clear()
        {
            _display = "0";
            _acc = 0;
            _op = null;
            _phase = Phase.Result;
            _error = false;
            _repeat = false;
            _lastOp = null;
            _lastRight = 0;
            _lastRecord = null;
            Expression = "";
        }

        public string TakeRecord()
        {
            string line = _lastRecord;
            _lastRecord = null;
            return line;
        }

        public void InputDigit(char d)
        {
            if (d < '0' || d > '9') return;
            if (_error) Clear();

            if (_phase != Phase.Editing)
            {
                _display = d == '0' ? "0" : d.ToString();
                _phase = Phase.Editing;
                _repeat = false;
                return;
            }

            if (_display == "0")
            {
                if (d != '0') _display = d.ToString();
                return;
            }

            if (_display == "-" || _display == "-0")
            {
                _display = d == '0' ? "-0" : "-" + d;
                return;
            }

            if (CountDigits(_display) >= MaxDigits) return;
            _display += d;
        }

        public void InputDot()
        {
            if (_error) Clear();
            if (_phase != Phase.Editing)
            {
                _display = "0.";
                _phase = Phase.Editing;
                _repeat = false;
                return;
            }

            if (_display.IndexOf('.') < 0)
                _display += ".";
        }

        public void Backspace()
        {
            if (_error)
            {
                Clear();
                return;
            }

            if (_phase == Phase.AwaitOperand) return;

            if (_phase == Phase.Result)
            {
                _phase = Phase.Editing;
                _repeat = false;
            }

            if (_display == "0" || _display == "-0")
            {
                _display = "0";
                return;
            }

            string next = _display.Substring(0, _display.Length - 1);
            if (next.Length == 0 || next == "-" || next == "-.")
                next = "0";
            _display = next;
        }

        public void ToggleSign()
        {
            if (_error) return;

            if (_phase == Phase.AwaitOperand)
            {
                _display = "-0";
                _phase = Phase.Editing;
                _repeat = false;
                return;
            }

            if (_display.StartsWith("-"))
                _display = _display.Substring(1);
            else if (_display != "0" && _display != "0.")
                _display = "-" + _display;

            if (_phase == Phase.Result)
                _repeat = false;
        }

        public void Percent()
        {
            if (_error) Clear();

            double cur = Parse(_display);
            double result = (_op == "+" || _op == "-") ? _acc * cur / 100.0 : cur / 100.0;
            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                Fail("错误");
                return;
            }

            _display = Format(result);
            _phase = Phase.Editing;
            _repeat = false;
        }

        public void SetOperator(string newOp)
        {
            if (_error) Clear();

            if (_phase == Phase.Editing && _op != null)
            {
                if (!ComputeIntoAcc()) return;
            }
            else
            {
                _acc = Parse(_display);
            }

            _op = newOp;
            _phase = Phase.AwaitOperand;
            _repeat = false;
            _display = Format(_acc);
            Expression = Pretty(_display) + " " + Symbol(_op);
        }

        public void Evaluate()
        {
            if (_error)
            {
                Clear();
                return;
            }

            if (_op == null && !_repeat)
            {
                _display = Format(Parse(_display));
                _phase = Phase.Result;
                Expression = "";
                return;
            }

            string op;
            double right;
            if (_repeat && _op == null)
            {
                op = _lastOp;
                right = _lastRight;
                _acc = Parse(_display);
            }
            else if (_phase == Phase.AwaitOperand)
            {
                op = _op;
                right = _acc;
            }
            else
            {
                op = _op;
                right = Parse(_display);
            }

            if (op == null)
            {
                _display = Format(Parse(_display));
                _phase = Phase.Result;
                Expression = "";
                return;
            }

            double result;
            if (!Apply(op, _acc, right, out result)) return;

            Note(_acc, op, right, result);
            _lastOp = op;
            _lastRight = right;
            _acc = result;
            _display = Format(result);
            _op = null;
            _phase = Phase.Result;
            _repeat = true;
            Expression = "";
        }

        public bool TryPaste(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var sb = new StringBuilder(text.Length);
            foreach (char ch in text.Trim())
            {
                if (ch == ',' || ch == '，' || ch == ' ' || ch == '\u00A0') continue;
                if (ch == '．' || ch == '。')
                {
                    sb.Append('.');
                    continue;
                }
                if (ch >= '０' && ch <= '９')
                {
                    sb.Append((char)('0' + (ch - '０')));
                    continue;
                }
                sb.Append(ch);
            }

            string raw = sb.ToString();
            if (raw.StartsWith("+")) raw = raw.Substring(1);
            if (!Regex.IsMatch(raw, @"^-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$")) return false;
            if (CountDigits(raw) > MaxDigits) return false;

            double value = Parse(raw);
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            if (_error) Clear();
            _display = Format(value);
            _phase = Phase.Editing;
            _repeat = false;
            return true;
        }

        bool ComputeIntoAcc()
        {
            double left = _acc;
            double right = Parse(_display);
            double result;
            if (!Apply(_op, left, right, out result)) return false;
            Note(left, _op, right, result);
            _lastOp = _op;
            _lastRight = right;
            _acc = result;
            _display = Format(result);
            return true;
        }

        void Note(double left, string op, double right, double result)
        {
            _lastRecord = Pretty(Format(left)) + " " + Symbol(op) + " " + Pretty(Format(right)) + " = " + Pretty(Format(result));
        }

        bool Apply(string op, double left, double right, out double result)
        {
            result = 0;
            switch (op)
            {
                case "+": result = left + right; break;
                case "-": result = left - right; break;
                case "*": result = left * right; break;
                case "/":
                    if (right == 0)
                    {
                        Fail("不能除以 0");
                        return false;
                    }
                    result = left / right;
                    break;
                default:
                    result = right;
                    break;
            }

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                Fail("错误");
                return false;
            }

            return true;
        }

        void Fail(string message)
        {
            _error = true;
            _display = message;
            Expression = "";
            _op = null;
            _phase = Phase.Result;
            _repeat = false;
            _acc = 0;
        }

        static int CountDigits(string raw)
        {
            int n = 0;
            foreach (char c in raw)
            {
                if (c >= '0' && c <= '9') n++;
            }
            if (raw.StartsWith("0.") || raw.StartsWith("-0.")) n--;
            return n < 0 ? 0 : n;
        }

        static double Parse(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "-" || s == "." || s == "-.") return 0;
            if (s.EndsWith(".")) s = s.Substring(0, s.Length - 1);
            if (s.Length == 0 || s == "-") return 0;
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return 0;
            return v;
        }

        public static string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "错误";
            if (v == 0) return "0";

            double abs = Math.Abs(v);
            if (abs >= 1e-8 && abs < 1e15)
            {
                string rounded = v.ToString("G12", CultureInfo.InvariantCulture);
                double rv;
                if (!double.TryParse(rounded, NumberStyles.Float, CultureInfo.InvariantCulture, out rv))
                    rv = v;
                string s = rv.ToString("0.############", CultureInfo.InvariantCulture);
                if (s == "-0") return "0";
                return s;
            }

            return v.ToString("G8", CultureInfo.InvariantCulture);
        }

        public static string Pretty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "0";
            if (raw.IndexOf('E') >= 0 || raw.IndexOf('e') >= 0) return raw;

            bool neg = raw[0] == '-';
            string s = neg ? raw.Substring(1) : raw;
            if (s.Length == 0) s = "0";

            bool trailDot = s.EndsWith(".");
            if (trailDot) s = s.Substring(0, s.Length - 1);

            int dot = s.IndexOf('.');
            string ip = dot >= 0 ? s.Substring(0, dot) : s;
            string fp = dot >= 0 ? s.Substring(dot) : "";
            if (ip.Length == 0) ip = "0";

            int z = 0;
            while (z < ip.Length - 1 && ip[z] == '0') z++;
            ip = ip.Substring(z);

            var sb = new StringBuilder();
            for (int k = 0; k < ip.Length; k++)
            {
                sb.Append(ip[k]);
                int remain = ip.Length - k - 1;
                if (remain > 0 && remain % 3 == 0) sb.Append(',');
            }
            if (fp.Length > 0) sb.Append(fp);
            if (trailDot) sb.Append('.');
            if (neg) sb.Insert(0, '-');
            return sb.ToString();
        }

        static string Symbol(string op)
        {
            switch (op)
            {
                case "+": return "+";
                case "-": return "−";
                case "*": return "×";
                case "/": return "÷";
                default: return "";
            }
        }
    }
}
