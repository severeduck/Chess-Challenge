using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{

    public Move Think(Board board, Timer timer)
    {
        int maxDepth = 4;  // or calculate dynamically based on timer

        int alpha = int.MinValue;
        int beta = int.MaxValue;

        var legalMoves = board.GetLegalMoves(false);

        // If there are no legal moves, just return the null move.
        if (legalMoves.Length == 0)
        {
            return Move.NullMove;
        }

        Move bestMove = legalMoves[0]; // Initialize with the first legal move.

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
        int value = 0;

        // Material Difference (existing logic)
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

        // Mobility: Reward positions where our bot has more legal moves.
        value += board.GetLegalMoves(true).Length * 10;  // Let's give a weight of 10 for each legal move for the bot.
        value -= board.GetLegalMoves(false).Length * 10; // Penalize opponent's moves.

        // TODO: Add more evaluation factors like King Safety, Central Control, Pawn Structure etc.

        return board.IsWhiteToMove ? value : -value;
    }
}
