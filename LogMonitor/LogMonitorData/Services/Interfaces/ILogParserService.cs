using LogMonitorData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorData.Services.Interfaces
{
    public interface ILogParserService
    {
        IEnumerable<LogEntry> ParseLogEntries(string filePath);
    }
}
