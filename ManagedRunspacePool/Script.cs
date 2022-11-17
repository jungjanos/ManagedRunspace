using System;

namespace ManagedRunspacePool2
{
    [Serializable]
    public class Script
    {
        public string ScriptText { get; }
        public bool UseLocalScope { get; }

        public Script(string scriptText, bool useLocalScope = false)
        {
            ScriptText = !string.IsNullOrEmpty(scriptText) ? scriptText : throw new ArgumentNullException(nameof(scriptText));
            UseLocalScope = useLocalScope;
        }

        public static implicit operator Script(string script)
            => new Script(script);

        public static implicit operator Script((string script, bool useLocalScope) tuple)
            => new Script(tuple.script, tuple.useLocalScope);
    }
}
