#nullable enable
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// The slice's sample effect. Wired imperatively from the UI button
    /// — there are no <c>[On(...)]</c> attributes in this slice.
    /// </summary>
    public static class Fireball
    {
        public const int Damage = 6;

        public static void Cast(World world, Entity? caster, Entity target)
        {
            world.Execute(new DealDamage(caster, target, Damage));
        }
    }
}
