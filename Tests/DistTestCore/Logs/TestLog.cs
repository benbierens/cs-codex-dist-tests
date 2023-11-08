﻿namespace DistTestCore.Logs
{
    public class TestLog : BaseTestLog
    {
        private readonly string methodName;
        private readonly string fullName;

        public TestLog(string folder, string name = "")
        {
            methodName = NameUtils.GetTestMethodName(name);
            fullName = Path.Combine(folder, methodName);

            Log($"*** Begin: {methodName}");
        }

        protected override string GetFullName()
        {
            return fullName;
        }
    }
}
