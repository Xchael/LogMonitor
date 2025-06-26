using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorData.Models
{
    public class JobInfo
    {
        public int PID { get; set; }
        public string? Description { get; set; }
        public TimeSpan StartTime{ get; set; }
        public TimeSpan EndTime{ get; set; }
        //calculate duration
        public TimeSpan Duration => EndTime - StartTime;
    }
}
