// 웹 PoC의 mulberry32와 비트 단위로 같은 난수. 같은 seed면 웹과 같은 계단, 같은 공격 순서가 나온다.
namespace Stairs.Core
{
    public sealed class Mulberry32
    {
        uint a;
        public Mulberry32(uint seed) { a = seed; }
        public Mulberry32(int seed) { a = unchecked((uint)seed); }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
