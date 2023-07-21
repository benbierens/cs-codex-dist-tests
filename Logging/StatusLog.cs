﻿using Newtonsoft.Json;

namespace Logging
{
    public class StatusLog
    {
        private readonly object fileLock = new object();
        private readonly string fullName;
        private readonly string fixtureName;
        private readonly string codexId;

        public StatusLog(LogConfig config, DateTime start, string codexId, string name = "")
        {
            fullName = NameUtils.GetFixtureFullName(config, start, name) + "_STATUS.log";
            fixtureName = NameUtils.GetRawFixtureName();
            this.codexId = codexId;
        }

        public void ConcludeTest(string resultStatus, string testDuration)
        {
            Write(new StatusLogJson
            {
                @timestamp = DateTime.UtcNow.ToString("o"),
                runid = NameUtils.GetRunId(),
                status = resultStatus,
                testid = NameUtils.GetTestId(),
                codexid = codexId,
                category = NameUtils.GetCategoryName(),
                fixturename = fixtureName,
                testname = NameUtils.GetTestMethodName(),
                testduration = testDuration
            });
        }

        private void Write(StatusLogJson json)
        {
            try
            {
                lock (fileLock)
                {
                    File.AppendAllLines(fullName, new[] { JsonConvert.SerializeObject(json) });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to write to status log: " + ex);
            }
        }
    }

    public class StatusLogJson
    {
        public string @timestamp { get; set; } = string.Empty;
        public string runid { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string testid { get; set; } = string.Empty; 
        public string codexid { get; set; } = string.Empty;
        public string category { get; set; } = string.Empty;
        public string fixturename { get; set; } = string.Empty;
        public string testname { get; set; } = string.Empty;
        public string testduration { get; set;} = string.Empty;
    }
}