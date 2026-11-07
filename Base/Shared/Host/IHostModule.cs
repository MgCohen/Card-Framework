using Host.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Host;

public interface IHostModule
{
    void Configure(IServiceCollection services, PeerRole role);
}
