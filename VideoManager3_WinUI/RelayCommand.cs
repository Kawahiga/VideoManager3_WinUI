using System;
using System.Windows.Input;

namespace VideoManager3_WinUI {
    // ICommandを実装する汎用的なコマンドクラス
    public class RelayCommand:ICommand {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        // Null許容参照型に対応
        public event EventHandler? CanExecuteChanged;

        public RelayCommand( Action<object?> execute, Predicate<object?>? canExecute = null ) {
            _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
            _canExecute = canExecute;
        }

        // ラムダ式でコマンドを作成するためのコンストラクタ
        public RelayCommand( Action execute, Func<bool>? canExecute = null ) {
            _execute = _ => execute();
            if ( canExecute != null ) {
                _canExecute = _ => canExecute();
            }
        }

        public bool CanExecute( object? parameter ) {
            return _canExecute == null || _canExecute( parameter );
        }

        public void Execute( object? parameter ) {
            _execute( parameter );
        }

        public void RaiseCanExecuteChanged() {
            CanExecuteChanged?.Invoke( this, EventArgs.Empty );
        }
    }
}
