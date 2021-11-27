using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Syndical.Library
{
    public sealed class Crc32 : HashAlgorithm
    {
        private uint hash;
        private readonly uint seed;
        private readonly uint[] table;
        private static uint[] defaultTable;

        public Crc32() : this(0xedb88320, uint.MaxValue)
        { }

        public Crc32(uint polynomial, uint seed)
        { 
            table = InitializeTable(polynomial);
            hash = seed;
            this.seed = seed;
        }

        private static uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
        {
            uint num = seed;
            for (int i = start; i < (size - start); i++)
            {
                num = (num >> 8) ^ table[(int) ((IntPtr) (buffer[i] ^ (num & 0xff)))];
            }
            return num;
        }

        public static uint Compute(byte[] buffer) => 
            Compute(uint.MaxValue, buffer);

        public static uint Compute(uint seed, byte[] buffer) => 
            Compute(0xedb88320, seed, buffer);

        public static uint Compute(uint polynomial, uint seed, byte[] buffer) => 
            ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            this.hash = CalculateHash(this.table, this.hash, buffer, start, length);
        }

        protected override byte[] HashFinal()
        {
            byte[] buffer = UInt32ToBigEndianBytes(~this.hash);
            base.HashValue = buffer;
            return buffer;
        }

        public override void Initialize()
        {
            this.hash = this.seed;
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            if ((polynomial == 0xedb88320) && (defaultTable != null))
            {
                return defaultTable;
            }
            uint[] numArray = new uint[0x100];
            for (int i = 0; i < 0x100; i++)
            {
                uint num2 = (uint) i;
                for (int j = 0; j < 8; j++)
                {
                    if ((num2 & 1) == 1)
                    {
                        num2 = (num2 >> 1) ^ polynomial;
                    }
                    else
                    {
                        num2 = num2 >> 1;
                    }
                }
                numArray[i] = num2;
            }
            if (polynomial == 0xedb88320)
            {
                defaultTable = numArray;
            }
            return numArray;
        }

        private static byte[] UInt32ToBigEndianBytes(uint uint32)
        {
            byte[] bytes = BitConverter.GetBytes(uint32);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        public override int HashSize =>
            0x20;
    }
}