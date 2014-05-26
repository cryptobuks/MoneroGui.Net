﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;

namespace Jojatekok.MoneroAPI.ProcessManagers
{
    public class WalletManager : BaseProcessManager, IDisposable
    {
        public EventHandler ReadyToLogin;
        public EventHandler Refreshed;
        public EventHandler<string> AddressReceived;
        public EventHandler<Balance> BalanceChanged;
        public EventHandler<string> SentMoney;

        private Paths Paths { get; set; }

        private ObservableCollection<Transaction> TransactionsPrivate { get; set; }
        public ReadOnlyObservableCollection<Transaction> Transactions { get; private set; }

        private Timer RefreshTimer { get; set; }

        internal WalletManager(Paths paths) : base(paths.SoftwareWallet)
        {
            ErrorReceived += Process_ErrorReceived;
            OutputReceived += Process_OutputReceived;

            Paths = paths;

            TransactionsPrivate = new ObservableCollection<Transaction>();
            Transactions = new ReadOnlyObservableCollection<Transaction>(TransactionsPrivate);

            if (File.Exists(Paths.FileWalletData)) {
                StartProcess("--wallet-file=" + Paths.FileWalletData);
            } else {
                if (!Directory.Exists(Paths.DirectoryWalletData)) Directory.CreateDirectory(Paths.DirectoryWalletData);
                StartProcess("--generate-new-wallet=" + Paths.FileWalletData);
            }

            RefreshTimer = new Timer(10000);
            RefreshTimer.Elapsed += (sender, e) => Refresh();
            RefreshTimer.Start();
        }

        private void Process_ErrorReceived(object sender, string e)
        {
            if (e.Contains("failed to connect")) {
                // Cannot connect to the daemon

            } else if (e.Contains("failed to generate new wallet")) {
                // Failed to generate a new wallet file

            } else if (e.Contains("invalid password")) {
                // Invalid password

            } else if (e.Contains("wrong address")) {
                // Invalid send address

            } else if (e.Contains("not enough money")) {
                // Not enough money
            }

            // TODO: Handle unexpected errors
        }

        private void Process_OutputReceived(object sender, string e)
        {
            var data = e.ToLower(Helper.InvariantCulture);

            // <-- Reply methods -->

            if (Refreshed != null && data.Contains("refresh done")) {
                Refreshed(this, EventArgs.Empty);
                return;
            }

            if (BalanceChanged != null && data.Contains("balance")) {
                var match = Regex.Match(data, "balance: ([0-9\\.,]*), unlocked balance: ([0-9\\.,]*)");
                if (match.Success) {
                    var total = double.Parse(match.Groups[1].Value, Helper.InvariantCulture);
                    var spendable = double.Parse(match.Groups[2].Value, Helper.InvariantCulture);

                    BalanceChanged(this, new Balance(total, spendable));
                }

                return;
            }

            if (SentMoney != null && data.Contains("money successfully sent")) {
                var match = Regex.Match(data, "transaction <([0-9a-z]*)>");
                if (match.Success) {
                    SentMoney(this, match.Groups[1].Value);
                }

                return;
            }

            // <-- Transaction fetching -->

            if (Regex.IsMatch(data, "amount[\\s]+spent")) {
                TransactionsPrivate.Clear();
                return;
            }

            var newTransactionMatch = Regex.Match(data, "([0-9]+\\.[0-9]+)[\\s]*([TF])[\\s]*[0-9]+[\\s]*<([0-9a-z]+)>");
            if (newTransactionMatch.Success) {
                var amount = double.Parse(newTransactionMatch.Groups[1].Value, Helper.InvariantCulture);
                var type = newTransactionMatch.Groups[2].Value == "T" ? TransactionType.Send : TransactionType.Receive;
                var transactionId = newTransactionMatch.Groups[3].Value;

                TransactionsPrivate.Add(new Transaction(amount, type, transactionId));

                return;
            }

            // <-- Initializer methods -->

            if (ReadyToLogin != null && data.Contains("bitmonero wallet")) {
                ReadyToLogin(this, EventArgs.Empty);
                return;
            }

            if (AddressReceived != null && (data.Contains("opened wallet: ") || data.Contains("generated new wallet: "))) {
                AddressReceived(this, data.Substring(data.IndexOf(':') + 1).Trim());
                return;
            }

            // <-- Error handler -->

            if (data.Contains("error")) {
                ErrorReceived(this, data);
            }
        }

        public void Login(string password = "x")
        {
            Send(password);
        }

        public void GetBalance()
        {
            Send("balance");
        }

        public void Transfer(string address, double amount, int mixCount)
        {
            Send(string.Format(Helper.InvariantCulture, "transfer {0} {1} {2}", mixCount, address.Trim(), amount));
        }

        public void Refresh()
        {
            Send("refresh");
            Send("incoming_transfers");
        }

        public void Backup(string path = null)
        {
            if (path == null) {
                path = Paths.DirectoryWalletDataBackups + DateTime.Now.ToString("yyyy-MM-dd");
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var walletName = Paths.FileWalletData;
            walletName = walletName.Substring(0, walletName.LastIndexOf('.'));

            var filesToBackup = Directory.GetFiles(Paths.DirectoryWalletData, walletName + "*");
            for (var i = filesToBackup.Length - 1; i >= 0; i--) {
                var file = filesToBackup[i];
                Debug.Assert(file != null, "file != null");
                File.Copy(file, Path.Combine(path, Path.GetFileName(file)), true);
            }
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing) {
                if (RefreshTimer != null) {
                    RefreshTimer.Dispose();
                    RefreshTimer = null;
                }

                base.Dispose();
            }
        }
    }
}