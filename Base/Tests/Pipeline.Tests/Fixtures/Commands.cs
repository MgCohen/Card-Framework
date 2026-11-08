using VSA.Contracts.Commands;

namespace Pipeline.Tests;

public sealed record Ping : TestMessage, ICommand<string>;

public sealed record Echo(int Value) : TestMessage, ICommand<int>;

public sealed record Boom : TestMessage, ICommand<int>;

public sealed record Denied : TestMessage, ICommand<int>;

public sealed record Crash : TestMessage, ICommand<int>;

public sealed record Hold : TestMessage, ICommand<int>;

public sealed record Write(string Value) : TestMessage, ICommand<bool>;

public sealed record Keep : TestMessage, ICommand<int>;

public sealed record Nest : TestMessage, ICommand<string>;

public sealed record NestTwice : TestMessage, ICommand<string>;

public sealed record Twice : TestMessage, ICommand<int>, ICommand<string>;

public sealed record Cross(int Value) : TestMessage, ICommand<int>;

public sealed record Loose : TestMessage, ICommand<int>;

public sealed record Misfit : TestMessage, ICommand<int>;

public sealed record Orphan : TestMessage, ICommand<int>;

public sealed record Both : TestMessage, IBothMessage, ICommand<int>;

public sealed record Count(int Value) : OtherMessage, ICommand<int>;

public sealed record Plain(int Value) : ICommand<int>;
