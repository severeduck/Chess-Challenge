using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 2;
    private int _maxDepth;
    private Timer _timer;

    public MyBot()
    {
        // Constructor remains empty for now since we removed the opening book initialization.
    }

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _maxDepth = CalculateDynamicDepth(timer);
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

        return bestMove.Value;
    }

    private double AlphaBeta(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0 || _timer.MillisecondsRemaining < 10000)
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
        if (depth == 0 || _timer.MillisecondsRemaining < 10000)
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
        // Placeholder evaluation function
        // return board.MaterialDifference;
        return 0;
    }

    private int CalculateDynamicDepth(Timer timer)
    {
        // double timePerMove = timer.RemainingTime.TotalSeconds / timer.TotalMoves;
        // if (timePerMove < 1) return 1;
        // if (timePerMove < 5) return 2;
        return 3; // Adjust this as needed.
    }
}
