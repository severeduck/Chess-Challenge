using ChessChallenge.API;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 2;
    private int _maxDepth;
    private Timer _timer;

    // This dictionary will only hold the UCI move strings, as the board will be provided when initializing the Move.

    public MyBot()
    {
    }

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _maxDepth = CalculateDynamicDepth(timer);

        var legalMoves = board.GetLegalMoves().ToList();
        if (!legalMoves.Any())
        {
            return default; // No legal moves, which means it's either a checkmate or stalemate.
        }

        int depth = 1;
        Move bestMove = legalMoves.First();

        // Iterative deepening
        while (_timer.MillisecondsRemaining > 100) // Leaving a buffer of 100ms
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
                alpha = score;
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
    // Basic piece values
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

    double scoreDifference = whiteScore - blackScore;

    // Return the evaluation from the perspective of the current player
    return board.IsWhiteToMove ? scoreDifference : -scoreDifference;
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
