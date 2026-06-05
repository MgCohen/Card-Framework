using System;
using NUnit.Framework;

namespace Acceptance
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class PictureAttribute : PropertyAttribute
    {
        public PictureAttribute(string fileName) : base(fileName) { }
    }
}
