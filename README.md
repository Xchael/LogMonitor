# LogMonitor
#Should this work as a console app or maybe a windows service? Will probably go with both and create a common dll for reuse

Decided to create services and interfaces for parsing the log file as well as handling the data inside it


# Workings:
The application will parse the data lazely and store it into an IEnumerable<LogEntry>
The JobMonitorService will receive the parsed data and store it inside JobInfo objects after grouping by PID and isStart
The JobInfo object contains  start time and end time and will calculate the duration