﻿using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int maxDepth = 4;  // or calculate dynamically based on timer
        Move bestMove = Move.NullMove;

        int alpha = int.MinValue;
        int beta = int.MaxValue;

        var legalMoves = board.GetLegalMoves(false);

        // If there are no legal moves, just return the null move.
        if (legalMoves.Length == 0)
        {
            return Move.NullMove;
        }

        foreach (var move in legalMoves)
        {
            board.MakeMove(move);
            int moveValue = -AlphaBeta(board, maxDepth - 1, -beta, -alpha);
            board.UndoMove(move);
            if (moveValue > alpha)
            {
                alpha = moveValue;
                bestMove = move;
            }
        }

        // Check if the best move is in the legal moves list.
        if (!Array.Exists(legalMoves, m => m.Equals(bestMove)))
        {
            throw new Exception("Invalid best move detected!");
        }

        return bestMove;
    }
    int AlphaBeta(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
            return EvaluateBoard(board);

        var legalMoves = board.GetLegalMoves(false);
        foreach (var move in legalMoves)
        {
            board.MakeMove(move);
            int score = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }

    int EvaluateBoard(Board board)
    {
        // A simple evaluation function for demonstration.
        // Here, you would use your own logic to assign a score to the board.
        // For now, we'll simply count the material difference.
        int value = 0;

        // Assign piece values and sum up
        foreach (var pieceList in board.GetAllPieceLists())
        {
            int pieceValue;
            switch (pieceList.TypeOfPieceInList)
            {
                case PieceType.Pawn: pieceValue = 1; break;
                case PieceType.Knight: pieceValue = 3; break;
                case PieceType.Bishop: pieceValue = 3; break;
                case PieceType.Rook: pieceValue = 5; break;
                case PieceType.Queen: pieceValue = 9; break;
                default: pieceValue = 0; break;
            }

            value += pieceList.IsWhitePieceList ? pieceValue * pieceList.Count : -pieceValue * pieceList.Count;
        }

        return board.IsWhiteToMove ? value : -value;
    }
}
