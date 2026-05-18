#nullable enable
using NUnit.Framework;

namespace Spike.CardPresentation.Tests
{
    public class CardViewModelTests
    {
        [Test]
        public void Setter_FiresChanged_OnlyWhenValueDiffers()
        {
            var vm = new CardViewModel();
            int fires = 0;
            vm.Changed += () => fires++;

            vm.Name = "First";
            vm.Name = "First";          // same value — must not fire
            vm.Name = "Second";

            Assert.That(fires, Is.EqualTo(2));
        }

        [Test]
        public void Setter_NoFire_ForUnchangedDefault()
        {
            var vm = new CardViewModel();
            int fires = 0;
            vm.Changed += () => fires++;
            vm.Cost = 0;                 // default int
            vm.Revealed = false;         // default bool
            Assert.That(fires, Is.EqualTo(0));
        }

        [Test]
        public void Refresh_MirrorsEntityFields()
        {
            var vm = new CardViewModel { CardId = 7 };
            var e = new CardEntity
            {
                Id = 7,
                Name = "Yeti",
                Kind = CardKind.Minion,
                Cost = 4,
                Attack = 4,
                Health = 5,
                BodyText = "stout",
                Revealed = true,
                Frozen = true,
                Pinned = true,
            };

            vm.Refresh(e);

            Assert.That(vm.Name,     Is.EqualTo("Yeti"));
            Assert.That(vm.Kind,     Is.EqualTo(CardKind.Minion));
            Assert.That(vm.Cost,     Is.EqualTo(4));
            Assert.That(vm.Attack,   Is.EqualTo(4));
            Assert.That(vm.Health,   Is.EqualTo(5));
            Assert.That(vm.BodyText, Is.EqualTo("stout"));
            Assert.That(vm.Revealed, Is.True);
            Assert.That(vm.Frozen,   Is.True);
            Assert.That(vm.Pinned,   Is.True);
        }

        [Test]
        public void Refresh_IsIdempotent_NoExtraFiresOnSecondCall()
        {
            var vm = new CardViewModel();
            var e = new CardEntity { Name = "x", Cost = 3, Kind = CardKind.Spell };

            vm.Refresh(e);
            int fires = 0;
            vm.Changed += () => fires++;

            vm.Refresh(e);              // mirror values match — nothing changes

            Assert.That(fires, Is.EqualTo(0));
        }

        [Test]
        public void RequestMove_WithoutSystem_ReturnsFalse()
        {
            var vm = new CardViewModel { CardId = 1 };
            Assert.That(vm.RequestMove(targetZoneId: 99), Is.False);
        }

        [Test]
        public void CanMoveTo_WithoutSystem_ReturnsFalse()
        {
            var vm = new CardViewModel { CardId = 1 };
            Assert.That(vm.CanMoveTo(targetZoneId: 99), Is.False);
        }

        [Test]
        public void HasAnyMove_WithoutSystem_ReturnsFalse()
        {
            var vm = new CardViewModel { CardId = 1 };
            Assert.That(vm.HasAnyMove(), Is.False);
        }

        [Test]
        public void RequestMove_DelegatesToSystem_AndReflectsResult()
        {
            // Drive the VM through a real World+System to exercise the
            // intent path the sandbox uses.
            var w   = new World();
            var sys = new GameSystem(w);
            w.System = sys;
            var card = w.AddCard(new CardEntity { ZoneId = GameSystem.ZoneIdHand });
            sys.Recompute();
            var vm = w.GetVM(card.Id);

            Assert.That(vm.RequestMove(GameSystem.ZoneIdPlayfield), Is.True);
            Assert.That(card.ZoneId, Is.EqualTo(GameSystem.ZoneIdPlayfield));
        }
    }
}
