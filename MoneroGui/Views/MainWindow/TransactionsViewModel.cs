﻿using Jojatekok.MoneroAPI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Jojatekok.MoneroGUI.Views.MainWindow
{
    sealed class TransactionsViewModel : INotifyPropertyChanged
    {
        private ConcurrentReadOnlyObservableCollection<Transaction> _dataSource;
        public ConcurrentReadOnlyObservableCollection<Transaction> DataSource {
            get { return _dataSource; }

            set {
                _dataSource = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
