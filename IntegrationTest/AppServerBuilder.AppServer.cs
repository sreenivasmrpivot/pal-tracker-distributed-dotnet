﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using TestSupport;

namespace IntegrationTest
{
    public partial class AppServerBuilder
    {
        public class AppServer
        {
            private readonly string _database;
            private readonly string _baseUrl;
            private Process _process;
            private readonly ProcessStartInfo _startInfo;
            private readonly string _dllPath;

            internal AppServer(int port, string appName, IDictionary<string, string> environmentVariables,
                string database)
            {
                _baseUrl = $"http://localhost:{port}";
                _database = database;

                var appPath = $"{AppContext.BaseDirectory}../../../../Applications/{appName}";
                _dllPath = $"{appPath}/bin/Debug/netcoreapp2.0/{appName}.dll";

                _startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"exec {_dllPath} --urls {_baseUrl}",
                    UseShellExecute = false,
                    WorkingDirectory = appPath
                };

                foreach (var kv in environmentVariables)
                {
                    _startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }

                _startInfo.EnvironmentVariables["MYSQL__CLIENT__CONNECTIONSTRING"] =
                    TestDatabaseSupport.ConnectionString(_database);
            }

            public string Url(string relativePath = "/") => $"{_baseUrl}{relativePath}";

            public void Start()
            {
                if (!File.Exists(_dllPath))
                {
                    throw new InvalidOperationException($"File {_dllPath} does not exist. Run 'dotnet build' to rebuild the application.");
                }

                Console.WriteLine("Starting Process... " + _startInfo.Arguments);

                _process = Process.Start(_startInfo);

                Console.WriteLine("Started Process... " + _startInfo.Arguments + " successfully");

                Console.WriteLine("Started waiting");
                WaitUntilReady();
                Console.WriteLine("Done waiting");

                new TestDatabaseSupport(TestDatabaseSupport.ConnectionString(_database)).TruncateAllTables();
            }

            public void Stop()
            {
                _process.Kill();
            }

            private void WaitUntilReady()
            {
                const int retryThreshold = 10;
                const int delay = 5000;
                var httpClient = new HttpClient();

                var tries = 0;
                Exception failureReason = null;

                while (tries < retryThreshold)
                {
                    try
                    {
                        var _ = httpClient.GetAsync(_baseUrl).Result;
                        return;
                    }
                    catch (Exception ex) when(ex.GetBaseException().GetType() == typeof(HttpRequestException))
                    {
                        failureReason = ex;
                        Thread.Sleep(delay);
                    }

                    tries++;
                }

                throw failureReason.GetBaseException();
            }
        }
    }
}