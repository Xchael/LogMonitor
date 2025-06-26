using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorData.Models
{
    public class LogEntry
    {
        /// <summary>
        /// Represents a single log record from the CSV.
        /// </summary>
        public TimeSpan TimeStamp { get; set; }
        public string? JobDescription { get; set;  }
        public bool IsStart { get; set; }
        public int Pid { get; set; }
    }
}
