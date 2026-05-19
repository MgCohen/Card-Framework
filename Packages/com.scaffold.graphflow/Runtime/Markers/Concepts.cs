using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public interface IGraphEntry { }

    public interface IGraphAction<TRunner> where TRunner : GraphRunner { }

    public interface IExecutable<TRunner> where TRunner : GraphRunner
    {
        Task Execute(TRunner runner);
    }
}
