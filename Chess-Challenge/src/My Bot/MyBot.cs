using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 2;
    private Timer _timer;

    public MyBot()
    {
    }

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;

        var legalMoves = board.GetLegalMoves().ToList();
        if (!legalMoves.Any())
        {
            return default; // No legal moves, which means it's either a checkmate or stalemate.
        }

        if (board.PlyCount <= 2)
        {
            legalMoves = legalMoves.OrderBy(x => Guid.NewGuid()).ToList();  // Shuffle moves
        }

        int depth = board.PlyCount <= 2 ? 3 : 1;  // If it's the first or second move, start at depth 3. 
        Move bestMove = legalMoves.First();

        double timeForThisMove = _timer.MillisecondsRemaining / (double)40; // Assume we'll make 40 more moves
        DateTime startTime = DateTime.Now;

        // Iterative deepening
        while ((DateTime.Now - startTime).TotalMilliseconds < timeForThisMove && depth <= 5) // Limiting maximum depth to 5 for safety
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

        // Simple move ordering: prioritize captures.
        legalMoves = legalMoves.OrderByDescending(move => move.IsCapture).ToList();

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

    private double AlphaBeta(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0 || _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds / (double)board.PlyCount))
            return QuiescenceSearch(board, alpha, beta, QUIESCENCE_MAX_DEPTH);

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double score = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
                return beta;

            if (score > alpha)
            {
                alpha = score;
                if (alpha >= beta)
                    break;  // Prune the search tree.
            }
        }

        return alpha;
    }

    private double QuiescenceSearch(Board board, double alpha, double beta, int depth)
    {
        if (depth == 0 || _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds / (double)board.PlyCount))
            return EvaluateBoard(board);

        foreach (var move in board.GetLegalMoves())
        {
            if (move.IsCapture)
            {
                board.MakeMove(move);
                double score = -QuiescenceSearch(board, -beta, -alpha, depth - 1);
                board.UndoMove(move);

                if (score >= beta)
                    return beta;

                if (score > alpha)
                    alpha = score;
            }
        }

        return alpha;
    }

    private double EvaluateBoard(Board board)
    {
        double score = 0;

        double pawnValue = 1.0;
        double knightValue = 3.0;
        double bishopValue = 3.0;
        double rookValue = 5.0;
        double queenValue = 9.0;
        double kingSafetyValue = -20.0;

        var whitePawns = board.GetPieceList(PieceType.Pawn, true).ToList();
        var blackPawns = board.GetPieceList(PieceType.Pawn, false).ToList();

        // Basic material count
        score += (whitePawns.Count - blackPawns.Count) * pawnValue;
        score += (board.GetPieceList(PieceType.Knight, true).Count - board.GetPieceList(PieceType.Knight, false).Count) * knightValue;
        score += (board.GetPieceList(PieceType.Bishop, true).Count - board.GetPieceList(PieceType.Bishop, false).Count) * bishopValue;
        score += (board.GetPieceList(PieceType.Rook, true).Count - board.GetPieceList(PieceType.Rook, false).Count) * rookValue;
        score += (board.GetPieceList(PieceType.Queen, true).Count - board.GetPieceList(PieceType.Queen, false).Count) * queenValue;

        // King Safety
        if (board.IsInCheck())
        {
            score += board.IsWhiteToMove ? kingSafetyValue : -kingSafetyValue;
        }

        // Pawn Structures
        foreach (var pawn in whitePawns)
        {
            if (IsIsolatedPawn(pawn, whitePawns))
                score -= 0.5;
            if (IsPassedPawn(pawn, blackPawns))
                score += 1;
        }
        foreach (var pawn in blackPawns)
        {
            if (IsIsolatedPawn(pawn, blackPawns))
                score += 0.5;
            if (IsPassedPawn(pawn, whitePawns))
                score -= 1;
        }

        // Central Control
        double centralControlValue = 0.5;
        score += whitePawns.Count(p => p.Square.File == 'd' || p.Square.File == 'e') * centralControlValue;
        score -= blackPawns.Count(p => p.Square.File == 'd' || p.Square.File == 'e') * centralControlValue;

        return board.IsWhiteToMove ? score : -score;
    }

    private bool IsIsolatedPawn(Piece pawn, List<Piece> pawnsOfSameColor)
    {
        return !pawnsOfSameColor.Any(p => Math.Abs(p.Square.File - pawn.Square.File) == 1 && p.Square.Rank == pawn.Square.Rank);
    }

    private bool IsPassedPawn(Piece pawn, List<Piece> enemyPawns)
    {
        char file = (char)(pawn.Square.File + 'a');
        int rank = pawn.Square.Rank;

        return !enemyPawns.Any(p => p.Square.Rank > rank && (p.Square.File == file || Math.Abs(p.Square.File - file) == 1));
    }
    private int CalculateDynamicDepth(ChessChallenge.API.Timer timer)
    {
        double estimatedMovesRemaining = (timer.GameStartTimeMilliseconds - timer.MillisecondsElapsedThisTurn) / (double)timer.MillisecondsElapsedThisTurn;
        double averageTimePerMove = timer.MillisecondsRemaining / estimatedMovesRemaining;

        if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.01) // less than 1% of the total time
            return 1;
        else if (averageTimePerMove < timer.GameStartTimeMilliseconds * 0.05) // less than 5% of the total time
            return 2;
        else
            return 3;
    }
}
