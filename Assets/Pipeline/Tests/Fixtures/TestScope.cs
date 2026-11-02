using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class TestScope : PipelineScope
    {
        readonly Func<Message, Task<Envelope<Message>>> _run;

        public TestScope(Func<Message, Task<Envelope<Message>>> run = null)
        {
            _run = run;
        }

        public Task<Envelope<Message>> Run(Message message)
        {
            ThrowIfClosed();
            return _run(message);
        }
    }
}
