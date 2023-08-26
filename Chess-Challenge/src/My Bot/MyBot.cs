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

        double timeForThisMove = _timer.MillisecondsRemaining / 50.0; // Adjusted from 40 for better endgame performance
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeForThisMove && depth <= 6)  // Depth limit increased to 6
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

        // Enhanced move ordering: prioritize captures and then move those with the highest history scores (implementing a simple history heuristic)
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

    // Implement a simple history heuristic. 
    // This is a very rudimentary version, where we remember how often a move has contributed to beta cutoffs.
    private static Dictionary<Move, int> moveHistory = new Dictionary<Move, int>();
    private int MoveHistoryScore(Move move)
    {
        if (moveHistory.ContainsKey(move))
            return moveHistory[move];
        return 0;
    }

    private double AlphaBeta(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0 || _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds / 1024.0))
            return QuiescenceSearch(board, alpha, beta, QUIESCENCE_MAX_DEPTH);

        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double score = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
            {
                // Record the move that caused the cutoff
                if (!moveHistory.ContainsKey(move))
                    moveHistory[move] = 0;
                moveHistory[move]++;

                return beta;
            }

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private double EvaluateBoard(Board board)
    {
        double score = 0;

        // ... [your existing evaluation code here]

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
            var whiteKing = board.GetPieceList(PieceType.King, true).First();
            var blackKing = board.GetPieceList(PieceType.King, false).First();
            score += 0.3 * (7 - DistanceToCenter(whiteKing.Square));
            score -= 0.3 * (7 - DistanceToCenter(blackKing.Square));
        }

        return board.IsWhiteToMove ? score : -score;
    }

    private int GetPieceCount(Board board, bool isWhite)
    {
        int startIndex = isWhite ? 0 : 6;  // 0 for white, 6 for black
        int endIndex = isWhite ? 5 : 11;   // 5 for white, 11 for black

        int count = 0;

        var allPieceLists = board.GetAllPieceLists();
        for (int i = startIndex; i <= endIndex; i++)
        {
            count += allPieceLists[i].Count();
        }

        return count;
    }

    private double DistanceToCenter(Square square)
    {
        int dx = Math.Min(Math.Abs(square.File - 'd'), Math.Abs(square.File - 'e'));
        int dy = Math.Min(Math.Abs(square.Rank - 4), Math.Abs(square.Rank - 5));

        // Using Euclidean distance for simplicity
        return Math.Sqrt(dx * dx + dy * dy);
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

    private double QuiescenceSearch(Board board, double alpha, double beta, int depthLeft)
    {
        if (depthLeft == 0 || _timer.MillisecondsRemaining <= (_timer.GameStartTimeMilliseconds / 1024.0))
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
}
