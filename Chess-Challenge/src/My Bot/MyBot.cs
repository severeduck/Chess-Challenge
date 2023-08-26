using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 3;
    private Timer _timer;

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        var legalMoves = board.GetLegalMoves().ToList();

        if (!legalMoves.Any())
            return default;

        int depth = CalculateDynamicDepth(timer);
        Move bestMove = legalMoves.First();

        double timeForThisMove = _timer.MillisecondsRemaining / 50.0;
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeForThisMove && depth <= 6)
        {
            bestMove = AlphaBetaSearch(board, legalMoves, depth++);
        }

        return bestMove;
    }

    private Move AlphaBetaSearch(Board board, List<Move> legalMoves, int depth)
    {
        Move bestMove = legalMoves.First();
        double alpha = double.NegativeInfinity;
        double beta = double.PositiveInfinity;

        legalMoves = legalMoves.OrderByDescending(move => move.IsCapture)
                               .ThenByDescending(move => MoveHistoryScore(move))
                               .ToList();

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

    private static readonly Dictionary<Move, int> moveHistory = new Dictionary<Move, int>();

    private int MoveHistoryScore(Move move)
    {
        return moveHistory.TryGetValue(move, out int score) ? score : 0;
    }

    private double AlphaBeta(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0 || IsTimeRunningOut())
            return QuiescenceSearch(board, alpha, beta, QUIESCENCE_MAX_DEPTH);

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double score = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
            {
                moveHistory[move] = moveHistory.GetValueOrDefault(move) + 1;
                return beta;
            }

            if (score > alpha)
                alpha = score;
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

    private int CalculateDynamicDepth(ChessChallenge.API.Timer timer)
    {
        double estimatedMovesRemaining = (timer.GameStartTimeMilliseconds - timer.MillisecondsElapsedThisTurn) / (double)timer.MillisecondsElapsedThisTurn;
        double averageTimePerMove = timer.MillisecondsRemaining / estimatedMovesRemaining;

        if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.01)
            return 1;
        else if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.05)
            return 2;
        else
            return 3;
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
        return _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds / 1024.0);
    }
}
