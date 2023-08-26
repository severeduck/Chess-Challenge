﻿using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private const int QUIESCENCE_MAX_DEPTH = 2;
    private int _maxDepth;
    private Timer _timer;

    // This dictionary will only hold the UCI move strings, as the board will be provided when initializing the Move.
    private Dictionary<string, string> _openingBook = new Dictionary<string, string>
    {
        // King's Pawn Opening
        { "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "e2e4" },
        { "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", "c7c5" }, // Sicilian Defense
        
        // Queen's Pawn Opening
        { "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "d2d4" },
        { "rnbqkbnr/pppppppp/8/8/3P4/8/PPP2PPP/RNBQKBNR b KQkq d3 0 1", "d7d5" }, // Double Queen's Pawn Opening
        { "rnbqkbnr/pppppppp/8/8/3P4/8/PPP2PPP/RNBQKBNR b KQkq d3 0 1", "g8f6" }, // Indian Game

        // More openings can be added as needed.
    };

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

        // Check if the current board position is in the opening book
        var currentFen = board.GetFenString();
        if (_openingBook.ContainsKey(currentFen))
        {
            return new Move(_openingBook[currentFen], board);
        }

        return AlphaBetaSearch(board, legalMoves, _maxDepth);
    }

    private Move AlphaBetaSearch(Board board, List<Move> legalMoves, int depth)
    {
        Move? bestMove = null;
        double alpha = double.NegativeInfinity;
        double beta = double.PositiveInfinity;

        foreach (var move in legalMoves)
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

        return bestMove.HasValue ? bestMove.Value : legalMoves.First();
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

        return whiteScore - blackScore; 
    }

    private int CalculateDynamicDepth(Timer timer)
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
