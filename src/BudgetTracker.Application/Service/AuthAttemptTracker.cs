using System.Collections.Concurrent;

namespace BudgetTracker.Application.Service;

/// <summary>
/// Contador de tentativas e bloqueios temporários por conta, em memória. Vale para uma única
/// instância da API e é zerado quando ela reinicia.
/// </summary>
public sealed class AuthAttemptTracker(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset ExpiresAt)> _entries = new();

    /// <summary>Registra uma tentativa; retorna false quando o limite da janela já foi atingido.</summary>
    public bool TryRegisterAttempt(string key, int maxAttempts, TimeSpan window)
    {
        var now = _time.GetUtcNow();
        var entry = _entries.AddOrUpdate(
            key,
            _ => (1, now + window),
            (_, current) => current.ExpiresAt <= now ? (1, now + window) : (current.Count + 1, current.ExpiresAt));

        return entry.Count <= maxAttempts;
    }

    public void Lock(string key, TimeSpan duration) => _entries[key] = (1, _time.GetUtcNow() + duration);

    public bool IsLocked(string key) => _entries.TryGetValue(key, out var entry) && entry.ExpiresAt > _time.GetUtcNow();

    public void Clear(string key) => _entries.TryRemove(key, out _);
}
