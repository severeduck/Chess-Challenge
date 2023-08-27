using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    const int MAX_DEPTH = 6;
    const int MATE_DEPTH = 3;
    const double PANIC_FRACTION = 0.15;
    Timer t;

    public Move Think(Board b, ChessChallenge.API.Timer timer)
    {
        t = timer;

        var moves = b.GetLegalMoves().ToList();
        if (!moves.Any()) return default;

        moves = moves.OrderByDescending(m => m.IsCapture)
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

    Move? MateInMoves(Board b, int n)
    {
        double alpha = double.NegativeInfinity;
        foreach (var m in b.GetLegalMoves())
        {
            b.MakeMove(m);
            double score = -MateSearch(b, n * 2 - 1, -double.PositiveInfinity, -alpha);
            b.UndoMove(m);
            if (score == double.PositiveInfinity) return m;
        }
        return null;
    }

    double MateSearch(Board b, int d, double alpha, double beta)
    {
        if (d == 0 || b.IsInCheckmate() || b.IsDraw())
        {
            return b.IsInCheckmate() 
                   ? (b.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity)
                   : 0;
        }

        foreach (var m in b.GetLegalMoves())
        {
            b.MakeMove(m);
            double score = -MateSearch(b, d - 1, -beta, -alpha);
            b.UndoMove(m);
            if (score >= beta) return beta;
            alpha = Math.Max(alpha, score);
        }
        return alpha;
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

    double Eval(Board b)
    {
        double score = 0;
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
