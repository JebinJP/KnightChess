using System.Diagnostics;
using System.IO;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Engine;

/// <summary>
/// UCI-compatible chess engine integration (Stockfish) with embedded fallback move generation.
/// </summary>
public class UciStockfishEngine : IEngineService
{
    private readonly ILogger<UciStockfishEngine> _logger;
    private Process? _process;
    private StreamWriter? _input;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Random _random = new();

    public bool IsEngineAvailable { get; private set; }
    public string EngineName { get; private set; } = "Built-in Chess Engine";

    public UciStockfishEngine(ILogger<UciStockfishEngine> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync(string? executablePath = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            StopProcessInternal();

            string? resolvedPath = ResolveEnginePath(executablePath);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                _logger.LogInformation("Stockfish executable not found. Using built-in engine.");
                IsEngineAvailable = false;
                EngineName = "Built-in Chess Engine";
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = new Process { StartInfo = startInfo };
            proc.Start();

            _input = proc.StandardInput;
            _process = proc;

            await _input.WriteLineAsync("uci");
            await _input.FlushAsync(cancellationToken);

            // Read until uciok
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync(cancellationToken)) != null)
            {
                if (line.StartsWith("id name "))
                {
                    EngineName = line["id name ".Length..].Trim();
                }

                if (line == "uciok")
                    break;
            }

            await _input.WriteLineAsync("isready");
            await _input.FlushAsync(cancellationToken);

            while ((line = await proc.StandardOutput.ReadLineAsync(cancellationToken)) != null)
            {
                if (line == "readyok")
                    break;
            }

            IsEngineAvailable = true;
            _logger.LogInformation("Stockfish initialized successfully: {Name}", EngineName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start external Stockfish process. Falling back to built-in engine.");
            StopProcessInternal();
            IsEngineAvailable = false;
            EngineName = "Built-in Chess Engine";
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetBestMoveAsync(string fen, EngineDifficulty difficulty = EngineDifficulty.Casual,
        int moveTimeMs = 800, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsEngineAvailable && _process != null && !_process.HasExited && _input != null)
            {
                int skill = difficulty switch
                {
                    EngineDifficulty.Beginner => 1,
                    EngineDifficulty.Casual => 5,
                    EngineDifficulty.Intermediate => 10,
                    EngineDifficulty.Advanced => 15,
                    EngineDifficulty.Master => 20,
                    _ => 5
                };

                await _input.WriteLineAsync($"setoption name Skill Level value {skill}");
                await _input.WriteLineAsync($"position fen {fen}");
                await _input.WriteLineAsync($"go movetime {moveTimeMs}");
                await _input.FlushAsync(cancellationToken);

                string? line;
                while ((line = await _process.StandardOutput.ReadLineAsync(cancellationToken)) != null)
                {
                    if (line.StartsWith("bestmove "))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 2 && parts[1] != "(none)")
                        {
                            return parts[1];
                        }
                        break;
                    }
                }
            }

            // Fallback engine move calculation
            return CalculateFallbackMove(fen, difficulty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting best move from engine. Using fallback.");
            return CalculateFallbackMove(fen, difficulty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<EngineEvaluation?> EvaluatePositionAsync(string fen, int depth = 12, int timeoutMs = 1500,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsEngineAvailable || _process == null || _process.HasExited || _input == null)
            {
                return EvaluateFallback(fen);
            }

            await _input.WriteLineAsync($"position fen {fen}");
            await _input.WriteLineAsync($"go depth {depth}");
            await _input.FlushAsync(cancellationToken);

            var eval = new EngineEvaluation();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                string? line;
                while ((line = await _process.StandardOutput.ReadLineAsync(cts.Token)) != null)
                {
                    if (line.StartsWith("info ") && line.Contains("score "))
                    {
                        ParseInfoLine(line, eval);
                    }
                    else if (line.StartsWith("bestmove "))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 2)
                        {
                            eval.BestMoveUci = parts[1];
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await _input.WriteLineAsync("stop");
                await _input.FlushAsync(CancellationToken.None);
            }

            return eval;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        if (IsEngineAvailable && _input != null)
        {
            try
            {
                await _input.WriteLineAsync("stop");
                await _input.FlushAsync();
            }
            catch { /* ignore */ }
        }
    }

    private static void ParseInfoLine(string line, EngineEvaluation eval)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i] == "depth" && int.TryParse(tokens[i + 1], out int d))
                eval.Depth = d;
            else if (tokens[i] == "cp" && int.TryParse(tokens[i + 1], out int cp))
                eval.ScoreCentipawns = cp;
            else if (tokens[i] == "mate" && int.TryParse(tokens[i + 1], out int mate))
                eval.MateInMoves = mate;
            else if (tokens[i] == "nps" && long.TryParse(tokens[i + 1], out long nps))
                eval.NodesPerSecond = nps;
            else if (tokens[i] == "pv")
            {
                eval.PrincipalVariation = tokens.Skip(i + 1).ToList();
                if (eval.PrincipalVariation.Count > 0)
                    eval.BestMoveUci = eval.PrincipalVariation[0];
                break;
            }
        }
    }

    /// <summary>
    /// Tactical heuristic move picker used when Stockfish binary is not present on disk.
    /// Evaluates piece values, checks, captures, and center control.
    /// </summary>
    private string? CalculateFallbackMove(string fen, EngineDifficulty difficulty)
    {
        try
        {
            var game = new GeraChessGameAdapter(fen);

            var legalMoves = new List<string>();
            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    var from = new BoardSquare(file, rank);
                    var piece = game.GetPieceAt(from);
                    if (piece != null && piece.Color == game.ActiveTurn)
                    {
                        var destinations = game.GetLegalDestinations(from);
                        foreach (var to in destinations)
                        {
                            var uci = $"{from.AlgebraicNotation}{to.AlgebraicNotation}";
                            if (piece.Type == PieceType.Pawn && (to.Rank == 7 || to.Rank == 0))
                            {
                                legalMoves.Add(uci + "q");
                            }
                            else
                            {
                                legalMoves.Add(uci);
                            }
                        }
                    }
                }
            }

            if (legalMoves.Count == 0) return null;

            var scoredMoves = new List<(string Uci, int Score)>();
            foreach (var uci in legalMoves)
            {
                int score = 0;
                var toSq = BoardSquare.FromAlgebraic(uci.Substring(2, 2));
                var capturedPiece = game.GetPieceAt(toSq);
                if (capturedPiece != null)
                {
                    score += capturedPiece.MaterialValue * 100;
                }

                if (uci.Contains("e4") || uci.Contains("d4") || uci.Contains("e5") || uci.Contains("d5"))
                    score += 40;

                scoredMoves.Add((uci, score));
            }

            scoredMoves.Sort((a, b) => b.Score.CompareTo(a.Score));

            return difficulty switch
            {
                EngineDifficulty.Beginner => _random.Next(3) == 0 ? scoredMoves[0].Uci : scoredMoves[_random.Next(scoredMoves.Count)].Uci,
                EngineDifficulty.Casual => scoredMoves[_random.Next(Math.Min(3, scoredMoves.Count))].Uci,
                _ => scoredMoves[0].Uci
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback move generation encountered an issue.");
            return null;
        }
    }

    private static EngineEvaluation EvaluateFallback(string fen)
    {
        var eval = new EngineEvaluation();
        var game = new GeraChessGameAdapter(fen);

        if (game.IsGameOver && game.Result != GameResult.Draw)
        {
            eval.MateInMoves = 0;
            return eval;
        }

        int whiteMaterial = 0;
        int blackMaterial = 0;

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                var piece = game.GetPieceAt(file, rank);
                if (piece is null) continue;

                int val = piece.MaterialValue * 100;
                if (piece.Color == PieceColor.White) whiteMaterial += val;
                else blackMaterial += val;
            }
        }

        int cp = game.ActiveTurn == PieceColor.White
            ? whiteMaterial - blackMaterial
            : blackMaterial - whiteMaterial;

        eval.ScoreCentipawns = cp;
        eval.Depth = 1;
        return eval;
    }

    private static string? ResolveEnginePath(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return path;

        // Check local directories
        var localDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(localDir, "stockfish.exe"),
            Path.Combine(localDir, "stockfish_windows_x64.exe"),
            Path.Combine(localDir, "stockfish-windows-x86-64-avx2.exe"),
            Path.Combine(localDir, "engine", "stockfish.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void StopProcessInternal()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _input?.WriteLine("quit");
                _input?.Flush();
                if (!_process.WaitForExit(1000))
                {
                    _process.Kill();
                }
            }
        }
        catch { /* ignore */ }
        finally
        {
            _process?.Dispose();
            _process = null;
            _input = null;
        }
    }

    public void Dispose()
    {
        StopProcessInternal();
        _lock.Dispose();
    }
}
