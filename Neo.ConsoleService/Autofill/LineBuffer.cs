using System.Collections.Generic;

namespace Neo.ConsoleService
{
    class LineBuffer
    {
        private readonly List<string> _previousLines = new List<string>();

        public bool HasLines { get { return _previousLines.Count > 0; } }
        public string LineAtIndex { get { return _previousLines.Count == 0 ? null : _previousLines[Index]; } }

        public int Index { get; set; }

        public void AddLine(string line)
        {
            if (!string.IsNullOrEmpty(line))
                _previousLines.Add(line);

            Index = _previousLines.Count - 1;
        }

        public bool CycleUp()
        {
            if (!HasLines)
                return false;

            Index--;

            if (Index < 0)
                Index = _previousLines.Count - 1;

            return true;
        }

        public bool CycleDown()
        {
            if (!HasLines)
                return false;

            Index++;

            if (Index > _previousLines.Count - 1)
            {
                Index = _previousLines.Count - 1;
                return false;
            }

            return true;
        }
    }
}
