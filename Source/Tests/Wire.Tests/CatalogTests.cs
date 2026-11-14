using System.Buffers;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using MemoryPack;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// The catalog gives each entry type an id and packs entries into frames: [varint id][MemoryPack payload].
public sealed class CatalogTests
{
    [Test]
    public void Catalog_BuiltIns_FixedIds()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });

        Assert.That(catalog.IdOf(typeof(CreateEntity)), Is.EqualTo(0));
        Assert.That(catalog.IdOf(typeof(DeleteEntity)), Is.EqualTo(1));
        Assert.That(catalog.IdOf(typeof(GroupStart)), Is.EqualTo(2));
        Assert.That(catalog.IdOf(typeof(GroupEnd)), Is.EqualTo(3));
        Assert.That(catalog.IdOf(typeof(WireHello)), Is.EqualTo(4));
        Assert.That(catalog.IdOf(typeof(WireReply)), Is.EqualTo(5));
    }

    [Test]
    public void Catalog_KeyTypes_SortedByKeyWithNoAssembly()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });
        Type[] keys =
        {
            typeof(ChangeComponent<Health>), typeof(RemoveComponent<Health>),
            typeof(ChangeComponent<Armor>), typeof(RemoveComponent<Armor>),
            typeof(ChangeComponent<CardName>), typeof(RemoveComponent<CardName>),
            typeof(AddTag<Marked>), typeof(RemoveTag<Marked>),
        };

        Type[] byId = keys.OrderBy(catalog.IdOf).ToArray();

        Assert.That(catalog.Count, Is.EqualTo(14));
        Assert.That(byId, Is.EqualTo(keys.OrderBy(WireCatalog.KeyOf, StringComparer.Ordinal).ToArray()));
        Assert.That(keys.Select(catalog.IdOf), Is.All.GreaterThanOrEqualTo(6));
    }

    [Test]
    public void Catalog_ModuleOrderOrDuplicates_SameIds()
    {
        WireTypes health = WireTypes.Of(t => t.Component<Health>());
        WireTypes rest = WireTypes.Of(t => t.Component<Armor>().Tag<Marked>().Component<Health>());

        WireCatalog first = new WireCatalog(new[] { health, rest });
        WireCatalog second = new WireCatalog(new[] { rest, health });

        Assert.That(second.Count, Is.EqualTo(first.Count));
        Assert.That(second.IdOf(typeof(ChangeComponent<Health>)), Is.EqualTo(first.IdOf(typeof(ChangeComponent<Health>))));
        Assert.That(second.IdOf(typeof(AddTag<Marked>)), Is.EqualTo(first.IdOf(typeof(AddTag<Marked>))));
    }

    [Test]
    public void KeyOf_GenericType_HasNoAssemblyName()
    {
        string key = WireCatalog.KeyOf(typeof(ChangeComponent<Health>));

        Assert.That(key, Is.EqualTo("Ecs.Contracts.Commands.ChangeComponent`1[Wire.Tests.Fixtures.Health]"));
    }

    [Test]
    public void Hash_SameKeysInOtherOrder_Equal()
    {
        WireTypes health = WireTypes.Of(t => t.Component<Health>());
        WireTypes rest = WireTypes.Of(t => t.Component<Armor>().Tag<Marked>());

        WireCatalog first = new WireCatalog(new[] { health, rest });
        WireCatalog second = new WireCatalog(new[] { rest, health });

        Assert.That(second.Hash, Is.EqualTo(first.Hash));
    }

    [Test]
    public void Hash_OtherKeys_Differ()
    {
        WireCatalog all = new WireCatalog(new[] { Match.Keys() });
        WireCatalog less = new WireCatalog(new[] { WireTypes.Of(t => t.Component<Health>().Component<Armor>()) });
        WireCatalog none = new WireCatalog(Array.Empty<WireTypes>());

        Assert.That(less.Hash, Is.Not.EqualTo(all.Hash));
        Assert.That(none.Hash, Is.Not.EqualTo(all.Hash));
        Assert.That(none.Hash, Is.Not.EqualTo(less.Hash));
    }

    [Test]
    public void Catalog_Over256Types_Builds()
    {
        WireCatalog catalog = new WireCatalog(new[] { ManyKeys.All() });

        Assert.That(catalog.Count, Is.EqualTo(262));
    }

    [Test]
    public void Frame_IdAbove127_TwoByteHeader_RoundTrip()
    {
        WireCatalog catalog = new WireCatalog(new[] { ManyKeys.All() });
        Type type = typeof(ChangeComponent<C63>);
        int id = catalog.IdOf(type);
        ChangeComponent<C63> entry = new ChangeComponent<C63>(new EntityId(3), new C63 { Value = 9 });
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

        catalog.Write(entry, frame);

        Assert.That(id, Is.GreaterThan(127));
        Assert.That(WireVarint.Read(frame.WrittenSpan, out int length), Is.EqualTo(id));
        Assert.That(length, Is.EqualTo(2));
        Assert.That(catalog.Read(frame.WrittenSpan), Is.EqualTo(entry));
    }

    [Test]
    public void Catalog_ClientMessage_HasAnId_AndChangesTheHash()
    {
        WireCatalog keys = new WireCatalog(new[] { Match.Keys() });
        WireCatalog messages = new WireCatalog(new[] { Match.Keys(), WireTypes.Of(t => t.ClientMessage<Shout, int>()) });

        Assert.That(messages.IdOf(typeof(Shout)), Is.GreaterThanOrEqualTo(6));
        Assert.That(messages.Count, Is.EqualTo(keys.Count + 1));
        Assert.That(messages.Hash, Is.Not.EqualTo(keys.Hash));
    }

    [Test]
    public void Frame_ClientMessage_RoundTrip()
    {
        WireCatalog catalog = new WireCatalog(new[] { WireTypes.Of(t => t.ClientMessage<Shout, int>()) });
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

        catalog.Write(new Shout("hi"), frame);

        Assert.That(catalog.Read(frame.WrittenSpan), Is.EqualTo(new Shout("hi")));
    }

    [Test]
    public void Frame_VarintWithNoLastByte_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });

        Assert.Throws<InvalidOperationException>(new Action(() => catalog.Read(new byte[] { 0x80 })));
    }

    [Test]
    public void Catalog_UnknownType_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { WireTypes.Of(t => t.Component<Health>()) });

        Assert.Throws<InvalidOperationException>(new Action(() => catalog.IdOf(typeof(ChangeComponent<Armor>))));
    }

    [TestCaseSource(nameof(Entries))]
    public void Frame_RoundTrip_EachEntryType(IMessage entry)
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

        catalog.Write(entry, frame);
        IMessage read = catalog.Read(frame.WrittenSpan);

        Assert.That(frame.WrittenSpan[0], Is.EqualTo(catalog.IdOf(entry.GetType())));
        Assert.That(read, Is.EqualTo(entry));
    }

    [Test]
    public void Frame_WireReply_RoundTrip()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

        catalog.Write(new WireReply("Cards.NotACard", "No card.", new byte[] { 1, 2 }), frame);
        WireReply read = (WireReply)catalog.Read(frame.WrittenSpan);

        Assert.That(read.ErrorCode, Is.EqualTo("Cards.NotACard"));
        Assert.That(read.ErrorMessage, Is.EqualTo("No card."));
        Assert.That(read.Value, Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test]
    public void Frame_Empty_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });

        Assert.Throws<InvalidOperationException>(new Action(() => catalog.Read(ReadOnlySpan<byte>.Empty)));
    }

    [Test]
    public void Frame_UnknownId_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });

        Assert.Throws<InvalidOperationException>(new Action(() => catalog.Read(new byte[] { 200, 1, 2, 3 })));
    }

    [Test]
    public void Frame_BadPayload_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });
        byte[] frame = { (byte)catalog.IdOf(typeof(ChangeComponent<Health>)), 1 };

        Assert.Throws<MemoryPackSerializationException>(new Action(() => catalog.Read(frame)));
    }

    [Test]
    public void Write_UnknownType_Throws()
    {
        WireCatalog catalog = new WireCatalog(new[] { WireTypes.Of(t => t.Component<Health>()) });

        Assert.Throws<InvalidOperationException>(new Action(() => catalog.Write(new AddTag<Marked>(new EntityId(1)), new ArrayBufferWriter<byte>())));
    }

    private static IEnumerable<IMessage> Entries()
    {
        EntityId id = new EntityId(3);
        yield return new CreateEntity(id);
        yield return new DeleteEntity(id);
        yield return new ChangeComponent<Health>(id, new Health { Value = 27 });
        yield return new RemoveComponent<Health>(id);
        yield return new AddTag<Marked>(id);
        yield return new RemoveTag<Marked>(id);
        yield return new GroupStart(id, new BlockId(4));
        yield return new GroupEnd(new BlockId(4), GroupOutcome.Completed);
        yield return new WireHello(0x0123456789ABCDEFUL);
    }
}
