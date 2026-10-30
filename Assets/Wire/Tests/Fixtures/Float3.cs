namespace Framework.Wire.Tests
{
    // Test-local composite value type — packed inline by AutoPacker via its public fields.
    // Stands in for a domain vector so the composite-payload round-trip test stays in Wire's
    // own test surface (the runtime Vec3 primitive was removed as unused).
    public readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
