using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitor.MonitorUtils
{
    public class LogMonitorSettings
    {
        public string LogsFolder { get; set; } = "Logs";

        public string LogFileName { get; set; } = "logs.log";
    }
}
