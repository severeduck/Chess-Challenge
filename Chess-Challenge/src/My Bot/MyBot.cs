    using ChessChallenge.API;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MyBot : IChessBot
    {
        // Constants
        private const int MAX_SEARCH_DEPTH = 6;
        private const int MAX_SEARCH_MATE_DEPTH = 3;
        private const double PANIC_TIME_FRACTION = 0.25;  // Once only 33% of the original time remains, enter panic mode

        // Fields
        private Timer _timer;

        public Move Think(Board board, Timer timer)
        {
            _timer = timer;

            if (board.GameMoveHistory.Length == 0)
            {
                return new Move("d2d4", board);
            }

            var legalMoves = board.GetLegalMoves().ToList();

            if (!legalMoves.Any()) return default;

            // Order moves for efficiency
            legalMoves = OrderMovesForEfficiency(legalMoves);

            // Check for mates within the depth limits
            for (int mateDepth = 1; mateDepth <= MAX_SEARCH_MATE_DEPTH; mateDepth++)
            {
                Move? mateMove = CheckForMateInNMoves(board, mateDepth);
                if (mateMove != null) return (Move)mateMove;
            }

            int depth = CalculateDynamicDepth(board, timer);
            Move bestMove = legalMoves.First();
            DateTime startTime = DateTime.Now;

            double timeFraction = IsInPanicMode() ? 0.01 : 0.02;
            double timeForThisMove = _timer.MillisecondsRemaining * timeFraction;

            while ((DateTime.Now - startTime).TotalMilliseconds + 50 < timeForThisMove && depth <= MAX_SEARCH_DEPTH)
            {
                bestMove = AlphaBetaSearch(board, legalMoves, depth, double.NegativeInfinity, double.PositiveInfinity);
                depth++;
            }

            if (bestMove == default)
            {
                Random rand = new Random();
                bestMove = legalMoves[rand.Next(legalMoves.Count)];
            }

            return bestMove;
        }
        private List<Move> OrderMovesForEfficiency(List<Move> moves)
        {
            return moves.OrderByDescending(move => move.IsCapture)
                        .ThenByDescending(move => MVVLVA(move))
                        .ToList();
        }

        public bool IsGameOver(Board board)
        {
            return board.IsInCheckmate() || board.IsDraw();
        }

        private Move? CheckForMateInNMoves(Board board, int n)
        {
            double originalAlpha = double.NegativeInfinity;
            double originalBeta = double.PositiveInfinity;

            foreach (var move in board.GetLegalMoves())
            {
                board.MakeMove(move);

                // We use a negative sign because we're flipping perspectives for the opponent
                double score = -AlphaBetaMateSearch(board, n * 2 - 1, -originalBeta, -originalAlpha);  // x2 because each move consists of a move by both white and black.

                board.UndoMove(move);

                if (score == double.PositiveInfinity)  // If the returned score signifies a mate
                {
                    return move;  // Return the mating move
                }
            }
            return null;
        }

        private double AlphaBetaMateSearch(Board board, int depth, double alpha, double beta)
        {
            if (depth == 0 || IsGameOver(board))
            {
                if (board.IsInCheckmate())
                {
                    return board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;  // return positive/negative infinity based on who has the move.
                }
                return 0;  // No mate found at this depth.
            }

            foreach (var move in board.GetLegalMoves())
            {
                board.MakeMove(move);

                double score = -AlphaBetaMateSearch(board, depth - 1, -beta, -alpha);

                board.UndoMove(move);

                if (score >= beta)
                {
                    return beta;
                }
                alpha = Math.Max(alpha, score);
            }
            return alpha;
        }

        private Move AlphaBetaSearch(Board board, List<Move> legalMoves, int depth, double alpha, double beta)
        {
            Move bestMove = legalMoves.First();

            foreach (var move in legalMoves)
            {
                board.MakeMove(move);
                double value = -AlphaBeta(board, depth - 1, -beta, -alpha);
                board.UndoMove(move);

                if (value > alpha)
                {
                    alpha = value;
                    bestMove = move;
                }
            }

            return bestMove;
        }


        // Most Valuable Victim - Least Valuable Attacker
        private int MVVLVA(Move move)
        {
            if (!move.IsCapture) return -1;
            int attackerValue = PieceValue(move.MovePieceType);
            int victimValue = PieceValue(move.CapturePieceType);
            return victimValue - attackerValue;
        }

        private int PieceValue(PieceType pieceType)
        {
            return pieceType switch
            {
                PieceType.Pawn => 1,
                PieceType.Knight => 3,
                PieceType.Bishop => 3,
                PieceType.Rook => 5,
                PieceType.Queen => 9,
                _ => 0
            };
        }

        private double AlphaBeta(Board board, int depth, double alpha, double beta)
        {
            if (depth == 0 || IsGameOver(board))
                return EvaluateBoard(board);

            var legalMoves = board.GetLegalMoves().ToList();

            if (!legalMoves.Any()) return 0;

            legalMoves = OrderMovesForEfficiency(legalMoves);

            foreach (var move in legalMoves)
            {
                board.MakeMove(move);
                double value = -AlphaBeta(board, depth - 1, -beta, -alpha);
                board.UndoMove(move);

                if (value >= beta)
                    return beta;

                if (value > alpha)
                    alpha = value;
            }

            return alpha;
        }

        private double EvaluateBoard(Board board)
        {
            // ... [your existing evaluation code here, if any]

            double score = 0;

            // Doubled Pawns
            var whitePawns = board.GetPieceList(PieceType.Pawn, true).ToList();
            var blackPawns = board.GetPieceList(PieceType.Pawn, false).ToList();
            score -= whitePawns.GroupBy(p => p.Square.File).Where(g => g.Count() > 1).Count() * 0.5;
            score += blackPawns.GroupBy(p => p.Square.File).Where(g => g.Count() > 1).Count() * 0.5;

            // Mobility
            score += 0.1 * board.GetLegalMoves(true).Count();
            score -= 0.1 * board.GetLegalMoves(false).Count();

            // Endgame considerations
            if (GetPieceCount(board, true) <= 7 && GetPieceCount(board, false) <= 7)
            {
                // In the endgame, the king should be more active
                var whiteKing = board.GetPieceList(PieceType.King, true).First().Square;
                var blackKing = board.GetPieceList(PieceType.King, false).First().Square;
                score += 0.3 * (7 - DistanceToCenter(whiteKing));
                score -= 0.3 * (7 - DistanceToCenter(blackKing));
            }

            return board.IsWhiteToMove ? score : -score;
        }
        private int GetPieceCount(Board board, bool isWhite)
        {
            int startIndex = isWhite ? 0 : 6;
            int endIndex = isWhite ? 5 : 11;

            int count = 0;

            var allPieceLists = board.GetAllPieceLists();
            for (int i = startIndex; i <= endIndex; i++)
            {
                count += allPieceLists[i].Count();
            }

            return count;
        }

        private int DistanceToCenter(Square square)
        {
            int centerX = 4;  // e-file, assuming 0-based index
            int centerY = 4;  // 5th rank, assuming 0-based index

            return Math.Abs(centerX - square.File) + Math.Abs(centerY - square.Rank);
        }

        private int CalculateDynamicDepth(Board board, Timer timer)
        {
            double estimatedMovesRemaining = (timer.GameStartTimeMilliseconds - timer.MillisecondsElapsedThisTurn) / (double)timer.MillisecondsElapsedThisTurn;
            double averageTimePerMove = timer.MillisecondsRemaining / estimatedMovesRemaining;

            int depth = 4;  // Starting depth increased

            if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.01)
                depth = 2;
            else if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.05)
                depth = 3;

            // Deepen search in endgame scenarios
            if (GetPieceCount(board, true) <= 7 && GetPieceCount(board, false) <= 7)
                depth += 2; // More aggressive in the endgame

            return depth;
        }

        private double QuiescenceSearch(Board board, double alpha, double beta, int depthLeft)
        {
            if (depthLeft == 0 || IsTimeRunningOut())
            {
                return EvaluateBoard(board);
            }

            double standPat = EvaluateBoard(board);
            if (standPat >= beta)
            {
                return beta;
            }
            if (alpha < standPat)
            {
                alpha = standPat;
            }

            foreach (var move in board.GetLegalMoves().Where(m => m.IsCapture))
            {
                board.MakeMove(move);
                double score = -QuiescenceSearch(board, -beta, -alpha, depthLeft - 1);
                board.UndoMove(move);

                if (score >= beta)
                {
                    return beta;
                }
                if (score > alpha)
                {
                    alpha = score;
                }
            }
            return alpha;
        }

        private bool IsTimeRunningOut()
        {
            double timeMargin = IsInPanicMode() ? 50 : 100;
            return _timer.MillisecondsRemaining <= timeMargin;
        }

        private bool IsInPanicMode()
        {
            return _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds * PANIC_TIME_FRACTION);
        }

    }
