using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arbiter;

public enum AuthResult
{
    Success, UserExists, Failure,
}

public static class Authenticator
{
    private static Dictionary<Guid, int> _guidLookup = new Dictionary<Guid, int>();
    private static Dictionary<string, Guid> _nameLookup = new Dictionary<string, Guid>();
    private static Dictionary<string, Guid> _tokenLookup = new Dictionary<string, Guid>();
    private static Slotter _accounts;
    private static Slotter _tokens;
    private static SemaphoreSlim _sem;

    static Authenticator()
    {
        _sem = new SemaphoreSlim(1, 1);

        if (!File.Exists("./dat/"))
            Directory.CreateDirectory("./dat/");

        var accounts = File.Open("./dat/accounts", FileMode.OpenOrCreate, FileAccess.ReadWrite);
        var tokens = File.Open("./dat/tokens", FileMode.OpenOrCreate, FileAccess.ReadWrite);

        _accounts = new Slotter(accounts, 256);
        _tokens = new Slotter(tokens, 128);

        for (int i = 0; i < _accounts.Count; i++)
        {
            var account = _accounts.GetAsync<Account>(i).GetAwaiter().GetResult();

            if (account == null)
                continue;

            _guidLookup[account.Value.Guid] = account.Index;
            _nameLookup[account.Value.Name] = account.Value.Guid;
        }

        for (int i = 0; i < _tokens.Count; i++)
        {
            var token = _tokens.GetAsync<AccountToken>(i).GetAwaiter().GetResult();

            if (token == null)
                continue;

            _tokenLookup[token.Value.ToCookieValue()] = token.Value.Guid;
        }
    }

    public static async Task<Account?> Get(string cookieValue)
    {
        if (cookieValue.Length != 208)
            return null;

        await _sem.WaitAsync();

        if (!_tokenLookup.TryGetValue(cookieValue, out Guid guid))
        {
            _sem.Release();
            return null;
        }

        int index = _guidLookup[guid];
        var account = await _accounts.GetAsync<Account>(index);

        if (account == null)
        {
            _sem.Release();
            return null;
        }

        _sem.Release();
        return account.Value;
    }

    public static async Task<(AuthResult Result, AccountToken? Token)> Try(string name, string password)
    {
        await _sem.WaitAsync();

        if (!_nameLookup.TryGetValue(name, out Guid guid))
        {
            _sem.Release();
            return (AuthResult.Failure, null);
        }

        int index = _guidLookup[guid];
        var account = await _accounts.GetAsync<Account>(index);

        if (account == null)
        {
            _sem.Release();
            return (AuthResult.Failure, null);
        }

        if (!account.Value.CheckPassword(password))
        {
            _sem.Release();
            return (AuthResult.Failure, null);
        }

        var token = new AccountToken();

        token.Guid = guid;

        do
            Random.Shared.NextBytes(token.Token);
        while (_tokenLookup.ContainsKey(token.ToCookieValue()));

        _tokenLookup[token.ToCookieValue()] = guid;
        await _tokens.AppendAsync<AccountToken>(token);

        _sem.Release();
        return (AuthResult.Success, token);
    }

    public static async Task<(AuthResult Result, Guid Guid, int id)> Create(string name, string password)
    {
        await _sem.WaitAsync();

        if (_nameLookup.ContainsKey(name))
        {
            _sem.Release();
            return (AuthResult.UserExists, Guid.Empty, -1);
        }

        Guid guid;

        do
            guid = Guid.NewGuid();
        while (_guidLookup.ContainsKey(guid));

        var account = new Account(guid, name, password);
        int id = await _accounts.AppendAsync<Account>(account);

        _nameLookup[name] = guid;
        _guidLookup[guid] = id;
        _sem.Release();

        return (AuthResult.Success, guid, id);
    }
}