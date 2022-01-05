using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ambrosia
{
    public static partial class xxHash64
    {
        private const ulong p1 = 11400714785074694791UL;
        private const ulong p2 = 14029467366897019727UL;
        private const ulong p3 = 1609587929392839161UL;
        private const ulong p4 = 9650029242287828579UL;
        private const ulong p5 = 2870177450012600261UL;

        internal static class BitUtils
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static uint RotateLeft(uint value, int offset)
            {
#if FCL_BITOPS
            return System.Numerics.BitOperations.RotateLeft(value, offset);
#else
                return (value << offset) | (value >> (32 - offset));
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong RotateLeft(ulong value, int offset)
            {
#if FCL_BITOPS
            return System.Numerics.BitOperations.RotateLeft(value, offset);
#else
                return (value << offset) | (value >> (64 - offset));
#endif
            }
        }

        internal static class UnsafeBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
            {
                Debug.Assert(src != null);
                Debug.Assert(dst != null);
                Debug.Assert(srcOffset >= 0 && srcOffset < src.Length);
                Debug.Assert(dstOffset >= 0 && dstOffset < dst.Length);
                Debug.Assert(count >= 0);
                Debug.Assert(count + srcOffset <= src.Length);
                Debug.Assert(count + dstOffset <= dst.Length);

                fixed (byte* pSrc = &src[srcOffset])
                fixed (byte* pDst = &dst[dstOffset])
                {
                    byte* ptrSrc = pSrc;
                    byte* ptrDst = pDst;

                SMALLTABLE:
                    switch (count)
                    {
                        case 0:
                            return;
                        case 1:
                            *ptrDst = *ptrSrc;
                            return;
                        case 2:
                            *(short*)ptrDst = *(short*)ptrSrc;
                            return;
                        case 3:
                            *(short*)(ptrDst + 0) = *(short*)(ptrSrc + 0);
                            *(ptrDst + 2) = *(ptrSrc + 2);
                            return;
                        case 4:
                            *(int*)ptrDst = *(int*)ptrSrc;
                            return;
                        case 5:
                            *(int*)(ptrDst + 0) = *(int*)(ptrSrc + 0);
                            *(ptrDst + 4) = *(ptrSrc + 4);
                            return;
                        case 6:
                            *(int*)(ptrDst + 0) = *(int*)(ptrSrc + 0);
                            *(short*)(ptrDst + 4) = *(short*)(ptrSrc + 4);
                            return;
                        case 7:
                            *(int*)(ptrDst + 0) = *(int*)(ptrSrc + 0);
                            *(short*)(ptrDst + 4) = *(short*)(ptrSrc + 4);
                            *(ptrDst + 6) = *(ptrSrc + 6);
                            return;
                        case 8:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            return;
                        case 9:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(ptrDst + 8) = *(ptrSrc + 8);
                            return;
                        case 10:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(short*)(ptrDst + 8) = *(short*)(ptrSrc + 8);
                            return;
                        case 11:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(short*)(ptrDst + 8) = *(short*)(ptrSrc + 8);
                            *(ptrDst + 10) = *(ptrSrc + 10);
                            return;
                        case 12:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(int*)(ptrDst + 8) = *(int*)(ptrSrc + 8);
                            return;
                        case 13:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(int*)(ptrDst + 8) = *(int*)(ptrSrc + 8);
                            *(ptrDst + 12) = *(ptrSrc + 12);
                            return;
                        case 14:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(int*)(ptrDst + 8) = *(int*)(ptrSrc + 8);
                            *(short*)(ptrDst + 12) = *(short*)(ptrSrc + 12);
                            return;
                        case 15:
                            *(long*)(ptrDst + 0) = *(long*)(ptrSrc + 0);
                            *(int*)(ptrDst + 8) = *(int*)(ptrSrc + 8);
                            *(short*)(ptrDst + 12) = *(short*)(ptrSrc + 12);
                            *(ptrDst + 14) = *(ptrSrc + 14);
                            return;
                        case 16:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            return;
                        case 17:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(ptrDst + 16) = *(ptrSrc + 16);
                            return;
                        case 18:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(short*)(ptrDst + 16) = *(short*)(ptrSrc + 16);
                            return;
                        case 19:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(short*)(ptrDst + 16) = *(short*)(ptrSrc + 16);
                            *(ptrDst + 18) = *(ptrSrc + 18);
                            return;
                        case 20:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(int*)(ptrDst + 16) = *(int*)(ptrSrc + 16);
                            return;

                        case 21:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(int*)(ptrDst + 16) = *(int*)(ptrSrc + 16);
                            *(ptrDst + 20) = *(ptrSrc + 20);
                            return;
                        case 22:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(int*)(ptrDst + 16) = *(int*)(ptrSrc + 16);
                            *(short*)(ptrDst + 20) = *(short*)(ptrSrc + 20);
                            return;
                        case 23:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(int*)(ptrDst + 16) = *(int*)(ptrSrc + 16);
                            *(short*)(ptrDst + 20) = *(short*)(ptrSrc + 20);
                            *(ptrDst + 22) = *(ptrSrc + 22);
                            return;
                        case 24:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            return;
                        case 25:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(ptrDst + 24) = *(ptrSrc + 24);
                            return;
                        case 26:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(short*)(ptrDst + 24) = *(short*)(ptrSrc + 24);
                            return;
                        case 27:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(short*)(ptrDst + 24) = *(short*)(ptrSrc + 24);
                            *(ptrDst + 26) = *(ptrSrc + 26);
                            return;
                        case 28:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(int*)(ptrDst + 24) = *(int*)(ptrSrc + 24);
                            return;
                        case 29:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(int*)(ptrDst + 24) = *(int*)(ptrSrc + 24);
                            *(ptrDst + 28) = *(ptrSrc + 28);
                            return;
                        case 30:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(int*)(ptrDst + 24) = *(int*)(ptrSrc + 24);
                            *(short*)(ptrDst + 28) = *(short*)(ptrSrc + 28);
                            return;
                        case 31:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(int*)(ptrDst + 24) = *(int*)(ptrSrc + 24);
                            *(short*)(ptrDst + 28) = *(short*)(ptrSrc + 28);
                            *(ptrDst + 30) = *(ptrSrc + 30);
                            return;
                        case 32:
                            *(long*)ptrDst = *(long*)ptrSrc;
                            *(long*)(ptrDst + 8) = *(long*)(ptrSrc + 8);
                            *(long*)(ptrDst + 16) = *(long*)(ptrSrc + 16);
                            *(long*)(ptrDst + 24) = *(long*)(ptrSrc + 24);
                            return;
                        default:
                            break;
                    }

                    long* lpSrc = (long*)ptrSrc;
                    long* ldSrc = (long*)ptrDst;
                    while (count >= 64)
                    {
                        *(ldSrc + 0) = *(lpSrc + 0);
                        *(ldSrc + 1) = *(lpSrc + 1);
                        *(ldSrc + 2) = *(lpSrc + 2);
                        *(ldSrc + 3) = *(lpSrc + 3);
                        *(ldSrc + 4) = *(lpSrc + 4);
                        *(ldSrc + 5) = *(lpSrc + 5);
                        *(ldSrc + 6) = *(lpSrc + 6);
                        *(ldSrc + 7) = *(lpSrc + 7);
                        if (count == 64)
                            return;
                        count -= 64;
                        lpSrc += 8;
                        ldSrc += 8;
                    }
                    if (count > 32)
                    {
                        *(ldSrc + 0) = *(lpSrc + 0);
                        *(ldSrc + 1) = *(lpSrc + 1);
                        *(ldSrc + 2) = *(lpSrc + 2);
                        *(ldSrc + 3) = *(lpSrc + 3);
                        count -= 32;
                        lpSrc += 4;
                        ldSrc += 4;
                    }

                    ptrSrc = (byte*)lpSrc;
                    ptrDst = (byte*)ldSrc;
                    goto SMALLTABLE;
                }
            }

        }

        /// <summary>
        /// Compute xxhash64 for the unsafe array of memory
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="length"></param>
        /// <param name="seed"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ulong UnsafeComputeHash(byte* ptr, int length, ulong seed = 0)
        {
            byte* end = ptr + length;
            ulong h64;

            if (length >= 32)

            {
                byte* limit = end - 32;

                ulong v1 = seed + p1 + p2;
                ulong v2 = seed + p2;
                ulong v3 = seed + 0;
                ulong v4 = seed - p1;

                do
                {
                    v1 += *((ulong*)ptr) * p2;
                    v1 = BitUtils.RotateLeft(v1, 31); // rotl 31
                    v1 *= p1;
                    ptr += 8;

                    v2 += *((ulong*)ptr) * p2;
                    v2 = BitUtils.RotateLeft(v2, 31); // rotl 31
                    v2 *= p1;
                    ptr += 8;

                    v3 += *((ulong*)ptr) * p2;
                    v3 = BitUtils.RotateLeft(v3, 31); // rotl 31
                    v3 *= p1;
                    ptr += 8;

                    v4 += *((ulong*)ptr) * p2;
                    v4 = BitUtils.RotateLeft(v4, 31); // rotl 31
                    v4 *= p1;
                    ptr += 8;

                } while (ptr <= limit);

                h64 = BitUtils.RotateLeft(v1, 1) +  // rotl 1
                      BitUtils.RotateLeft(v2, 7) +  // rotl 7
                      BitUtils.RotateLeft(v3, 12) + // rotl 12
                      BitUtils.RotateLeft(v4, 18);  // rotl 18

                // merge round
                v1 *= p2;
                v1 = BitUtils.RotateLeft(v1, 31); // rotl 31
                v1 *= p1;
                h64 ^= v1;
                h64 = h64 * p1 + p4;

                // merge round
                v2 *= p2;
                v2 = BitUtils.RotateLeft(v2, 31); // rotl 31
                v2 *= p1;
                h64 ^= v2;
                h64 = h64 * p1 + p4;

                // merge round
                v3 *= p2;
                v3 = BitUtils.RotateLeft(v3, 31); // rotl 31
                v3 *= p1;
                h64 ^= v3;
                h64 = h64 * p1 + p4;

                // merge round
                v4 *= p2;
                v4 = BitUtils.RotateLeft(v4, 31); // rotl 31
                v4 *= p1;
                h64 ^= v4;
                h64 = h64 * p1 + p4;
            }
            else
            {
                h64 = seed + p5;
            }

            h64 += (ulong)length;

            // finalize
            while (ptr <= end - 8)
            {
                ulong t1 = *((ulong*)ptr) * p2;
                t1 = BitUtils.RotateLeft(t1, 31); // rotl 31
                t1 *= p1;
                h64 ^= t1;
                h64 = BitUtils.RotateLeft(h64, 27) * p1 + p4; // (rotl 27) * p1 + p4
                ptr += 8;
            }

            if (ptr <= end - 4)
            {
                h64 ^= *((uint*)ptr) * p1;
                h64 = BitUtils.RotateLeft(h64, 23) * p2 + p3; // (rotl 23) * p2 + p3
                ptr += 4;
            }

            while (ptr < end)
            {
                h64 ^= *((byte*)ptr) * p5;
                h64 = BitUtils.RotateLeft(h64, 11) * p1; // (rotl 11) * p1
                ptr += 1;
            }

            // avalanche
            h64 ^= h64 >> 33;
            h64 *= p2;
            h64 ^= h64 >> 29;
            h64 *= p3;
            h64 ^= h64 >> 32;

            return h64;
        }

        /// <summary>
        /// Compute xxHash for the stream
        /// </summary>
        /// <param name="stream">The stream of data</param>
        /// <param name="bufferSize">The buffer size</param>
        /// <param name="seed">The seed number</param>
        /// <returns>The hash</returns>
        public static ulong ComputeHash(Stream stream, int bufferSize = 8192, ulong seed = 0)
        {
            Debug.Assert(stream != null);
            Debug.Assert(bufferSize > 32);

            // Optimizing memory allocation
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize + 32);

            int readBytes;
            int offset = 0;
            long length = 0;

            // Prepare the seed vector
            ulong v1 = seed + p1 + p2;
            ulong v2 = seed + p2;
            ulong v3 = seed + 0;
            ulong v4 = seed - p1;

            try
            {
                // Read flow of bytes
                while ((readBytes = stream.Read(buffer, offset, bufferSize)) > 0)
                {
                    length = length + readBytes;
                    offset = offset + readBytes;

                    if (offset < 32) continue;

                    int r = offset % 32; // remain
                    int l = offset - r;  // length

                    // Process the next chunk 
                    UnsafeAlign(buffer, l, ref v1, ref v2, ref v3, ref v4);

                    // Put remaining bytes to buffer
                    UnsafeBuffer.BlockCopy(buffer, l, buffer, 0, r);
                    offset = r;
                }

                // Process the final chunk
                ulong h64 = UnsafeFinal(buffer, offset, ref v1, ref v2, ref v3, ref v4, length, seed);

                return h64;
            }
            finally
            {
                // Free memory
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Compute the first part of xxhash64 (need for the streaming api)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="l"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <param name="v4"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UnsafeAlign(byte[] data, int l, ref ulong v1, ref ulong v2, ref ulong v3, ref ulong v4)
        {
            fixed (byte* pData = &data[0])
            {
                byte* ptr = pData;
                byte* limit = ptr + l;

                do
                {
                    v1 += *((ulong*)ptr) * p2;
                    v1 = BitUtils.RotateLeft(v1, 31); // rotl 31
                    v1 *= p1;
                    ptr += 8;

                    v2 += *((ulong*)ptr) * p2;
                    v2 = BitUtils.RotateLeft(v2, 31); // rotl 31
                    v2 *= p1;
                    ptr += 8;

                    v3 += *((ulong*)ptr) * p2;
                    v3 = BitUtils.RotateLeft(v3, 31); // rotl 31
                    v3 *= p1;
                    ptr += 8;

                    v4 += *((ulong*)ptr) * p2;
                    v4 = BitUtils.RotateLeft(v4, 31); // rotl 31
                    v4 *= p1;
                    ptr += 8;

                } while (ptr < limit);
            }
        }

        /// <summary>
        /// Compute the second part of xxhash64 (need for the streaming api)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="l"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <param name="v4"></param>
        /// <param name="length"></param>
        /// <param name="seed"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong UnsafeFinal(byte[] data, int l, ref ulong v1, ref ulong v2, ref ulong v3, ref ulong v4, long length, ulong seed)
        {
            fixed (byte* pData = &data[0])
            {
                byte* ptr = pData;
                byte* end = pData + l;
                ulong h64;

                if (length >= 32)
                {
                    h64 = BitUtils.RotateLeft(v1, 1) +  // rotl 1
                          BitUtils.RotateLeft(v2, 7) +  // rotl 7
                          BitUtils.RotateLeft(v3, 12) + // rotl 12
                          BitUtils.RotateLeft(v4, 18);  // rotl 18

                    // merge round
                    v1 *= p2;
                    v1 = BitUtils.RotateLeft(v1, 31); // rotl 31
                    v1 *= p1;
                    h64 ^= v1;
                    h64 = h64 * p1 + p4;

                    // merge round
                    v2 *= p2;
                    v2 = BitUtils.RotateLeft(v2, 31); // rotl 31
                    v2 *= p1;
                    h64 ^= v2;
                    h64 = h64 * p1 + p4;

                    // merge round
                    v3 *= p2;
                    v3 = BitUtils.RotateLeft(v3, 31); // rotl 31
                    v3 *= p1;
                    h64 ^= v3;
                    h64 = h64 * p1 + p4;

                    // merge round
                    v4 *= p2;
                    v4 = BitUtils.RotateLeft(v4, 31); // rotl 31
                    v4 *= p1;
                    h64 ^= v4;
                    h64 = h64 * p1 + p4;

                }
                else
                {
                    h64 = seed + p5;
                }

                h64 += (ulong)length;

                // finalize
                while (ptr <= end - 8)
                {
                    ulong t1 = *((ulong*)ptr) * p2;
                    t1 = BitUtils.RotateLeft(t1, 31); // rotl 31
                    t1 *= p1;
                    h64 ^= t1;
                    h64 = BitUtils.RotateLeft(h64, 27) * p1 + p4; // (rotl 27) * p1 + p4
                    ptr += 8;
                }

                if (ptr <= end - 4)
                {
                    h64 ^= *((uint*)ptr) * p1;
                    h64 = BitUtils.RotateLeft(h64, 23) * p2 + p3; // (rotl 27) * p2 + p3
                    ptr += 4;
                }

                while (ptr < end)
                {
                    h64 ^= *((byte*)ptr) * p5;
                    h64 = BitUtils.RotateLeft(h64, 11) * p1; // (rotl 11) * p1
                    ptr += 1;
                }

                // avalanche
                h64 ^= h64 >> 33;
                h64 *= p2;
                h64 ^= h64 >> 29;
                h64 *= p3;
                h64 ^= h64 >> 32;

                return h64;
            }
        }
    }
}
