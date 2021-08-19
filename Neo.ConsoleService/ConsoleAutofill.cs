using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Neo.ConsoleService
{
    enum CyclingDirections
    {
        Forward,
        Backward
    }

    class ConsoleAutofill
    {
        private List<string> _AutofillList;
        private string _previousAutofill = string.Empty;
        private int _AutofillIndex;

        public string Autofill(string line, List<string> strings)
        {
            return Autofill(line, strings, CyclingDirections.Forward, true);
        }

        public string Autofill(string line, List<string> strings, CyclingDirections cyclingDirection)
        {
            return Autofill(line, strings, cyclingDirection, true);
        }

        public string Autofill(string line, List<string> strings, bool ignoreCase)
        {
            return Autofill(line, strings, CyclingDirections.Forward, ignoreCase);
        }

        public string Autofill(string line, List<string> strings, CyclingDirections cyclingDirection, bool ignoreCase)
        {
            if (IsPreviousCycle(line))
            {
                if (cyclingDirection == CyclingDirections.Forward)
                    return ContinueCycle();
                return ContinueCycleReverse();
            }

            _AutofillList = GetAutofillPossibilities(line, strings, ignoreCase);
            if (_AutofillList.Count == 0)
                return line;
            return StartNewCycle();
        }

        private string StartNewCycle()
        {
            _AutofillIndex = 0;
            var AutofillLine = _AutofillList[_AutofillIndex];
            _previousAutofill = AutofillLine;
            return AutofillLine;
        }

        private string ContinueCycle()
        {
            _AutofillIndex++;
            if (_AutofillIndex >= _AutofillList.Count)
                _AutofillIndex = 0;
            var AutofillLine = _AutofillList[_AutofillIndex];
            _previousAutofill = AutofillLine;
            return AutofillLine;
        }

        private string ContinueCycleReverse()
        {
            _AutofillIndex--;
            if (_AutofillIndex < 0)
                _AutofillIndex = _AutofillList.Count - 1;
            var AutofillLine = _AutofillList[_AutofillIndex];
            _previousAutofill = AutofillLine;
            return AutofillLine;
        }

        private bool IsPreviousCycle(string line)
        {
            return _AutofillList != null && _AutofillList.Count != 0 && _previousAutofill == line;
        }

        public static List<string> GetAutofillPossibilities(string input, List<string> strings, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(input) || strings == null || strings.Count == 0)
                return new List<string>(0);

            return strings.Where(s => s.StartsWith(input, ignoreCase, CultureInfo.InvariantCulture)).ToList();
        }

        public static string GetComplimentaryAutofill(string input, List<string> strings, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var possibilities = GetAutofillPossibilities(input, strings, ignoreCase);
            if (possibilities.Count == 0)
                return input;
            var leadingString = GetEqualLeadingString(possibilities, ignoreCase);
            if (leadingString.Length == input.Length)
                return input;
            return leadingString;
        }

        public static string GetEqualLeadingString(List<string> strings, bool ignoreCase = true)
        {
            if (strings == null || strings.Count == 0)
                return string.Empty;
            if (strings.Count == 1)
                return strings[0];

            var baseString = strings[0];
            var index = 0;
            var result = string.Empty;
            while (index < baseString.Length)
            {
                var currentChar = baseString[index];
                for (var i = 1; i < strings.Count; i++)
                {
                    var otherChar = strings[i][index];
                    if (ignoreCase)
                    {
                        if (char.ToUpperInvariant(otherChar) != char.ToUpperInvariant(currentChar))
                            return result;
                    }
                    else if (otherChar != currentChar)
                        return result;
                }
                result += currentChar;
                index++;
            }
            return result;
        }
    }
}
