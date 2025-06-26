using LogMonitorData.Models;
using LogMonitorData.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorData.Services
{
    public class JobMonitorService : IJobMonitorService
    {
        private readonly ILogger<JobMonitorService> _logger;
        private const double WarningThresholdMinutes = 5;
        private const double ErrorThresholdMinutes = 10;

        /// <summary>
        /// Service responsible for analyzing parsed log entries and producing
        /// JobInfo objects, with warnings/errors based on duration thresholds.
        /// </summary>
        public JobMonitorService(ILogger<JobMonitorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes a sequence of LogEntry records, pairing each START with its corresponding END,
        /// calculating durations, and emitting warnings or errors if thresholds are exceeded.
        /// </summary>
        /// <param name="entries">
        /// Collection of parsed log entries. Must not be null.
        /// </param>
        public IEnumerable<JobInfo> AnalyzeJobs(IEnumerable<LogEntry> entries)
        {
            //Null-check the input collection
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            //Log the start of analysis, including how many entries we'll process
            _logger.LogInformation("Starting job analysis on {Count} entries.", entries.Count());

            //Prepare a list to accumulate results
            var jobs = new List<JobInfo>();

            //Group entries by PID and Desc
            var grouped = entries.GroupBy(e => (e.Pid, e.IsStart));
            foreach (var group in grouped)
            {
                var starts = group.Where(e => e.IsStart).OrderBy(e => e.TimeStamp).ToList();
                var ends = group.Where(e => !e.IsStart).OrderBy(e => e.TimeStamp).ToList();

                //Warn if the count of START and END entries doesn't match
                if (starts.Count != ends.Count)
                {
                    _logger.LogWarning($"Mismatched start/end count for PID {group.Key.Pid}, Job '{group.Key.JobDescription}': {starts.Count} starts, {ends.Count} ends.");
                }

                //Pair up START and END by index, up to the shorter list length
                for (int i = 0; i < Math.Min(starts.Count, ends.Count); i++)
                {
                    var start = starts[i];
                    var end = ends[i];

                    if (end.TimeStamp < start.TimeStamp)
                    {
                        // Skip this malformed pair
                        _logger.LogWarning($"End time before start time for PID {start.Pid}, Job '{start.JobDescription}', start: {start.TimeStamp}, end: {end.TimeStamp}");
                        continue;
                    }

                    var job = new JobInfo
                    {
                        PID = start.Pid,
                        Description = start.JobDescription,
                        StartTime = start.TimeStamp,
                        EndTime = end.TimeStamp
                    };

                    var durationMin = job.Duration.TotalMinutes;
                    if (durationMin > ErrorThresholdMinutes)
                    {
                        _logger.LogError($"Job '{job.Description}' (PID {job.PID}) took {durationMin} minutes (exceeds {ErrorThresholdMinutes} minutes).");
                    }
                    else if (durationMin > WarningThresholdMinutes)
                    {
                        _logger.LogWarning($"Job '{job.Description}' (PID {job.PID}) took {durationMin} minutes (exceeds {WarningThresholdMinutes} minutes).");
                    }
                    else
                    {
                        _logger.LogInformation($"Job '{job.Description}' (PID {job.PID}) completed in {durationMin} minutes.");
                    }

                    jobs.Add(job);
                }
            }

            _logger.LogInformation($"Job analysis complete. {jobs.Count} jobs processed.");
            return jobs;
        }
    }
}
