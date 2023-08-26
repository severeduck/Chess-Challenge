using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private const int CheckmateScore = 100000;
    private readonly OpeningBook _openingBook;
    private Dictionary<string, int> _evaluatedPositions;

    public MyBot()
    {
        _openingBook = new OpeningBook();
        _evaluatedPositions = new Dictionary<string, int>();
    }

    public Move Think(Board board, Timer timer)
    {
        _evaluatedPositions.Clear();
        var bestMove = MinimaxRoot(board, CalculateDynamicDepth(board));
        return bestMove;
    }

    private int CalculateDynamicDepth(Board board)
    {
        // A very simple logic to increase depth as the game progresses. 
        // This can be expanded or replaced with a more robust approach.
        int totalPieces = board.WhitePieces.Count() + board.BlackPieces.Count();
        return totalPieces > 25 ? 3 : 4; // Adjust values as per testing
    }

    private Move MinimaxRoot(Board board, int depth)
    {
        var validMoves = board.GetValidMovesForCurrentPlayer();
        int bestMoveScore = int.MinValue;
        Move bestMove = validMoves.First();

        foreach (var move in validMoves)
        {
            board.MakeMove(move);
            var boardValue = -AlphaBeta(board, depth - 1, int.MinValue, int.MaxValue);
            board.UndoLastMove();

            if (boardValue > bestMoveScore)
            {
                bestMoveScore = boardValue;
                bestMove = move;
            }
        }
        return bestMove;
    }

    private int AlphaBeta(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0)
            return QuiescenceSearch(board, alpha, beta, depth);

        var validMoves = board.GetValidMovesForCurrentPlayer();

        if (!validMoves.Any())
        {
            return board.CurrentPlayer == board.WhitePlayer ? -CheckmateScore : CheckmateScore;
        }

        foreach (var move in validMoves)
        {
            board.MakeMove(move);
            int boardValue = -AlphaBeta(board, depth - 1, -beta, -alpha);
            board.UndoLastMove();

            if (boardValue >= beta)
                return beta;
            if (boardValue > alpha)
                alpha = boardValue;
        }

        return alpha;
    }

    private int QuiescenceSearch(Board board, int alpha, int beta, int depth)
    {
        int standPat = EvaluateBoard(board);
        if (standPat >= beta)
            return beta;
        if (alpha < standPat)
            alpha = standPat;

        var moves = board.GetValidMovesForCurrentPlayer().Where(move => move.IsCapture);

        foreach (var move in moves)
        {
            board.MakeMove(move);
            int score = -QuiescenceSearch(board, -beta, -alpha, depth + 1);
            board.UndoLastMove();

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }

    private int EvaluateBoard(Board board)
    {
        string fen = board.GetFen();
        if (_evaluatedPositions.ContainsKey(fen))
        {
            return _evaluatedPositions[fen];
        }

        int evaluation = 0;

        if (_openingBook.ContainsPosition(fen))
        {
            evaluation = _openingBook.GetEvaluation(fen);
        }
        else
        {
            foreach (var piece in board.WhitePieces)
            {
                evaluation += GetPieceValue(piece);
            }

            foreach (var piece in board.BlackPieces)
            {
                evaluation -= GetPieceValue(piece);
            }
        }

        _evaluatedPositions[fen] = evaluation;
        return evaluation;
    }

    private int GetPieceValue(Piece piece)
    {
        // Values can be adjusted based on preference
        return piece.Type switch
        {
            PieceType.Pawn => 10,
            PieceType.Knight => 30,
            PieceType.Bishop => 30,
            PieceType.Rook => 50,
            PieceType.Queen => 90,
            PieceType.King => 900,
            _ => 0,
        };
    }
}

public class OpeningBook
{
    // Dummy example
    private Dictionary<string, int> _openings = new Dictionary<string, int>
    {
        // ["example_fen"] = evaluation
    };

    public bool ContainsPosition(string fen)
    {
        return _openings.ContainsKey(fen);
    }

    public int GetEvaluation(string fen)
    {
        return _openings.TryGetValue(fen, out int value) ? value : 0;
    }
}
