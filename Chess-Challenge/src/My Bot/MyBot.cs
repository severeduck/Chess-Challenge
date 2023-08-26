using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{

    public Move Think(Board board, Timer timer)
    {
        DateTime startTime = DateTime.Now;
        TimeSpan timeForMove = TimeSpan.FromSeconds(2); // Temporary heuristic. Adjust as needed.
        Move bestMove = Move.NullMove;

        var legalMoves = board.GetLegalMoves(false);

        // If no legal moves, return NullMove
        if (legalMoves.Length == 0) return Move.NullMove;

        for (int depth = 1; depth <= 100; depth++) // 100 or whatever max depth you choose
        {
            Move currentBestMove = DepthLimitedSearch(board, depth);
            if (DateTime.Now - startTime > timeForMove)
                break;
            if (currentBestMove != Move.NullMove)
                bestMove = currentBestMove;
        }

        // If we don't have a best move from the search, pick the first legal move.
        if (bestMove == Move.NullMove)
            bestMove = legalMoves[0];

        return bestMove;
    }

    public Move DepthLimitedSearch(Board board, int depth)
    {
        int maxDepth = depth;
        Move bestMove = Move.NullMove;

        int alpha = int.MinValue;
        int beta = int.MaxValue;

        var legalMoves = board.GetLegalMoves(false);
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

        // If no move was better than the initial alpha, return a NullMove
        if (alpha == int.MinValue) return Move.NullMove;

        return bestMove;
    }



    int AlphaBeta(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0)
            return QuiescenceSearch(board, alpha, beta);

        if (board.IsDraw() || board.IsInCheckmate())
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

    int QuiescenceSearch(Board board, int alpha, int beta)
    {
        int standPat = EvaluateBoard(board);
        if (standPat >= beta)
            return beta;
        if (alpha < standPat)
            alpha = standPat;

        var captureMoves = board.GetLegalMoves(false); // Assuming this returns only capturing moves

        foreach (var move in captureMoves)
        {
            board.MakeMove(move);
            int score = -QuiescenceSearch(board, -beta, -alpha);
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
        int value = 0;
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
