﻿using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ManagedRunspacePool2
{
    /// <summary>
    /// Models the result of script execution
    /// </summary>
    [Serializable]    
    public class PsResult
    {
        public PsResult(IList<PSObject> results, IList<object> errors, Exception exception = null)
        {
            Results = results ?? new PSObject[0];
            Errors = errors ?? new object[0];
            Exception = exception;
        }

        public IList<PSObject> Results { get; }
        
        // TODO : Errors must be [Serializable] otherwise a runtime Exception is possible, currently its not checked
        public IList<object> Errors { get; }
        public Exception Exception { get; }
        public bool IsSuccess => Errors.Count == 0 && Exception == null;
    }
}
