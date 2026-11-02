using System;

namespace Framework.Pipeline
{
    public abstract class PipelineScope
    {
        public bool IsClosed { get; private set; }

        internal void Close() => IsClosed = true;

        protected void ThrowIfClosed()
        {
            if (IsClosed)
                throw new InvalidOperationException($"{GetType().Name} is closed. A scope verb works only during its run.");
        }
    }
}
