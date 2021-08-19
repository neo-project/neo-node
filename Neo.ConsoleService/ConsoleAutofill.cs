using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Neo.ConsoleService
{
    class ConsoleAutofill
    {
        private List<string> _AutofillList;
        private string _previousAutofill = string.Empty;
        private int _AutofillIndex;

        public string Autofill(string line, List<string> strings, bool ignoreCase)
        {
            if (IsPreviousCycle(line))
                return ContinueCycle();

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
    }
}
