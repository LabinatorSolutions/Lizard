﻿namespace Lizard.Logic.Search.Ordering
{
    public static unsafe class MoveOrdering
    {

        /// <summary>
        /// Gives each of the <paramref name="size"/> pseudo-legal moves in the <paramref name = "list"/> scores.
        /// </summary>
        /// <param name="ss">The entry containing Killer moves to prioritize</param>
        /// <param name="history">A reference to a <see cref="HistoryTable"/> with MainHistory/CaptureHistory scores.</param>
        /// <param name="ttMove">The <see cref="CondensedMove"/> retrieved from the TT probe, or Move.Null if the probe missed (ss->ttHit == false). </param>
        public static void AssignScores(Position pos, SearchStackEntry* ss, in HistoryTable history,
                ScoredMove* list, int size, CondensedMove ttMove, bool doKillers = true)
        {
            ref Bitboard bb = ref pos.bb;
            int pc = bb.GetColorAtIndex(list[0].Move.From);

            for (int i = 0; i < size; i++)
            {
                ref ScoredMove sm = ref list[i];
                Move m = sm.Move;
                int moveTo = m.To;
                int moveFrom = m.From;

                if (m.Equals(ttMove))
                {
                    sm.Score = int.MaxValue - 100000;
                }
                else if (doKillers && m == ss->Killer0)
                {
                    sm.Score = int.MaxValue - 1000000;
                }
                else if (doKillers && m == ss->Killer1)
                {
                    sm.Score = int.MaxValue - 2000000;
                }
                else if (m.Capture)
                {
                    int capturedPiece = bb.GetPieceAtIndex(moveTo);
                    int capIdx = HistoryTable.CapIndex(pc, bb.GetPieceAtIndex(moveFrom), moveTo, capturedPiece);
                    sm.Score = (13 * GetPieceValue(capturedPiece)) + (history.CaptureHistory[capIdx] / 12);
                }
                else
                {
                    int pt = bb.GetPieceAtIndex(moveFrom);
                    int contIdx = PieceToHistory.GetIndex(pc, pt, moveTo);

                    sm.Score = 2 * history.MainHistory[HistoryTable.HistoryIndex(pc, m)];
                    sm.Score += 2 * (*(ss - 1)->ContinuationHistory)[contIdx];
                    sm.Score += (*(ss - 2)->ContinuationHistory)[contIdx];
                    sm.Score += (*(ss - 4)->ContinuationHistory)[contIdx];
                    sm.Score += (*(ss - 6)->ContinuationHistory)[contIdx];

                    if ((pos.State->CheckSquares[pt] & SquareBB[moveTo]) != 0)
                    {
                        sm.Score += 10000;
                    }
                }
            }
        }


        /// <summary>
        /// Assigns scores to each of the <paramref name="size"/> moves in the <paramref name="list"/>.
        /// <br></br>
        /// This is only called for ProbCut, so the only moves in <paramref name="list"/> should be generated 
        /// using <see cref="GenLoud"/>, which only generates captures and promotions.
        /// </summary>
        public static void AssignProbCutScores(ref Bitboard bb, ScoredMove* list, int size)
        {
            for (int i = 0; i < size; i++)
            {
                ref ScoredMove sm = ref list[i];
                Move m = sm.Move;

                sm.Score = EvaluationConstants.SEEValues[m.EnPassant ? Pawn : bb.GetPieceAtIndex(m.To)];
                if (m.Promotion)
                {
                    //  Gives promotions a higher score than captures.
                    //  We can assume a queen promotion is better than most captures.
                    sm.Score += EvaluationConstants.SEEValues[Queen] + 1;
                }
            }
        }


        /// <summary>
        /// Passes over the list of <paramref name="moves"/>, bringing the move with the highest <see cref="ScoredMove.Score"/>
        /// within the range of <paramref name="listIndex"/> and <paramref name="size"/> to the front and returning it.
        /// </summary>
        public static Move OrderNextMove(ScoredMove* moves, int size, int listIndex)
        {
            if (size < 2)
            {
                return moves[listIndex].Move;
            }

            int max = int.MinValue;
            int maxIndex = -1;

            for (int i = listIndex; i < size; i++)
            {
                if (moves[i].Score > max)
                {
                    max = moves[i].Score;
                    maxIndex = i;
                }
            }

            (moves[maxIndex], moves[listIndex]) = (moves[listIndex], moves[maxIndex]);

            return moves[listIndex].Move;
        }


        public static void AssignScores(Position pos, SearchStackEntry* ss, in HistoryTable history,
                Span<ScoredMove> list, int size, CondensedMove ttMove, bool doKillers = true)
        {
            fixed (ScoredMove* ptr = list)
            {
                AssignScores(pos, ss, history, ptr, size, ttMove, doKillers);
            }
        }

        /// <inheritdoc cref="OrderNextMove"/>
        public static Move OrderNextMove(Span<ScoredMove> moves, int size, int listIndex)
        {
            fixed (ScoredMove* ptr = moves)
            {
                return OrderNextMove(ptr, size, listIndex);
            }
        }

    }


}
