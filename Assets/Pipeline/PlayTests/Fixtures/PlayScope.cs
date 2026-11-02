using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.PlayTests
{
    public sealed class PlayScope : PipelineScope
    {
        readonly Func<Message, Task<Envelope<Message>>> _run;

        public PlayScope(Func<Message, Task<Envelope<Message>>> run)
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
