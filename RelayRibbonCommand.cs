using System.Windows.Input;

namespace Revit.Import.Convertor.App
{
    class RelayRibbonCommand : ICommand
    {
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        public RelayRibbonCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter = null) => _canExecute(parameter!);

        public void Execute(object? parameter = null) => _execute(parameter!);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
