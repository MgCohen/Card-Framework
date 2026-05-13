#nullable enable
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample card entity. Typed properties are hand-written for the
    /// slice — source-gen lands later.
    /// </summary>
    public class Card : Entity
    {
        public int Hp
        {
            get => Get(Tags.Hp);
            set => Set(Tags.Hp, value);
        }

        public int MaxHp
        {
            get => Get(Tags.MaxHp);
            set => Set(Tags.MaxHp, value);
        }

        public int Attack
        {
            get => Get(Tags.Attack);
            set => Set(Tags.Attack, value);
        }

        public bool DivineShield
        {
            get => Get(Tags.DivineShield);
            set => Set(Tags.DivineShield, value);
        }
    }
}
