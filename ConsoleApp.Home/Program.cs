using Services;

var configuration = ConfigurationHelper.LoadConfiguration();

SoftEOTLogService.ProcessLogs(configuration);