namespace StellarIndustries.Core.Util
{
    /// <summary>Deterministic xorshift32 RNG whose state is stored in the save file.</summary>
    public static class Rng
    {
        public static uint Next(ref uint state)
        {
            if (state == 0) state = 2463534242u;
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public static double NextDouble(ref uint state) => Next(ref state) / 4294967296.0;
    }
}
