﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics
{
    public class DebuggeeInfo : IDisposable
    {
        public readonly ITestOutputHelper Output;
        public readonly TestConfiguration TestConfiguration;
        public readonly bool Launch;
        public readonly string PipeName;

        public int ProcessId { get; private set; }
        public IntPtr ResumeHandle { get; set; }

        private readonly AutoResetEvent _createProcessEvent = new(false);
        private readonly NamedPipeServerStream _pipeServer;
        private HResult _createProcessResult = HResult.E_FAIL;
        private Process _process;

        public DebuggeeInfo(ITestOutputHelper output, TestConfiguration config, bool launch)
        {
            Output = output;
            TestConfiguration = config;
            Launch = launch;
            PipeName = Guid.NewGuid().ToString();
            _pipeServer = new NamedPipeServerStream(PipeName);
        }

        public void SetProcessId(int processId)
        {
            ProcessId = processId;
            try
            {
                _process = Process.GetProcessById(processId);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Trace.TraceError($"DebuggeeInfo.SetProcessId({processId}): {ex}");
            }
        }

        public void SetCreateProcessResult(HResult hr)
        {
            _createProcessResult = hr;
            _createProcessEvent.Set();
        }

        public HResult WaitForCreateProcess()
        {
            Assert.True(_createProcessEvent.WaitOne());
            return _createProcessResult;
        }

        public async Task<bool> WaitForDebuggee()
        {
            if (_process is null)
            {
                Trace.TraceWarning("DebuggeeInfo.WaitForDebuggee: no process");
                return true;
            }
            try
            {
                using CancellationTokenSource source = new(TimeSpan.FromMinutes(5));
                Trace.TraceInformation($"DebuggeeInfo.WaitForDebuggee: waiting {ProcessId}");

                Task processDeath = _process.WaitForExitAsync(source.Token);
                Task debuggeeReady = _pipeServer.WaitForConnectionAsync(source.Token);
                Task doneTask = await Task.WhenAny(processDeath, debuggeeReady).WaitAsync(source.Token);

                source.Cancel();

                if (doneTask == processDeath)
                {
                    Trace.TraceWarning($"DebuggeeInfo.WaitForDebuggee: process {ProcessId} exited");
                    return false;
                }

                Trace.TraceInformation($"DebuggeeInfo.WaitForDebuggee: after wait {ProcessId}");
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                Trace.TraceError($"DebuggeeInfo.WaitForDebuggee: canceled {ex}");
                return false;
            }
            return true;
        }

        public HResult ResumeDebuggee()
        {
            HResult result = HResult.S_OK;
            if (ResumeHandle != IntPtr.Zero)
            {
                Trace.TraceInformation($"DebuggeeInfo.ResumeDebuggee {ProcessId} handle {ResumeHandle:X8}");
                result = DbgShimAPI.ResumeProcess(ResumeHandle);
                DbgShimAPI.CloseResumeHandle(ResumeHandle);
                ResumeHandle = IntPtr.Zero;
            }
            return result;
        }

        public void Disconnect()
        {
            try
            {
                _pipeServer.Disconnect();
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public void Kill()
        {
            if (_process is not null)
            {
                Trace.TraceInformation($"DebuggeeInfo: kill process {ProcessId}");
                try
                {
                    _process.Kill();
                    _process = null;
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
                {
                    Trace.TraceError(ex.ToString());
                }
            }
        }

        public void Dispose()
        {
            Trace.TraceInformation($"DebuggeeInfo: disposing process {ProcessId}");
            ResumeDebuggee();
            _pipeServer.Dispose();
            Kill();
        }
    }
}
