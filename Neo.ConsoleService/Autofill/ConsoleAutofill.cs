// Copyright (C) 2016-2021 The Neo Project.
// This file belongs to Neo.ConsoleService
//
// The Neo.ConsoleService is free software distributed under the MIT 
// software license, see the accompanying file LICENSE in the main directory
// of the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
        internal static readonly LineBuffer lineBuffer = new LineBuffer();
        public string Autofill(string line, List<string> strings, bool ignoreCase)
        {
            if (IsPreviousCycle(line))
                return ContinueCycle();

            _AutofillList = GetAutofillPossibilities(line, strings, ignoreCase);
            if (_AutofillList.Count == 0)
                return line;
            return StartNewCycle();
        }
        public static void AddToBuffer(string command)
        {
            lineBuffer.AddLine(command);
        }

        public static string CycleUp()
        {
            if (lineBuffer.HasLines)
            {
                lineBuffer.CycleUp();
                return lineBuffer.LineAtIndex;
            }
            return null;
        }

        public static string CycleDown()
        {
            if (lineBuffer.HasLines)
            {
                if (!lineBuffer.CycleDown()) return "";
                return lineBuffer.LineAtIndex;
            }
            return null;
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
