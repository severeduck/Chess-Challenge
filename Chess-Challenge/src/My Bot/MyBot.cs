using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    const int MAX_DEPTH = 6;
    const int MATE_DEPTH = 4;  // Increase mate depth
    const double PANIC_FRACTION = 0.15;
    Timer t;

    public Move Think(Board b, ChessChallenge.API.Timer timer)
    {
        t = timer;

        var moves = b.GetLegalMoves().ToList();
        if (!moves.Any()) return default;

        // Prioritize moves that avoid check
        moves = moves.OrderByDescending(m => DoesMoveAvoidCheck(m, b))
                     .ThenByDescending(m => m.IsCapture)
                     .ThenByDescending(m => VictimValue(m.CapturePieceType) - AttackerValue(m.MovePieceType))
                     .ToList();

        for (int d = 1; d <= MATE_DEPTH; d++)
        {
            var mateMove = MateInMoves(b, d);
            if (mateMove != null) return (Move)mateMove;
        }

        double timeFraction = IsPanic() ? 0.01 : 0.02;
        DateTime start = DateTime.Now;
        Move best = moves.First();

        int depth = DynamicDepth(b, timer);
        while ((DateTime.Now - start).TotalMilliseconds + 50 < t.MillisecondsRemaining * timeFraction && depth <= MAX_DEPTH)
        {
            best = Search(b, moves, depth, double.NegativeInfinity, double.PositiveInfinity);
            depth++;
        }

        return best == default ? moves[new Random().Next(moves.Count)] : best;
    }

    public Move? MateInMoves(Board b, int depth)
    {
        double alpha = double.NegativeInfinity;
        double beta = double.PositiveInfinity;

        foreach (var move in b.GetLegalMoves())
        {
            b.MakeMove(move);
            if (b.IsInCheckmate())
            {
                b.UndoMove(move);
                return move;
            }

            double score = -MateSearch(b, depth - 1, -beta, -alpha);

            b.UndoMove(move);

            if (score == double.PositiveInfinity) // This means we found a path that leads to a mate.
                return move;
        }

        return null;
    }

    double MateSearch(Board b, int depth, double alpha, double beta)
    {
        if (depth == 0 || b.IsDraw()) return Eval(b);

        if (b.IsInCheckmate())
            return b.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;

        foreach (var move in b.GetLegalMoves())
        {
            b.MakeMove(move);
            double score = -MateSearch(b, depth - 1, -beta, -alpha);
            b.UndoMove(move);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    public bool DoesMoveAvoidCheck(Move move, Board board)
    {
        board.MakeMove(move);
        bool isStillInCheck = board.IsInCheck();
        board.UndoMove(move);
        return !isStillInCheck;
    }

    double Eval(Board b)
    {
        double score = 0;

        if (b.IsInCheckmate())
        {
            return b.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;
        }

        score -= b.GetPieceList(PieceType.Pawn, true).GroupBy(p => p.Square.File).Where(g => g.Count() > 1).Count() * 0.5;
        score += b.GetPieceList(PieceType.Pawn, false).GroupBy(p => p.Square.File).Where(g => g.Count() > 1).Count() * 0.5;

        score += 0.1 * b.GetLegalMoves(true).Count() - 0.1 * b.GetLegalMoves(false).Count();

        if (b.GetAllPieceLists().Take(6).Sum(l => l.Count()) <= 7 && b.GetAllPieceLists().Skip(6).Sum(l => l.Count()) <= 7)
        {
            var whiteKing = b.GetPieceList(PieceType.King, true).First().Square;
            var blackKing = b.GetPieceList(PieceType.King, false).First().Square;
            score += 0.3 * (7 - DistanceToCenter(whiteKing)) - 0.3 * (7 - DistanceToCenter(blackKing));
        }

        return b.IsWhiteToMove ? score : -score;
    }
    Move Search(Board b, List<Move> moves, int d, double alpha, double beta)
    {
        Move best = moves.First();
        foreach (var m in moves)
        {
            b.MakeMove(m);
            double val = -AlphaBeta(b, d - 1, -beta, -alpha);
            b.UndoMove(m);
            if (val > alpha)
            {
                alpha = val;
                best = m;
            }
        }
        return best;
    }
    double AlphaBeta(Board b, int d, double alpha, double beta)
    {
        if (d == 0 || b.IsInCheckmate() || b.IsDraw()) return Eval(b);

        var moves = b.GetLegalMoves().ToList();
        if (!moves.Any()) return 0;

        foreach (var m in moves)
        {
            b.MakeMove(m);
            double val = -AlphaBeta(b, d - 1, -beta, -alpha);
            b.UndoMove(m);

            if (val >= beta) return beta;
            if (val > alpha) alpha = val;
        }
        return alpha;
    }

    int VictimValue(PieceType p) => p switch { PieceType.Pawn => 1, PieceType.Knight => 3, PieceType.Bishop => 3, PieceType.Rook => 5, PieceType.Queen => 9, _ => 0 };
    int AttackerValue(PieceType p) => p == PieceType.Pawn ? 1 : 0;

    int DistanceToCenter(Square s) => Math.Abs(4 - s.File) + Math.Abs(4 - s.Rank);

    int DynamicDepth(Board b, ChessChallenge.API.Timer t)
    {
        double estMoves = (t.GameStartTimeMilliseconds - t.MillisecondsElapsedThisTurn) / (double)t.MillisecondsElapsedThisTurn;
        double avgTime = t.MillisecondsRemaining / estMoves;

        int depth = 4;

        if (avgTime < t.GameStartTimeMilliseconds * 0.01) depth = 2;
        else if (avgTime < t.GameStartTimeMilliseconds * 0.05) depth = 3;

        if (b.GetAllPieceLists().Take(6).Sum(l => l.Count()) <= 7 && b.GetAllPieceLists().Skip(6).Sum(l => l.Count()) <= 7) depth += 2;

        return depth;
    }

    bool IsPanic() => t.MillisecondsRemaining <= (t.GameStartTimeMilliseconds * PANIC_FRACTION);
}
