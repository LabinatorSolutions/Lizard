﻿using System.Runtime.InteropServices;
using System.Text;

namespace LTChess.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CondensedMove
    {
        //  6 bits for: from, to, idxEnPassant, idxChecker, idxDoubleChecker
        //  3 bits for: PromotionTo
        //  1 bit for each of the 6 (7 when IsMate works) flags.
        //  Total of 39.

        //  fffffftttttteeeeeeccccccdddddd

        public const int IndexSize = 6;
        public const int DataSize = (IndexSize * 5);

        public const int PromotionToIndex = 8;
        public const int NumFlags = 6;

        public const int FlagsSize = (3 + NumFlags);

        private int data;
        private short flags;


        public CondensedMove(Move m)
        {
            data = (m.from << (DataSize - IndexSize));
            data |= (m.to << (DataSize - (2 * IndexSize)));
            data |= (m.idxEnPassant << (DataSize - (3 * IndexSize)));
            data |= (m.idxChecker << (DataSize - (4 * IndexSize)));
            data |= (m.idxDoubleChecker << (DataSize - (5 * IndexSize)));

            flags = (short)(m.PromotionTo << PromotionToIndex);
            flags |= (short)((m.Capture ? 1 : 0) << 5);
            flags |= (short)((m.EnPassant ? 1 : 0) << 4);
            flags |= (short)((m.Castle ? 1 : 0) << 3);
            flags |= (short)((m.CausesCheck ? 1 : 0) << 2);
            flags |= (short)((m.CausesDoubleCheck ? 1 : 0) << 1);
            flags |= (short)(m.Promotion ? 1 : 0);

            Log("cm: [" + ToString() + "]");
        }

        [MethodImpl(Inline)]
        public Move ToMove()
        {
            Move m = new Move(From, To, PromotionTo);
            m.idxEnPassant = idxEnPassant;
            m.idxChecker = idxChecker;
            m.idxDoubleChecker = idxDoubleChecker;

            m.Capture = Capture;
            m.EnPassant = EnPassant;
            m.Castle = Castle;
            m.CausesCheck = CausesCheck;
            m.CausesDoubleCheck = CausesDoubleCheck;
            m.Promotion = Promotion;

            return m;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(Convert.ToString(data, 2));
            if (sb.Length != 32)
            {
                sb.Insert(0, "0", 32 - sb.Length);
            }

            sb.Insert(30, " ");
            sb.Insert(24, " ");
            sb.Insert(18, " ");
            sb.Insert(12, " ");
            sb.Insert(6, " ");

            sb.Insert(2, "|");

            sb.Append(Convert.ToString(flags, 2));

            sb.Insert(35, " ");

            return sb.ToString();
        }

        public int From => (data >> (DataSize - IndexSize));

        public int To => (data >> (DataSize - (2 * IndexSize)));

        public int idxEnPassant => (data >> (DataSize - (3 * IndexSize)));

        public int idxChecker => (data >> (DataSize - (4 * IndexSize)));

        public int idxDoubleChecker => (data >> (DataSize - (5 * IndexSize)));

        public int PromotionTo => (flags >> PromotionToIndex);

        public bool Capture => ((flags & 0b100000) != 0);

        public bool EnPassant => ((flags & 0b10000) != 0);

        public bool Castle => ((flags & 0b1000) != 0);

        public bool CausesCheck => ((flags & 0b100) != 0);

        public bool CausesDoubleCheck => ((flags & 0b10) != 0);

        public bool Promotion => ((flags & 0b1) != 0);
    }
}
