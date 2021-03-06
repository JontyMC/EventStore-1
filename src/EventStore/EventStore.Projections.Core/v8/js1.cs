﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Runtime.InteropServices;

namespace EventStore.Projections.Core.v8
{
    public static class Js1
    {
        private const string DllName = "js1";

        public delegate void CommandHandlerRegisteredDelegate(
            [MarshalAs(UnmanagedType.LPWStr)] string eventName, IntPtr handlerHandle);

        public delegate void ReverseCommandHandlerDelegate(
            [MarshalAs(UnmanagedType.LPWStr)] string commandName, [MarshalAs(UnmanagedType.LPWStr)] string commandBody);

        public delegate IntPtr LoadModuleDelegate([MarshalAs(UnmanagedType.LPWStr)] string moduleName);

        public delegate void LogDelegate([MarshalAs(UnmanagedType.LPWStr)] string message);

        public delegate void ReportErrorDelegate(int erroe_code, [MarshalAs(UnmanagedType.LPWStr)] string error_message);


        [DllImport("js1", EntryPoint = "js1_api_version")]
        public static extern IntPtr ApiVersion();

        [DllImport("js1", EntryPoint = "compile_module")]
        public static extern IntPtr CompileModule(
            IntPtr prelude, [MarshalAs(UnmanagedType.LPWStr)] string script,
            [MarshalAs(UnmanagedType.LPWStr)] string fileName);

        [DllImport("js1", EntryPoint = "compile_prelude")]
        public static extern IntPtr CompilePrelude(
            [MarshalAs(UnmanagedType.LPWStr)] string prelude, [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            LoadModuleDelegate loadModuleHandler, LogDelegate logHandler);

        [DllImport("js1", EntryPoint = "compile_query")]
        public static extern IntPtr CompileQuery(
            IntPtr prelude, [MarshalAs(UnmanagedType.LPWStr)] string script,
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            CommandHandlerRegisteredDelegate commandHandlerRegisteredCallback,
            ReverseCommandHandlerDelegate reverseCommandHandler);

        [DllImport("js1", EntryPoint = "dispose_script")]
        public static extern void DisposeScript(IntPtr scriptHandle);

        //TODO: add no result execute_handler
        [DllImport("js1", EntryPoint = "execute_command_handler")]
        public static extern IntPtr ExecuteCommandHandler(
            IntPtr scriptHandle, IntPtr eventHandlerHandle, [MarshalAs(UnmanagedType.LPWStr)] string dataJson,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] dataOther, int otherLength,
            out IntPtr resultJson);

        [DllImport("js1", EntryPoint = "free_result")]
        public static extern void FreeResult(IntPtr resultHandle);

        [DllImport("js1", EntryPoint = "report_errors")]
        public static extern void ReportErrors(IntPtr scriptHandle, ReportErrorDelegate reportErrorCallback);

    }
}