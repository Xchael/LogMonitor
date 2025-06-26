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
            _logger.LogInformation($"Starting job analysis on {entries.Count()} entries.");

            var activeJobs = new Dictionary<int, LogEntry>();

            // Collects all completed jobs to return
            var completedJobs = new List<JobInfo>();

            // Process entries in chronological order
            //Will make use of PID a lot here
            //Assumption: PID is SOT
            foreach (var entry in entries.OrderBy(e => e.TimeStamp))
            {
                if (entry.IsStart)
                {
                    // START of a new job
                    if (activeJobs.ContainsKey(entry.Pid))
                    {
                        _logger.LogWarning($"Duplicate START for PID {entry.Pid}, job '{entry.JobDescription}' at {entry.TimeStamp}. Previous START at {activeJobs[entry.Pid].TimeStamp} will be ignored.");
                    }

                    //Maybe edge case? register again?
                    activeJobs[entry.Pid] = entry;
                }
                else
                {
                    // END of a job
                    if (!activeJobs.TryGetValue(entry.Pid, out var startEntry))
                    {
                        // No matching START found
                        _logger.LogWarning($"Unmatched END for PID {entry.Pid}, job '{entry.JobDescription}' at {entry.TimeStamp}.");
                        continue;
                    }

                    // Maybe edge case? If the END timestamp is before the START, skip it?
                    if (entry.TimeStamp < startEntry.TimeStamp)
                    {
                        _logger.LogWarning($"END before START for PID {entry.Pid}, job '{entry.JobDescription}': start={startEntry.TimeStamp}, end={entry.TimeStamp}. Skipping.");
                        activeJobs.Remove(entry.Pid);
                        continue;
                    }

                    // Build the completed JobInfo
                    var job = new JobInfo
                    {
                        PID = entry.Pid,
                        Description = startEntry.JobDescription,
                        StartTime = startEntry.TimeStamp,
                        EndTime = entry.TimeStamp
                    };

                    // Calculate duration in minutes
                    var duration = job.Duration.TotalMinutes;

                    // Emit appropriate log level based on thresholds
                    if (duration > ErrorThresholdMinutes)
                    {
                        _logger.LogError($"Job '{job.Description}' (PID {job.PID}) took {duration:F2} min (> {ErrorThresholdMinutes} min).");
                    }
                    else if (duration > WarningThresholdMinutes)
                    {
                        _logger.LogWarning($"Job '{job.Description}' (PID {job.PID}) took {duration:F2} min (> {WarningThresholdMinutes} min).");
                    }
                    else
                    {
                        _logger.LogInformation($"Job '{job.Description}' (PID {job.PID}) completed in {duration:F2} min.");
                    }

                    completedJobs.Add(job);
                    activeJobs.Remove(entry.Pid);
                }
            }

            // After processing all entries, any remaining STARTs never got an END
            if (activeJobs.Count > 0)
            {
                foreach (var orphan in activeJobs.Values)
                {
                    _logger.LogWarning($"No matching END for START of PID {orphan.Pid}, job '{orphan.JobDescription}' at {orphan.TimeStamp}.");
                }
            }

            _logger.LogInformation($"Job analysis complete: {completedJobs.Count} jobs matched.");

            return completedJobs;
        }
    }
}
