using Ecs.Domain;

namespace Ecs.Infrastructure;

public interface IWorldFactory
{
    World Create();
}
