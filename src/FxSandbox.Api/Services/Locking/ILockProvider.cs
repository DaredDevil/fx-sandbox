namespace FxSandbox.Services.Locking;

/// <summary>
/// Read/write lock abstraction. Swap the implementation without touching any caller.
///
/// Current default:  <see cref="LocalLockProvider"/> — ReaderWriterLockSlim.
///                   Correct for a single-process / single-pod deployment.
///
/// Multi-pod upgrade:
///   1. Implement RedisLockProvider : ILockProvider (see stub below).
///   2. In Program.cs change:
///        services.AddSingleton&lt;ILockProvider, LocalLockProvider&gt;();
///      to:
///        services.AddSingleton&lt;ILockProvider, RedisLockProvider&gt;();
///
/// ⚠ Distributed lock alone is not sufficient for multi-pod correctness.
///   The in-memory state (orders, balances, positions) must also move to a
///   shared store (Redis). Implement <see cref="ITradingEngine"/> backed by
///   Redis and swap it in DI — the locking and engine are independent seams.
/// </summary>
public interface ILockProvider : IDisposable
{
    void EnterReadLock();
    void ExitReadLock();
    void EnterWriteLock();
    void ExitWriteLock();
}

// ── Redis-backed distributed lock stub ─────────────────────────────────────
// Prerequisites (add to FxSandbox.Api.csproj):
//   <PackageReference Include="StackExchange.Redis"  Version="2.8.16" />
//   <PackageReference Include="RedLock.net"          Version="2.3.2"  />
//
// Registration in Program.cs:
//   builder.Services.AddSingleton<IConnectionMultiplexer>(
//       _ => ConnectionMultiplexer.Connect("redis:6379"));
//   builder.Services.AddSingleton<ILockProvider, RedisLockProvider>();
//
// public sealed class RedisLockProvider(IConnectionMultiplexer redis) : ILockProvider
// {
//     private const string Key = "fx-sandbox:engine-lock";
//     private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
//     private readonly IDatabase _db = redis.GetDatabase();
//     private string? _token;
//
//     public void EnterReadLock()  => AcquireExclusive(); // simple model: treat reads as writes
//     public void ExitReadLock()   => Release();
//     public void EnterWriteLock() => AcquireExclusive();
//     public void ExitWriteLock()  => Release();
//
//     private void AcquireExclusive()
//     {
//         _token = Guid.NewGuid().ToString();
//         while (!_db.StringSet(Key, _token, Ttl, When.NotExists))
//             Thread.Sleep(10); // add exponential back-off in production
//     }
//
//     private void Release()
//     {
//         // Lua ensures atomicity: only delete if we still own the lock
//         const string script =
//             "if redis.call('get',KEYS[1])==ARGV[1] then " +
//             "  return redis.call('del',KEYS[1]) " +
//             "else return 0 end";
//         _db.ScriptEvaluate(script, [(RedisKey)Key], [(RedisValue)(_token ?? "")]);
//     }
//
//     public void Dispose() { }
// }
