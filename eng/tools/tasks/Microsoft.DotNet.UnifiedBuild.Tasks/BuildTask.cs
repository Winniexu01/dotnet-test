// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public abstract partial class BuildTask : ITask
    {
        private TaskLoggingHelper _log = null;

        internal TaskLoggingHelper Log
        {
            get { return _log ?? (_log = new TaskLoggingHelper(this)); }
        }

        public BuildTask()
        {
        }

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public ITaskHost HostObject
        {
            get;
            set;
        }

        public abstract bool Execute();
    }
}