using System;
using Vintagestory.API.MathTools;

namespace CaveTweaks
{
    public class MTRandom : IRandom
    {
        private const int N = 624;
        private const int M = 397;
        private const ulong MATRIX_A = 0x9908b0dfUL;
        private const ulong UPPER_MASK = 0x80000000UL;   // most significant w-r bits
        private const ulong LOWER_MASK = 0x7fffffffUL;   // least significant r bits

        private ulong[] mt = new ulong[N]; // the array for the state vector
        private int mti = N + 1;           // mti==N+1 means mt[N] is not initialized
        private ulong worldSeed;           // Store the world seed for reproducibility

        //
        // Summary:
        //     Initialize with a seed.
        public MTRandom(ulong seed)
        {
            SetWorldSeed(seed);
        }

        //
        // Summary:
        //     Initialize with no seed, requires explicit initialization.
        public MTRandom()
        {
        }

        //
        // Summary:
        //     Sets the world seed.
        public void SetWorldSeed(ulong seed)
        {
            worldSeed = seed;
            InitGenRand(seed);
        }

        //
        // Summary:
        //     Initializes the generator with a seed.
        //
        // Parameters:
        //   seed:
        public void InitGenRand(ulong seed)
        {
            mt[0] = seed & 0xffffffffUL;
            for(mti = 1; mti < N; mti++)
            {
                mt[mti] = (1812433253UL * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (ulong)mti);
                mt[mti] &= 0xffffffffUL; // for UWORD 32 bit machines
            }
        }

        //
        // Summary:
        //     Initializes a position-dependent seed based on (xPos, zPos) and the world seed.
        public void InitPositionSeed(int xPos, int zPos)
        {
            ulong posSeed = (ulong)(xPos * 6364136223846793005L + zPos * 1442695040888963407L);
            InitGenRand(worldSeed ^ posSeed);  // Combine worldSeed with position seed
        }

        //
        // Summary:
        //     Initializes a position-dependent seed based on (xPos, yPos, zPos) and the world seed.
        public void InitPositionSeed(int xPos, int yPos, int zPos)
        {
            ulong posSeed = (ulong)(xPos * 6364136223846793005L + yPos * 1442695040888963407L + zPos * 6364136223846793005L);
            InitGenRand(worldSeed ^ posSeed);  // Combine worldSeed with position seed
        }

        //
        // Summary:
        //     Generate a random number between 0 and max (exclusive).
        //
        // Parameters:
        //   max:
        public int NextInt(int max)
        {
            return (int)(GenRandInt32() % (ulong)max);
        }

        //
        // Summary:
        //     Generate a random number between 0 and int.MaxValue (inclusive).
        public int NextInt()
        {
            return (int)(GenRandInt32() & 0x7fffffffUL);
        }

        //
        // Summary:
        //     Generate a random float between 0.0F and 1.0F (inclusive).
        public float NextFloat()
        {
            return (float)GenRandReal1();
        }

        //
        // Summary:
        //     Generate a random float between -1.0F and 1.0F (inclusive).
        public float NextFloatMinusToPlusOne()
        {
            return 2.0f * NextFloat() - 1.0f;
        }

        //
        // Summary:
        //     Generate a random double between 0.0 and 1.0 (inclusive).
        public double NextDouble()
        {
            return GenRandReal1();
        }

        //
        // Summary:
        //     Generates a random 32-bit integer.
        private ulong GenRandInt32()
        {
            ulong y;
            ulong[] mag01 = { 0x0UL, MATRIX_A };

            if(mti >= N)
            {
                int kk;

                if(mti == N + 1) // if init_genrand() has not been called
                    InitGenRand(5489UL); // a default initial seed is used

                for(kk = 0; kk < N - M; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1UL];
                }
                for(; kk < N - 1; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1UL];
                }
                y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1UL];

                mti = 0;
            }

            y = mt[mti++];

            // Tempering
            y ^= (y >> 11);
            y ^= (y << 7) & 0x9d2c5680UL;
            y ^= (y << 15) & 0xefc60000UL;
            y ^= (y >> 18);

            return y;
        }

        //
        // Summary:
        //     Generates a random number between 0.0 and 1.0 (inclusive).
        private double GenRandReal1()
        {
            return GenRandInt32() * (1.0 / 4294967295.0); // 2^32-1
        }
    }

}
