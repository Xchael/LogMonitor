using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorService.MonitorUtils
{
    public class LogMonitorSettings
    {
        public int IntervalHours { get; set; } = 10;

        public string LogsFolder { get; set; } = "Logs";

        public string LogFileName { get; set; } = "logs.log";
    }
}
