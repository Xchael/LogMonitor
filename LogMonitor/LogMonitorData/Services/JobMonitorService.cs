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

            // Cache to avoid multiple enumeration + get a stable Count
            var entryList = entries as IList<LogEntry> ?? entries.ToList();
            int totalEntries = entryList.Count;

            //Log the start of analysis, including how many entries we'll process
            _logger.LogInformation("Starting job analysis on {TotalEntries} entries.", totalEntries);

            // Pre-allocate about half as many jobs as entries:
            int estimatedJobs = totalEntries / 2;
            var completedJobs = new List<JobInfo>(estimatedJobs);
            var activeJobs = new Dictionary<int, LogEntry>(estimatedJobs);

            // Process entries in chronological order
            //Will make use of PID a lot here
            //Assumption: PID is SOT+
            // Sort once
            var sorted = entryList.OrderBy(e => e.TimeStamp);

            foreach (var entry in sorted)
            {
                if (entry.IsStart)
                {
                    // START of a new job
                    if (activeJobs.TryGetValue(entry.Pid, out var prevStart))
                    {
                        _logger.LogWarning(
                            "Duplicate START for PID {Pid} ('{Desc}') at {Time}; previous at {PrevTime} ignored.",
                            entry.Pid,
                            entry.JobDescription,
                            entry.TimeStamp,
                            prevStart.TimeStamp);
                    }
                    activeJobs[entry.Pid] = entry;
                    continue;
                    //Maybe edge case? register or overwrite?
                }
                else
                {
                    // END of a job
                    if (!activeJobs.TryGetValue(entry.Pid, out var startEntry))
                    {
                        _logger.LogWarning(
                            "Unmatched END for PID {Pid} ('{Desc}') at {Time}.",
                            entry.Pid,
                            entry.JobDescription,
                            entry.TimeStamp);
                        continue;
                    }

                    // Maybe edge case? If the END timestamp is before the START, skip it?
                    if (entry.TimeStamp < startEntry.TimeStamp)
                    {
                        _logger.LogWarning(
                            "END before START for PID {Pid} ('{Desc}'): start={StartTime}, end={EndTime}. Skipping.",
                            entry.Pid,
                            entry.JobDescription,
                            startEntry.TimeStamp,
                            entry.TimeStamp);
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
                    var minutes = job.Duration.TotalMinutes;

                    // Emit appropriate log level based on thresholds
                    if (minutes > ErrorThresholdMinutes)
                    {
                        _logger.LogError(
                            "Job '{Desc}' (PID {Pid}) took {Minutes:F2} min (>{ErrorThresh} min).",
                            job.Description,
                            job.PID,
                            minutes,
                            ErrorThresholdMinutes);
                    }
                    else if (minutes > WarningThresholdMinutes)
                    {
                        _logger.LogWarning(
                            "Job '{Desc}' (PID {Pid}) took {Minutes:F2} min (>{WarnThresh} min).",
                            job.Description,
                            job.PID,
                            minutes,
                            WarningThresholdMinutes);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Job '{Desc}' (PID {Pid}) completed in {Minutes:F2} min.",
                            job.Description,
                            job.PID,
                            minutes);
                    }

                    completedJobs.Add(job);
                    activeJobs.Remove(entry.Pid);
                }
            }

            //After processing all entries, any remaining STARTs never got an END
            if (activeJobs.Count > 0)
            {
                foreach (var orphan in activeJobs.Values)
                {
                    _logger.LogWarning(
                        "No matching END for PID {Pid} ('{Desc}') at {Time}.",
                        orphan.Pid,
                        orphan.JobDescription,
                        orphan.TimeStamp);
                }
            }

            _logger.LogInformation($"Job analysis complete: {completedJobs.Count} jobs matched.");

            return completedJobs;
        }
    }
}
