﻿using Logging;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Utils;

namespace DistTestCore
{
    public class Http
    {
        private readonly BaseLog log;
        private readonly ITimeSet timeSet;
        private readonly string ip;
        private readonly int port;
        private readonly string baseUrl;

        public Http(BaseLog log, ITimeSet timeSet, string ip, int port, string baseUrl)
        {
            this.log = log;
            this.timeSet = timeSet;
            this.ip = ip;
            this.port = port;
            this.baseUrl = baseUrl;

            if (!this.baseUrl.StartsWith("/")) this.baseUrl = "/" + this.baseUrl;
            if (!this.baseUrl.EndsWith("/")) this.baseUrl += "/";
        }

        public string HttpGetString(string route)
        {
            return Retry(() =>
            {
                using var client = GetClient();
                var url = GetUrl() + route;
                Log(url, "");
                var result = Time.Wait(client.GetAsync(url));
                var str = Time.Wait(result.Content.ReadAsStringAsync());
                Log(url, str);
                return str; ;
            });
        }

        public T HttpGetJson<T>(string route)
        {
            var json = HttpGetString(route);
            return TryJsonDeserialize<T>(json);
        }

        public TResponse HttpPostJson<TRequest, TResponse>(string route, TRequest body)
        {
            var json = HttpPostJson(route, body);
            return TryJsonDeserialize<TResponse>(json);
        }

        public string HttpPostJson<TRequest>(string route, TRequest body)
        {
            return Retry(() =>
            {
                using var client = GetClient();
                var url = GetUrl() + route;
                using var content = JsonContent.Create(body);
                Log(url, JsonConvert.SerializeObject(body));
                var result = Time.Wait(client.PostAsync(url, content));
                var str= Time.Wait(result.Content.ReadAsStringAsync());
                Log(url, str);
                return str;
            });
        }

        public string HttpPostStream(string route, Stream stream)
        {
            return Retry(() =>
            {
                using var client = GetClient();
                var url = GetUrl() + route;
                Log(url, "~ STREAM ~");
                var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var response = Time.Wait(client.PostAsync(url, content));
                var str =Time.Wait(response.Content.ReadAsStringAsync());
                Log(url, str);
                return str;
            });
        }

        public Stream HttpGetStream(string route)
        {
            return Retry(() =>
            {
                var client = GetClient();
                var url = GetUrl() + route;
                Log(url, "~ STREAM ~");
                return Time.Wait(client.GetStreamAsync(url));
            });
        }

        private string GetUrl()
        {
            return $"http://{ip}:{port}{baseUrl}";
        }

        private void Log(string url, string message)
        {
            log.Debug($"({url}) = '{message}'", 3);
        }

        private T Retry<T>(Func<T> operation)
        {
            var retryCounter = 0;

            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception exception)
                {
                    timeSet.HttpCallRetryDelay();
                    retryCounter++;
                    if (retryCounter > timeSet.HttpCallRetryCount())
                    {
                        Assert.Fail(exception.ToString());
                        throw;
                    }
                }
            }
        }

        private static T TryJsonDeserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json)!;
            }
            catch (Exception exception)
            {
                var msg = $"Failed to deserialize JSON: '{json}' with exception: {exception}";
                Assert.Fail(msg);
                throw new InvalidOperationException(msg, exception);
            }
        }

        private HttpClient GetClient()
        {
            var client = new HttpClient();
            client.Timeout = timeSet.HttpCallTimeout();
            return client;
        }
    }
}
