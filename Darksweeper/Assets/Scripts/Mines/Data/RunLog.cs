using System.Collections.Generic;

namespace Mines.Data
{
    /// <summary>
    /// Append-only log of all mine events in the current run.
    /// Ready for JSON serialization for narrative/god alignment system.
    /// </summary>
    public class RunLog
    {
        public List<RunEvent> events = new();
        public int nextIndex = 0;

        public void Record(RunEvent e)
        {
            e.eventIndex = nextIndex++;
            events.Add(e);
        }

        public void Clear()
        {
            events.Clear();
            nextIndex = 0;
        }
    }
}
