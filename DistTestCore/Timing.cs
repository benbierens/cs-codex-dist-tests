﻿using NUnit.Framework;

namespace DistTestCore
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UseLongTimeoutsAttribute : PropertyAttribute
    {
    }

    public interface ITimeSet
    {
        TimeSpan HttpCallTimeout();
        TimeSpan HttpCallRetryTime();
        TimeSpan HttpCallRetryDelay();
        TimeSpan WaitForK8sServiceDelay();
        TimeSpan K8sOperationTimeout();
        TimeSpan WaitForMetricTimeout();
    }

    public class DefaultTimeSet : ITimeSet
    {
        public TimeSpan HttpCallTimeout()
        {
            return TimeSpan.FromSeconds(10);
        }

        public TimeSpan HttpCallRetryTime()
        {
            return TimeSpan.FromMinutes(1);
        }

        public TimeSpan HttpCallRetryDelay()
        {
            return TimeSpan.FromSeconds(1);
        }

        public TimeSpan WaitForK8sServiceDelay()
        {
            return TimeSpan.FromSeconds(1);
        }

        public TimeSpan K8sOperationTimeout()
        {
            return TimeSpan.FromMinutes(1);
        }

        public TimeSpan WaitForMetricTimeout()
        {
            return TimeSpan.FromSeconds(30);
        }
    }

    public class LongTimeSet : ITimeSet
    {
        public TimeSpan HttpCallTimeout()
        {
            return TimeSpan.FromHours(2);
        }

        public TimeSpan HttpCallRetryTime()
        {
            return TimeSpan.FromHours(5);
        }

        public TimeSpan HttpCallRetryDelay()
        {
            return TimeSpan.FromSeconds(2);
        }

        public TimeSpan WaitForK8sServiceDelay()
        {
            return TimeSpan.FromSeconds(10);
        }

        public TimeSpan K8sOperationTimeout()
        {
            return TimeSpan.FromMinutes(15);
        }

        public TimeSpan WaitForMetricTimeout()
        {
            return TimeSpan.FromMinutes(5);
        }
    }
}
