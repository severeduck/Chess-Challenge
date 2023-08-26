using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 3;
    private const int MAX_SEARCH_DEPTH = 6;
    private const int TIME_DIVISOR = 1024;
    private const double TIME_FRACTION = 1.0 / TIME_DIVISOR;
    private Timer _timer;
    private static readonly Move[][] killerMoves = InitializeKillerMoves();

    private static Move[][] InitializeKillerMoves()
    {
        return new Move[6][].Select(m => new Move[2]).ToArray();
    }
    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        var legalMoves = board.GetLegalMoves().ToList();

        if (!legalMoves.Any()) return default;

        int depth = CalculateDynamicDepth(board, timer);
        Move bestMove = legalMoves.First();

        double timeForThisMove = _timer.MillisecondsRemaining / 50.0;
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeForThisMove && depth <= MAX_SEARCH_DEPTH)
        {
            bestMove = AlphaBetaSearch(board, legalMoves, depth, double.NegativeInfinity, double.PositiveInfinity);
            depth++;
        }

        return bestMove;
    }

    private Move AlphaBetaSearch(Board board, List<Move> legalMoves, int depth, double alpha, double beta)
    {
        Move bestMove = legalMoves.First();

        if (depth < 6)
        {
            // Try killer moves first for the given depth
            foreach (var killer in killerMoves[depth])
            {
                if (board.GetLegalMoves().Contains(killer))
                {
                    board.MakeMove(killer);
                    double value = -AlphaBeta(board, depth - 1, -beta, -alpha, killer);
                    board.UndoMove(killer);

                    if (value > alpha)
                    {
                        alpha = value;
                        bestMove = killer;
                    }
                }
            }
        }

        legalMoves = legalMoves.OrderByDescending(move => move.IsCapture)
                               .ThenByDescending(move => MVVLVA(move))
                               .ThenByDescending(move => MoveHistoryScore(move))
                               .ToList();

        foreach (var move in legalMoves.Where(m => depth >= 6 || killerMoves[depth] == null || !killerMoves[depth].Contains(m)))
        {
            board.MakeMove(move);
            double value = -AlphaBeta(board, depth - 1, -beta, -alpha, move);
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

    private static readonly Dictionary<Move, int> moveHistory = new Dictionary<Move, int>();

    private int MoveHistoryScore(Move move)
    {
        return moveHistory.TryGetValue(move, out int score) ? score : 0;
    }

private double AlphaBeta(Board board, int depth, double alpha, double beta, Move currentMove)
    {
        if (depth == 0 || IsTimeRunningOut())
            return QuiescenceSearch(board, alpha, beta, QUIESCENCE_MAX_DEPTH);

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double score = -AlphaBeta(board, depth - 1, -beta, -alpha, move);
            board.UndoMove(move);

            if (score >= beta)
            {
                UpdateKillerMove(depth, move);
                return beta;
            }
            alpha = Math.Max(alpha, score);
        }

        return alpha;
    }

    private void UpdateKillerMove(int depth, Move move)
    {
        // Ensure move is not a capture
        if (!move.IsCapture)
        {
            // Check if killer move for the depth already exists
            if (killerMoves[depth] == null)
            {
                killerMoves[depth] = new Move[2];
            }

            // If the move isn't already a killer move, add it
            if (!killerMoves[depth].Contains(move))
            {
                // Move current killer move to second slot
                killerMoves[depth][1] = killerMoves[depth][0];

                // Add new killer move to the first slot
                killerMoves[depth][0] = move;
            }
        }
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

    private int CalculateDynamicDepth(Board board, ChessChallenge.API.Timer timer)
    {
        double estimatedMovesRemaining = (timer.GameStartTimeMilliseconds - timer.MillisecondsElapsedThisTurn) / (double)timer.MillisecondsElapsedThisTurn;
        double averageTimePerMove = timer.MillisecondsRemaining / estimatedMovesRemaining;

        int depth = 3;

        if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.01)
            depth = 1;
        else if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.05)
            depth = 2;

        // Deepen search in endgame scenarios
        if (GetPieceCount(board, true) <= 7 && GetPieceCount(board, false) <= 7)
            depth += 1;

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
        return _timer.MillisecondsRemaining * (1.0 / TIME_DIVISOR) <= _timer.GameStartTimeMilliseconds;
    }
}
