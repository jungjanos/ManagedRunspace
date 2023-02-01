using System;

namespace ManagedRunspace2
{
    [Serializable]
    public class ManagedRunsapceException : Exception
    {
        public ErrorType Type { get; }

        public ManagedRunsapceException(ErrorType type, string message, Exception innerException) : base(message, innerException)
        {            
            Type = type;
        }

        public ManagedRunsapceException(ErrorType type, string message) : base(message)
        {
            Type = type;
        }

        public enum ErrorType
        {
            Unknown = default,
            RunspaceInitialization = 2,
            RunspaceProxyCreation = 4,
        }
    }    
}
