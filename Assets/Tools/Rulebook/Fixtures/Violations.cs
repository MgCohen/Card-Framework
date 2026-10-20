using System;
using System.Runtime.CompilerServices;

// Each check kind is proven against this assembly: it holds one deliberate violation per kind
// next to code that must pass. CheckKindTests lists the exact violations expected.
[assembly: InternalsVisibleTo("Tools.Rulebook.Tests")]
[assembly: InternalsVisibleTo("Not.Allowed")]

namespace Tools.Rulebook.Fixtures
{
    // --- il.callersOnly / il.forbiddenCalls ---

    public abstract class FakeHandler<T>
    {
        public abstract void Apply(T ev);
    }

    public sealed class FakeStore
    {
        int _value;
        internal void Write(int value) => _value = value;
        internal void Reset() => Write(0);
        public int Read() => _value;
    }

    public sealed class FakeService
    {
        public void Do() { }
    }

    public sealed class GoodHandler : FakeHandler<int>
    {
        readonly FakeStore _store;
        public GoodHandler(FakeStore store) => _store = store;
        public override void Apply(int ev) => _store.Write(ev);
    }

    public sealed class LambdaHandler : FakeHandler<int>
    {
        readonly FakeStore _store;
        public LambdaHandler(FakeStore store) => _store = store;
        public override void Apply(int ev)
        {
            Action write = () => _store.Write(ev);
            write();
        }
    }

    public sealed class BadService
    {
        readonly FakeStore _store;
        public BadService(FakeStore store) => _store = store;
        public void Cheat() => _store.Write(1);
    }

    public sealed class ChattyHandler : FakeHandler<int>
    {
        readonly FakeService _service;
        public ChattyHandler(FakeService service) => _service = service;
        public override void Apply(int ev) => _service.Do();
    }

    // --- type.memberTypes ---

    public interface IReadOnlyFake { }

    public sealed class FakeEngine
    {
        public IReadOnlyFake View    { get; set; }
        public FakeService   Service { get; set; }
        public FakeStore     Store   { get; set; }
        public FakeStore     StoreField;
        public IReadOnlyFake ViewOf() => View;
        public FakeStore     StoreOf() => Store;
        public void          Touch() { }
    }

    // --- type.noSubtypesIn ---

    public abstract class FakeEvent { }
    public sealed class BlockFakeEvent : FakeEvent { }
    public sealed class DomainFakeEvent : FakeEvent { }

    // --- tests.requireAttribute ---

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FakeTestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class FakePictureAttribute : Attribute
    {
        public FakePictureAttribute(string fileName) { }
    }

    public sealed class FakeBindings
    {
        [FakeTest, FakePicture("present.md")] public void Bound() { }
        [FakeTest] public void Unbound() { }
        [FakeTest, FakePicture("missing.md")] public void Broken() { }
    }
}
