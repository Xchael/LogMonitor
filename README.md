# Log monitor
An amazing application used to monitor log files.
This is a sample project.

# Thoughts
Should this work as a console app or maybe a windows service? Will probably go with both and create a common dll for reuse
Decided to create services and interfaces for parsing the log file as well as handling the data inside it
Considered using a big file, the worker finishes parsing and outputing fast. Maybe some optimization at AnalyzeJobs level
Thought about using structured logging (avoids string interpolation) to defer message formatting

# Usage:
The application will parse the data lazely and store it into an IEnumerable<LogEntry>
The JobMonitorService will receive the parsed data and store it inside JobInfo objects after grouping by PID and isStart
The JobInfo object contains  start time and end time and will calculate the duration
Output will be a full Job Report list as well as jobs that got flagged with _logger.LogWarning or _logger.LogError.
I also logged info for jobs that completed without passing the desired times

The LogMonitorService will be running as a windows service every 10H (set from appsettings)
It will do mostly the same thing as the console application for now, but it can be extended to log into Windows event log, parse the same file again and again for new data 
(in the current implementation a orphaned processes will not be reconciliated but that is doable as well)

# Disclaimer:
Some commits will have the name "adria". This was not intended but i did a bit of "impersonation" on another repo i worked on and i forgot to switch back the VS setting.
Check attached screenshot. Sorry for possible confusion!