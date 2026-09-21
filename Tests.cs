using System;

namespace MiniCalc
{
    static class Tests
    {
        static int _fails;

        static void Check(string name, string got, string expect)
        {
            if (got != expect)
            {
                Console.WriteLine("FAIL " + name + " got=[" + got + "] expect=[" + expect + "]");
                _fails++;
            }
        }

        static CalcEngine New()
        {
            var e = new CalcEngine();
            e.Clear();
            return e;
        }

        static void Main()
        {
            var e = New();
            e.InputDigit('1'); e.InputDigit('2'); e.SetOperator("+");
            e.InputDigit('3'); e.InputDigit('4'); e.Evaluate();
            Check("12+34", e.DisplayText, "46");
            Check("expr cleared", e.Expression, "");
            Check("record", e.TakeRecord() ?? "", "12 + 34 = 46");
            Check("record once", e.TakeRecord() ?? "", "");

            e = New();
            e.InputDigit('0'); e.InputDot(); e.InputDigit('1');
            e.SetOperator("+");
            e.InputDigit('0'); e.InputDot(); e.InputDigit('2');
            e.Evaluate();
            Check("0.1+0.2", e.DisplayText, "0.3");

            e = New();
            e.InputDigit('1'); e.InputDot(); e.InputDigit('1');
            e.SetOperator("+");
            e.InputDigit('2'); e.InputDot(); e.InputDigit('2');
            e.Evaluate();
            Check("1.1+2.2", e.DisplayText, "3.3");

            e = New();
            e.InputDigit('1'); e.SetOperator("+");
            e.InputDigit('2'); e.SetOperator("+");
            Check("running 1+2", e.DisplayText, "3");
            Check("expr", e.Expression, "3 +");
            e.InputDigit('3'); e.Evaluate();
            Check("1+2+3", e.DisplayText, "6");

            e = New();
            e.InputDigit('2'); e.SetOperator("+");
            e.InputDigit('3'); e.SetOperator("*");
            Check("2+3 then times shows 5", e.DisplayText, "5");
            Check("expr mul", e.Expression, "5 ×");
            e.InputDigit('4'); e.Evaluate();
            Check("(2+3)*4", e.DisplayText, "20");

            e = New();
            e.InputDigit('2'); e.InputDigit('0'); e.InputDigit('0');
            e.SetOperator("+");
            e.InputDigit('1'); e.InputDigit('0');
            e.Percent();
            Check("200+10% value", e.DisplayText, "20");
            e.Evaluate();
            Check("200+10%", e.DisplayText, "220");

            e = New();
            e.InputDigit('2'); e.InputDigit('0'); e.InputDigit('0');
            e.SetOperator("*");
            e.InputDigit('1'); e.InputDigit('0');
            e.Percent(); e.Evaluate();
            Check("200*10%", e.DisplayText, "20");

            e = New();
            e.InputDigit('2'); e.InputDigit('0'); e.InputDigit('0');
            e.SetOperator("/");
            e.InputDigit('1'); e.InputDigit('0');
            e.Percent(); e.Evaluate();
            Check("200/10%", e.DisplayText, "2,000");

            e = New();
            e.InputDigit('5'); e.InputDigit('0'); e.Percent();
            Check("50%", e.DisplayText, "0.5");

            e = New();
            e.InputDigit('5'); e.SetOperator("+"); e.Evaluate();
            Check("5+=", e.DisplayText, "10");
            e.Evaluate();
            Check("repeat +", e.DisplayText, "15");

            e = New();
            e.InputDigit('1'); e.InputDigit('0'); e.SetOperator("-");
            e.InputDigit('3'); e.Evaluate();
            Check("10-3", e.DisplayText, "7");
            e.Evaluate();
            Check("repeat -", e.DisplayText, "4");
            e.Evaluate();
            Check("repeat - again", e.DisplayText, "1");

            e = New();
            e.InputDigit('2'); e.SetOperator("*");
            e.InputDigit('3'); e.Evaluate();
            Check("2*3", e.DisplayText, "6");
            e.Evaluate();
            Check("repeat *", e.DisplayText, "18");

            e = New();
            e.InputDigit('8'); e.SetOperator("/");
            e.InputDigit('2'); e.Evaluate();
            Check("8/2", e.DisplayText, "4");
            e.Evaluate();
            Check("repeat /", e.DisplayText, "2");

            e = New();
            e.InputDigit('9'); e.SetOperator("/");
            e.InputDigit('0'); e.Evaluate();
            Check("div0", e.DisplayText, "不能除以 0");
            Check("err flag", e.HasError ? "yes" : "no", "yes");
            e.InputDigit('4');
            Check("recover", e.DisplayText, "4");

            e = New();
            e.InputDigit('8'); e.ToggleSign();
            Check("neg", e.DisplayText, "-8");
            e.SetOperator("/");
            e.InputDigit('2'); e.Evaluate();
            Check("-8/2", e.DisplayText, "-4");

            e = New();
            e.InputDigit('5'); e.SetOperator("+");
            e.ToggleSign(); e.InputDigit('3'); e.Evaluate();
            Check("5+-3", e.DisplayText, "2");

            e = New();
            e.InputDigit('1'); e.InputDigit('2'); e.InputDigit('3');
            e.Backspace();
            Check("bs", e.DisplayText, "12");
            e.Backspace(); e.Backspace(); e.Backspace();
            Check("bs to 0", e.DisplayText, "0");

            e = New();
            e.InputDigit('1'); e.InputDigit('2'); e.SetOperator("+");
            e.InputDigit('3'); e.InputDigit('4'); e.Evaluate();
            e.Backspace();
            Check("bs result", e.DisplayText, "4");

            e = New();
            e.InputDot(); e.InputDigit('5');
            Check("dot", e.DisplayText, "0.5");
            e.InputDot();
            Check("one dot", e.DisplayText, "0.5");

            e = New();
            e.InputDigit('0'); e.InputDigit('0'); e.InputDigit('5');
            Check("lead zero", e.DisplayText, "5");

            e = New();
            for (int i = 0; i < 13; i++) e.InputDigit('9');
            Check("max digits", e.DisplayText, "999,999,999,999");

            e = New();
            e.InputDigit('1'); e.InputDigit('0'); e.InputDigit('0'); e.InputDigit('0');
            e.SetOperator("+");
            Check("group expr", e.Expression, "1,000 +");
            e.SetOperator("-");
            Check("replace op", e.Expression, "1,000 −");
            Check("still 1000", e.DisplayText, "1,000");

            e = New();
            e.InputDigit('1'); e.InputDigit('2'); e.Clear();
            Check("clear", e.DisplayText, "0");
            Check("clear expr", e.Expression, "");

            e = New();
            e.InputDigit('3'); e.Evaluate();
            Check("eq noop", e.DisplayText, "3");

            e = New();
            e.InputDigit('1'); e.SetOperator("*");
            e.InputDigit('0'); e.InputDot(); e.InputDigit('1'); e.Evaluate();
            Check("1*0.1", e.DisplayText, "0.1");

            e = New();
            Check("paste", e.TryPaste("1,234.50") ? "yes" : "no", "yes");
            Check("paste text", e.DisplayText, "1,234.5");
            Check("reject", e.TryPaste("12+3") ? "yes" : "no", "no");
            Check("unchanged", e.DisplayText, "1,234.5");

            e = New();
            e.InputDigit('2'); e.SetOperator("/"); e.InputDigit('3'); e.Evaluate();
            Check("2/3", e.DisplayText, "0.666666666667");

            e = New();
            e.InputDigit('1'); e.InputDigit('0'); e.InputDigit('0');
            e.SetOperator("-"); e.InputDigit('1'); e.InputDigit('0'); e.Percent(); e.Evaluate();
            Check("100-10%", e.DisplayText, "90");

            if (_fails == 0) Console.WriteLine("OK");
            else Console.WriteLine("FAILED " + _fails);
            Environment.Exit(_fails == 0 ? 0 : 1);
        }
    }
}
