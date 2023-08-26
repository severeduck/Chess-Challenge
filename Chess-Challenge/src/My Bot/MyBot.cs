using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 2;
    private const int REGULAR_SEARCH_CUTOFF = 5000; // 5 seconds in milliseconds
    private const int QUIESCENCE_SEARCH_CUTOFF = 3000; // 3 seconds in milliseconds
    private int _maxDepth;
    private Timer _timer;

    public MyBot()
    {
    }

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _maxDepth = CalculateDynamicDepth(board, timer);
        return AlphaBetaSearch(board, _maxDepth);
    }

    private Move AlphaBetaSearch(Board board, int depth)
    {
        Move? bestMove = null;
        double alpha = double.NegativeInfinity;
        double beta = double.PositiveInfinity;

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double value = -AlphaBeta(board, depth - 1, -beta, -alpha);

            if (value > alpha)
            {
                alpha = value;
                bestMove = move;
            }
            board.UndoMove(move);
        }

        if (!bestMove.HasValue)
            throw new Exception("No legal moves available.");

        return bestMove.Value;
    }

    private double AlphaBeta(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0 || _timer.MillisecondsRemaining < REGULAR_SEARCH_CUTOFF)
            return QuiescenceSearch(board, alpha, beta, QUIESCENCE_MAX_DEPTH);

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double score = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private double QuiescenceSearch(Board board, double alpha, double beta, int depth)
    {
        if (depth == 0 || _timer.MillisecondsRemaining < QUIESCENCE_SEARCH_CUTOFF)
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
        double pawnValue = 1.0;
        double knightValue = 3.0;
        double bishopValue = 3.0;
        double rookValue = 5.0;
        double queenValue = 9.0;

        double whiteScore = board.GetPieceList(PieceType.Pawn, true).Count * pawnValue
                          + board.GetPieceList(PieceType.Knight, true).Count * knightValue
                          + board.GetPieceList(PieceType.Bishop, true).Count * bishopValue
                          + board.GetPieceList(PieceType.Rook, true).Count * rookValue
                          + board.GetPieceList(PieceType.Queen, true).Count * queenValue;

        double blackScore = board.GetPieceList(PieceType.Pawn, false).Count * pawnValue
                          + board.GetPieceList(PieceType.Knight, false).Count * knightValue
                          + board.GetPieceList(PieceType.Bishop, false).Count * bishopValue
                          + board.GetPieceList(PieceType.Rook, false).Count * rookValue
                          + board.GetPieceList(PieceType.Queen, false).Count * queenValue;

        return whiteScore - blackScore; // Assuming positive is better for white.
    }

    private int CalculateDynamicDepth(Board board, Timer timer)
    {
        double movesPlayed = board.PlyCount / 2.0; // Convert ply to full moves
        double movesRemaining = 40 - movesPlayed; // Assuming a typical 40-move game for simplicity
        if (movesRemaining <= 0) movesRemaining = 1; // To avoid division by zero
        
        double averageTimePerMove = timer.MillisecondsRemaining / movesRemaining;

        if (averageTimePerMove < 1000)
            return 1;
        else if (averageTimePerMove < 5000)
            return 2;
        else
            return 3; 
    }
}
