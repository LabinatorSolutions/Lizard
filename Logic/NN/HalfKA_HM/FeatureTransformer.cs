﻿
#define HM

using static LTChess.Logic.NN.HalfKA_HM.NNCommon;
using static LTChess.Logic.NN.HalfKA_HM.HalfKA_HM;
using static LTChess.Logic.NN.SIMD;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System;
using System.Numerics;
using System.Reflection;
using System.IO;
using LTChess.Logic.Search;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LTChess.Logic.NN.HalfKA_HM
{
    /// <summary>
    /// Handles Accumulator updates and refreshes, and translates the position into inputs for the network to use.
    /// <para></para>
    /// 
    /// https://github.com/official-stockfish/Stockfish/blob/84f3e867903f62480c33243dd0ecbffd342796fc/src/nnue/nnue_feature_transformer.h
    /// </summary>
    public unsafe class FeatureTransformer
    {
        // Number of output dimensions for one side
        public const uint HalfDimensions = HalfKA_HM.TransformedFeatureDimensions;

        public const uint InputDimensions = HalfKA_HM.Dimensions;
        public const uint OutputDimensions = HalfDimensions * 2;

        public const long BufferSize = OutputDimensions * sizeof(ushort);

        public static uint GetHashValue() => HalfKA_HM.HashValue ^ OutputDimensions;

        public short[] Biases = new short[HalfDimensions];
        public short[] Weights = new short[HalfDimensions * InputDimensions];
        public int[] PSQTWeights = new int[InputDimensions * PSQTBuckets];


        public const int NumRegs = 16;
        public const int NumPsqtRegs = 1;
        public const int LazyThreshold = 1400;

        public const int VectorSize = 16;
        public const int TileHeight = NumRegs * VSize.Byte / 2;
        public const int PsqtTileHeight = NumPsqtRegs * VSize.Byte / 4;


        /// <summary>
        /// Takes the input from the <paramref name="accumulator"/> and places them into <paramref name="output"/>,
        /// refreshing the <paramref name="accumulator"/> if necessary.
        /// </summary>
        [MethodImpl(Optimize)]
        public int TransformFeatures(Position pos, sbyte[] output, ref AccumulatorPSQT accumulator, int bucket)
        {
            RefreshAccumulator(pos, ref accumulator);

            uint NumChunks = HalfDimensions / SimdWidth;
            const int Control = 0b11011000;
            Vector256<sbyte> Zero = Vector256<sbyte>.Zero;

            int[] perspectives = { pos.ToMove, Not(pos.ToMove) };

            var psqt = (accumulator.PSQ(perspectives[0])[bucket] -
                        accumulator.PSQ(perspectives[1])[bucket]) / 2;

            for (int p = 0; p < 2; p++)
            {
                uint offset = (uint)(HalfDimensions * p);

                var accumulation = accumulator[perspectives[p]];

                for (int j = 0; j < NumChunks; ++j)
                {
                    int vectIndex = (int)(offset + (j * VSize.SByte));

                    Vector256<short> sum0 = Load256(accumulation, (j * 2 + 0) * VSize.Short);
                    Vector256<short> sum1 = Load256(accumulation, (j * 2 + 1) * VSize.Short);

                    Vector256<sbyte> saturated = Avx2.PackSignedSaturate(sum0, sum1);
                    Vector256<sbyte> maxVec = Avx2.Max(saturated, Zero);
                    Vector256<long> permuted = Avx2.Permute4x64(maxVec.AsInt64(), Control);

                    Vector256<sbyte> toStore = permuted.AsSByte();
                    Avx2.Store((sbyte*)UnsafeAddrOfPinnedArrayElementUnchecked(output, vectIndex), toStore);
                }
            }

            return psqt;
        }

        /// <summary>
        /// Finds the active features (existing pieces on the board) and updates the Accumulator to include those pieces.
        /// This is comparatively very slow, so it should only be done when absolutely necessary, like when our king moves.
        /// </summary>
        [MethodImpl(Inline)]
        public void RefreshAccumulator(Position pos, ref AccumulatorPSQT accumulator)
        {
            Span<int> active = stackalloc int[MaxActiveDimensions * 2];
            HalfKA_HM.AppendActiveIndices(pos, active);

            for (int perspective = 0; perspective < ColorNB; perspective++)
            {
                var accumulation = accumulator[perspective];
                var PSQTaccumulation = accumulator.PSQ(perspective);

                Span<Vector256<short>> acc = stackalloc Vector256<short>[NumRegs];

                for (int j = 0; j < HalfDimensions / TileHeight; j++)
                {

                    for (int k = 0; k < NumRegs; k++)
                    {
                        Vector256<short> biasTile = Load256(Biases, ((j * TileHeight) + (k * VectorSize)));
                        acc[k] = biasTile;
                    }

                    int i = 0;
                    while (i < MaxActiveDimensions)
                    {
                        int index = active[(i++) + (perspective * MaxActiveDimensions)];
                        if (index <= 0)
                        {
                            break;
                        }

                        for (int k = 0; k < NumRegs; k++)
                        {
                            Vector256<short> column = Load256(Weights, (int)(HalfDimensions * index + j * TileHeight) + (k * VectorSize));
                            acc[k] = Add256(acc[k], column);
                        }
                    }

                    for (int k = 0; k < NumRegs; k++)
                    {
                        Store256(ref acc[k], accumulation, (j * TileHeight) + (k * VectorSize));
                    }

                }



                Span<Vector256<int>> psq = stackalloc Vector256<int>[NumPsqtRegs];

                for (int j = 0; j < PSQTBuckets / PsqtTileHeight; j++)
                {
                    for (int k = 0; k < NumPsqtRegs; k++)
                    {
                        psq[k] = Vector256<int>.Zero;
                    }

                    int i = 0;
                    while (i < MaxActiveDimensions)
                    {
                        int index = active[(i++) + (perspective * MaxActiveDimensions)];
                        if (index <= 0)
                        {
                            break;
                        }

                        for (int k = 0; k < NumPsqtRegs; k++)
                        {
                            int columnOffset = (PSQTBuckets * index + j * PsqtTileHeight);
                            Vector256<int> column = Load256(PSQTWeights, columnOffset + (k * VSize.Int));
                            
                            psq[k] = Add256(psq[k], column);
                        }
                    }

                    for (int k = 0; k < NumPsqtRegs; k++)
                    {
                        Store256(ref psq[k], PSQTaccumulation, (j * PsqtTileHeight) + (k * VSize.Int));
                    }

                }
            }

            accumulator.NeedsRefresh = false;
        }



        /// <summary>
        /// Reads the weights and biases from the network file.
        /// </summary>
        public bool ReadParameters(BinaryReader br)
        {

            uint header = br.ReadUInt32();
            Debug.WriteLine("FeatureTransformer header: " + header.ToString("X"));

            for (int i = 0; i < HalfDimensions; i++)
            {
                if (IsLEB128)
                {
                    //  TODO: this obviously won't work
                    Biases[i] = (short)LEB128.LEB128.ReadLEB128Signed(br.BaseStream);
                }
                else
                {
                    Biases[i] = br.ReadInt16();
                }
            }

            for (int i = 0; i < HalfDimensions * InputDimensions; i++)
            {
                if (IsLEB128)
                {
                    Weights[i] = (short)LEB128.LEB128.ReadLEB128Signed(br.BaseStream);
                }
                else
                {
                    Weights[i] = br.ReadInt16();
                }
            }

            for (int i = 0; i < PSQTBuckets * InputDimensions; i++)
            {
                if (IsLEB128)
                {
                    PSQTWeights[i] = (int)LEB128.LEB128.ReadLEB128Signed(br.BaseStream);
                }
                else
                {
                    PSQTWeights[i] = br.ReadInt32();
                }
            }

            return true;
        }

    }
}