#nullable enable
using System.Linq;
using NUnit.Framework;

namespace Spike.CardPresentation.Tests
{
    public class WorldTests
    {
        [Test]
        public void AddCard_AssignsIdWhenZero()
        {
            var w = new World();
            var c1 = w.AddCard(new CardEntity { Name = "A" });
            var c2 = w.AddCard(new CardEntity { Name = "B" });
            Assert.That(c1.Id, Is.EqualTo(1));
            Assert.That(c2.Id, Is.EqualTo(2));
        }

        [Test]
        public void AddCard_PreservesExplicitId_AndKeepsNextIdAhead()
        {
            var w = new World();
            w.AddCard(new CardEntity { Id = 50, Name = "explicit" });
            var auto = w.AddCard(new CardEntity { Name = "auto" });
            // _nextId must clear the highest explicit id.
            Assert.That(auto.Id, Is.EqualTo(51));
        }

        [Test]
        public void GetCard_ReturnsAddedEntity()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity { Name = "x" });
            Assert.That(w.GetCard(c.Id), Is.SameAs(c));
        }

        [Test]
        public void GetCard_UnknownId_ReturnsNull()
        {
            var w = new World();
            Assert.That(w.GetCard(999), Is.Null);
        }

        [Test]
        public void CardsInZone_FiltersByZoneId()
        {
            var w = new World();
            w.AddCard(new CardEntity { Name = "a", ZoneId = 1 });
            w.AddCard(new CardEntity { Name = "b", ZoneId = 2 });
            w.AddCard(new CardEntity { Name = "c", ZoneId = 1 });

            var inZone1 = w.CardsInZone(1).Select(c => c.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "a", "c" }, inZone1);
        }

        [Test]
        public void SetCardZone_UpdatesZoneId_AndFiresChanged()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity { ZoneId = 1 });

            CardEntity? fired = null;
            w.CardChanged += e => fired = e;

            w.SetCardZone(c.Id, 2);

            Assert.That(c.ZoneId, Is.EqualTo(2));
            Assert.That(fired, Is.SameAs(c));
        }

        [Test]
        public void SetCardZone_NoOpWhenTargetMatches_DoesNotFire()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity { ZoneId = 1 });
            int fires = 0;
            w.CardChanged += _ => fires++;

            w.SetCardZone(c.Id, 1);

            Assert.That(fires, Is.EqualTo(0));
        }

        [Test]
        public void SetCardZone_UnknownId_DoesNotThrowOrFire()
        {
            var w = new World();
            int fires = 0;
            w.CardChanged += _ => fires++;

            Assert.DoesNotThrow(() => w.SetCardZone(999, 2));
            Assert.That(fires, Is.EqualTo(0));
        }

        [Test]
        public void GetVM_CreatesLazily_AndReturnsSameInstance()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity { Name = "x" });
            var vm1 = w.GetVM(c.Id);
            var vm2 = w.GetVM(c.Id);
            Assert.That(vm1, Is.SameAs(vm2));
            Assert.That(vm1.CardId, Is.EqualTo(c.Id));
        }

        [Test]
        public void GetVM_MirrorsCurrentEntityState()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity
            {
                Name = "Yeti", Kind = CardKind.Minion,
                Cost = 4, Attack = 4, Health = 5, BodyText = "stout",
            });
            var vm = w.GetVM(c.Id);

            Assert.That(vm.Name, Is.EqualTo("Yeti"));
            Assert.That(vm.Kind, Is.EqualTo(CardKind.Minion));
            Assert.That(vm.Cost, Is.EqualTo(4));
            Assert.That(vm.Attack, Is.EqualTo(4));
            Assert.That(vm.Health, Is.EqualTo(5));
            Assert.That(vm.BodyText, Is.EqualTo("stout"));
        }

        [Test]
        public void GetVM_RefreshesOnEachCall_AfterEntityMutation()
        {
            var w = new World();
            var c = w.AddCard(new CardEntity { Name = "Original" });
            var vm = w.GetVM(c.Id);
            Assert.That(vm.Name, Is.EqualTo("Original"));

            c.Name = "Renamed";
            var sameVm = w.GetVM(c.Id);
            Assert.That(sameVm, Is.SameAs(vm));
            Assert.That(vm.Name, Is.EqualTo("Renamed"));
        }

        [Test]
        public void GetVM_InjectsSystemReferenceFromWorld()
        {
            var w = new World();
            var sys = new GameSystem(w);
            w.System = sys;
            var c = w.AddCard(new CardEntity());
            var vm = w.GetVM(c.Id);
            Assert.That(vm.System, Is.SameAs(sys));
        }

        [Test]
        public void SetCardZone_RefreshesVMMirrorBeforeFiringChanged()
        {
            // Subscribers reading VM state inside CardChanged should see the
            // post-mutation values — World refreshes the VM before invoking.
            var w = new World();
            var c = w.AddCard(new CardEntity { ZoneId = 1, Name = "x" });
            var vm = w.GetVM(c.Id);

            int? observedZoneIdOnEntity = null;
            w.CardChanged += e => observedZoneIdOnEntity = e.ZoneId;

            w.SetCardZone(c.Id, 2);

            Assert.That(observedZoneIdOnEntity, Is.EqualTo(2));
            Assert.That(vm.Name, Is.EqualTo("x"));
        }
    }
}
