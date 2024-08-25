using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtpServer;
using SmtpServer.Authentication;

namespace LocalSmtpRelay.Components
{
    public sealed class SmtpServerUserAuthenticator : IUserAuthenticator
    {
        private readonly ILogger<SmtpServerUserAuthenticator> _logger;
        private Account[] _accounts;
        private readonly ReaderWriterLockSlim _accountLock;
        private readonly IOptionsMonitor<SmtpServerUserAuthenticatorOptions> _options;
        private readonly IDisposable? _optionsListener;

        public sealed class Factory : IUserAuthenticatorFactory, IDisposable
        {
            private readonly SmtpServerUserAuthenticator _instance;

            public Factory(IOptionsMonitor<SmtpServerUserAuthenticatorOptions> options, ILogger<SmtpServerUserAuthenticator> logger)
            {
                _instance = new SmtpServerUserAuthenticator(options, logger);
            }

            public IUserAuthenticator CreateInstance(ISessionContext context) => _instance;

            public void Dispose() => _instance.Close();
        }

        sealed class Account
        {
            public string Username { get; }
            public string Password { get; }

            public Account(string username, string password)
            {
                Username = username;
                Password = password;
            }
        }

#pragma warning disable CS8618 // _accounts set by UpdateAccounts
        public SmtpServerUserAuthenticator(IOptionsMonitor<SmtpServerUserAuthenticatorOptions> options, ILogger<SmtpServerUserAuthenticator> logger)
#pragma warning restore CS8618
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _accountLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _optionsListener = _options.OnChange(OnSmtpServerUserAuthenticatorOptionsChanged);
            UpdateAccounts(_options.CurrentValue, throwErrors: true);
        }

        private void OnSmtpServerUserAuthenticatorOptionsChanged(SmtpServerUserAuthenticatorOptions options, string? name)
        {
            UpdateAccounts(options, throwErrors: false);
        }

        private static Account MapToAccount(SmtpServerUserAuthenticatorOptions.Account accountOptions)
        {
            ArgumentNullException.ThrowIfNull(accountOptions);
            if (string.IsNullOrEmpty(accountOptions.Username))
                throw new ArgumentOutOfRangeException(nameof(accountOptions), "Username must be set.");

            if (!string.IsNullOrEmpty(accountOptions.PasswordFile))
            {
                if (!File.Exists(accountOptions.PasswordFile))
                    throw new FileNotFoundException($"{nameof(accountOptions.PasswordFile)} not found.", accountOptions.PasswordFile);

                return new Account(accountOptions.Username, File.ReadAllText(accountOptions.PasswordFile));
            }

            if (string.IsNullOrEmpty(accountOptions.Password))
                throw new ArgumentOutOfRangeException(nameof(accountOptions), $"Password must be set for username '{accountOptions.Username}'.");

            return new Account(accountOptions.Username, accountOptions.Password);
        }

        private void UpdateAccounts(SmtpServerUserAuthenticatorOptions options, bool throwErrors)
        {
            if (options.Accounts is null || options.Accounts.Length == 0)
            {
                _accountLock.EnterWriteLock();
                try
                {
                    _accounts = [];
                }
                finally
                {
                    _accountLock.ExitWriteLock();
                }
            }
            else
            {
                Account[] newAccounts;
                try
                {
                    var list = new List<Account>();
                    foreach (var item in options.Accounts)
                    {
                        list.Add(MapToAccount(item));
                    }
                    newAccounts = [.. list];
                }
                catch (Exception ex)
                {
                    if (throwErrors)
                        throw;
                    _logger.LogError(ex, "Failed to update accounts.");
                    return;
                }

                _accountLock.EnterWriteLock();
                try
                {
                    _accounts = newAccounts;
                }
                finally
                {
                    _accountLock.ExitWriteLock();
                }
            }   
        }

        Task<bool> IUserAuthenticator.AuthenticateAsync(ISessionContext context,
                                            string user,
                                            string password,
                                            CancellationToken cancellationToken)
        {
            bool allowAnonymous = _options.CurrentValue.AllowAnonymous;
            _accountLock.EnterReadLock();
            try
            {
                if (_accounts.Length != 0)
                {
                    foreach (Account account in _accounts)
                    {
                        if (account.Username == user)
                        {
                            bool success = account.Password == password;
                            if (success)
                                _logger.LogInformation("User authenticated: '{User}'.", user);
                            else if (!allowAnonymous)
                                _logger.LogWarning("Bad authentication password for username '{User}'. Expected password length: '{AccountPasswordLength}' received password length: '{PasswordLength}'", user, account.Password.Length, password?.Length);
                            return Task.FromResult(success || allowAnonymous);
                        }
                    }
                }
            }
            finally
            {
                _accountLock.ExitReadLock();
            }
            
            if (allowAnonymous)
            {
                return Task.FromResult(true);
            }

            _logger.LogWarning("Failed authentication for unknown user '{User}'.", user);
            return Task.FromResult(false);
        }

        public void Close()
        {
            _optionsListener?.Dispose();
            _accountLock.Dispose();
        }
    }
}
